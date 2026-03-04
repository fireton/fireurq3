using Urql.Core.Diagnostics;
using Urql.Core.IO;
using Urql.Core.Intermediate;
using Urql.Core.Runtime;
using Urql.Core.Syntax;

namespace Urql.Runner.MonoGame.Runner;

public sealed class QuestSession
{
    private readonly TranscriptBuffer _transcript = new();
    private readonly ChoicePresenter _choicePresenter = new();
    private readonly List<Diagnostic> _diagnostics = [];

    private VirtualMachine? _vm;
    private QuestRunnerConfig _config = new();
    private int _consumedOutputLength;
    private VmStatus _status = VmStatus.Halted;
    private bool _hitInstructionLimit;

    public void Load(string questPath, QuestRunnerConfig config)
    {
        _config = config;
        _vm = null;
        _consumedOutputLength = 0;
        _status = VmStatus.Halted;
        _hitInstructionLimit = false;
        _diagnostics.Clear();
        _transcript.Clear();
        _choicePresenter.Clear();

        if (!File.Exists(questPath))
        {
            _status = VmStatus.Faulted;
            _transcript.AppendSystem($"Quest file not found: {questPath}");
            return;
        }

        try
        {
            var load = UrqlTextLoader.LoadFile(questPath, new UrqlTextLoadOptions(config.EncodingName));
            var source = load.Text;
            var lex = Lexer.Lex(source);
            var parse = Parser.Parse(source, new ParserOptions(CompatibilityMode.DosUrq, AllowUnknownCommands: !config.StrictParserMode));
            var ir = Compiler.Compile(parse.Program, parse.Diagnostics);

            _diagnostics.AddRange(lex.Diagnostics);
            _diagnostics.AddRange(parse.Diagnostics);
            _diagnostics.AddRange(ir.Diagnostics);

            var context = new EvalContext
            {
                CharCodeEncodingName = load.EncodingName
            };
            _vm = new VirtualMachine(ir, context);
            _status = _vm.Status;

            _transcript.AppendSystem($"Loaded quest: {Path.GetFileName(questPath)} ({load.EncodingName})");
        }
        catch (Exception ex)
        {
            _status = VmStatus.Faulted;
            _transcript.AppendSystem($"Failed to load quest: {ex.Message}");
        }
    }

    public RunnerViewModel Advance()
    {
        if (_vm is null)
        {
            return Snapshot();
        }

        if (_vm.Status is VmStatus.WaitingForChoice or VmStatus.Halted or VmStatus.Faulted)
        {
            SyncOutputDelta();
            SyncChoices();
            _status = _vm.Status;
            return Snapshot();
        }

        var run = _vm.RunUntilWaitOrHalt(_config.MaxInstructionsPerAdvance);
        _hitInstructionLimit = run.HitInstructionLimit;
        SyncOutputDelta();
        SyncChoices();
        _status = run.Status;

        return Snapshot();
    }

    public RunnerViewModel SelectButton(int buttonId)
    {
        if (_vm is null)
        {
            return Snapshot();
        }

        if (_vm.Status != VmStatus.WaitingForChoice)
        {
            return Snapshot();
        }

        var button = _choicePresenter.FindById(buttonId) ?? _vm.Buttons.FirstOrDefault(x => x.Id == buttonId);
        if (button is null)
        {
            return Snapshot();
        }

        _transcript.AppendChoiceEcho($"[{button.Caption}]");
        _choicePresenter.Clear();

        var chosen = _vm.ChooseButton(buttonId);
        if (!chosen)
        {
            _status = _vm.Status;
            return Snapshot();
        }

        var run = _vm.RunUntilWaitOrHalt(_config.MaxInstructionsPerAdvance);
        _hitInstructionLimit = run.HitInstructionLimit;
        SyncOutputDelta();
        SyncChoices();
        _status = run.Status;

        return Snapshot();
    }

    public RunnerViewModel Snapshot()
    {
        var allDiagnostics = _diagnostics.ToList();
        if (_vm is not null)
        {
            allDiagnostics.AddRange(_vm.Context.Diagnostics.Items);
        }

        return new RunnerViewModel(
            Transcript: _transcript.Entries.ToList(),
            ActiveChoices: _choicePresenter.ActiveChoices.ToList(),
            Status: _status,
            HitInstructionLimit: _hitInstructionLimit,
            Diagnostics: allDiagnostics);
    }

    private void SyncOutputDelta()
    {
        if (_vm is null)
        {
            return;
        }

        var output = _vm.OutputText;
        if (_consumedOutputLength >= output.Length)
        {
            return;
        }

        var delta = output[_consumedOutputLength..];
        _consumedOutputLength = output.Length;
        _transcript.AppendOutput(delta);
    }

    private void SyncChoices()
    {
        if (_vm is null)
        {
            _choicePresenter.Clear();
            return;
        }

        if (_vm.Status == VmStatus.WaitingForChoice)
        {
            _choicePresenter.SetChoices(_vm.Buttons);
            return;
        }

        _choicePresenter.Clear();
    }
}
