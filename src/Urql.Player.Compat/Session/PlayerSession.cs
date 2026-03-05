using Urql.Core.Diagnostics;
using Urql.Core.IO;
using Urql.Core.Intermediate;
using Urql.Core.Runtime;
using Urql.Core.Syntax;
using Urql.Player.Compat.RichText;
using Urql.Player.Compat.Skin;
using Urql.Player.Compat.Viewport;

namespace Urql.Player.Compat.Session;

public sealed class PlayerSession
{
    private readonly ISkinProvider _skinProvider;
    private readonly IRichTextParser _richTextParser;
    private readonly List<RichRun> _textRuns = [];
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly List<FrameMenu> _menus = [];

    private PlayerSessionConfig _config = new(string.Empty);
    private VirtualMachine? _vm;
    private SkinDefinition _skin = new();
    private int _consumedOutputLength;
    private bool _hitInstructionLimit;
    private VmStatus _status = VmStatus.Halted;

    public PlayerSession(ISkinProvider skinProvider, IRichTextParser richTextParser)
    {
        _skinProvider = skinProvider;
        _richTextParser = richTextParser;
    }

    public void Load(PlayerSessionConfig config)
    {
        _config = config;
        _vm = null;
        _consumedOutputLength = 0;
        _hitInstructionLimit = false;
        _status = VmStatus.Halted;
        _textRuns.Clear();
        _diagnostics.Clear();
        _menus.Clear();

        if (!File.Exists(config.QuestPath))
        {
            _status = VmStatus.Faulted;
            _diagnostics.Add(new Diagnostic(
                DiagnosticCode.UnexpectedCharacter,
                DiagnosticSeverity.Error,
                $"Quest file not found: {config.QuestPath}",
                new SourceSpan(new SourcePosition(1, 1), new SourcePosition(1, 1))));
            _skin = _skinProvider.LoadBuiltInDefault();
            return;
        }

        _skin = ResolveSkin(config);

        var load = UrqlTextLoader.LoadFile(config.QuestPath, new UrqlTextLoadOptions(config.EncodingName));
        var source = load.Text;
        var lex = Lexer.Lex(source);
        var parse = Parser.Parse(source, new ParserOptions(CompatibilityMode.DosUrq, AllowUnknownCommands: !config.StrictUnknownCommands));
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
    }

    public PlayerFrame Advance(int viewWidth, int viewHeight)
    {
        if (_vm is null)
        {
            return Snapshot(viewWidth, viewHeight);
        }

        if (_vm.Status is VmStatus.WaitingForChoice or VmStatus.Halted or VmStatus.Faulted)
        {
            SyncOutput();
            _status = _vm.Status;
            return Snapshot(viewWidth, viewHeight);
        }

        var run = _vm.RunUntilWaitOrHalt(_config.MaxInstructionsPerAdvance);
        _hitInstructionLimit = run.HitInstructionLimit;
        SyncOutput();
        _status = run.Status;
        return Snapshot(viewWidth, viewHeight);
    }

    public PlayerFrame SelectButton(int buttonId, int viewWidth, int viewHeight)
    {
        if (_vm is null || _vm.Status != VmStatus.WaitingForChoice)
        {
            return Snapshot(viewWidth, viewHeight);
        }

        var button = _vm.Buttons.FirstOrDefault(b => b.Id == buttonId);
        if (button is null)
        {
            return Snapshot(viewWidth, viewHeight);
        }

        _textRuns.Add(new TextRun($"\n[{button.Caption}]\n"));
        var ok = _vm.ChooseButton(buttonId);
        if (ok)
        {
            var run = _vm.RunUntilWaitOrHalt(_config.MaxInstructionsPerAdvance);
            _hitInstructionLimit = run.HitInstructionLimit;
            SyncOutput();
            _status = run.Status;
        }
        else
        {
            _status = _vm.Status;
        }

        return Snapshot(viewWidth, viewHeight);
    }

    public PlayerFrame ActivateLink(LinkRun link, int viewWidth, int viewHeight)
    {
        if (_vm is null || _vm.Status is VmStatus.Halted or VmStatus.Faulted)
        {
            return Snapshot(viewWidth, viewHeight);
        }

        if (link.IsMenu)
        {
            _menus.Clear();
            _menus.Add(new FrameMenu(link.Target, [$"Menu '{link.Target}' is not implemented yet."]));
            return Snapshot(viewWidth, viewHeight);
        }

        var command = link.IsLocal ? $"proc {link.Target}" : $"goto {link.Target}";
        _ = _vm.ExecuteDynamicSingleStatement(command);
        var runResult = _vm.RunUntilWaitOrHalt(_config.MaxInstructionsPerAdvance);
        _hitInstructionLimit = runResult.HitInstructionLimit;
        SyncOutput();
        _status = runResult.Status;
        return Snapshot(viewWidth, viewHeight);
    }

    public PlayerFrame Snapshot(int viewWidth, int viewHeight)
    {
        var vw = _skin.ScreenWidth > 0 ? _skin.ScreenWidth : _config.VirtualWidth;
        var vh = _skin.ScreenHeight > 0 ? _skin.ScreenHeight : _config.VirtualHeight;
        var transform = ViewportMapper.ComputeLetterbox(vw, vh, Math.Max(1, viewWidth), Math.Max(1, viewHeight));
        var diagnostics = _diagnostics.ToList();
        if (_vm is not null)
        {
            diagnostics.AddRange(_vm.Context.Diagnostics.Items);
        }

        var buttons = _vm?.Buttons.Select(b => new FrameButton(b.Id, b.Caption, b.Target)).ToList() ?? [];
        return new PlayerFrame(
            VirtualWidth: vw,
            VirtualHeight: vh,
            ViewTransform: transform,
            TextRuns: _textRuns.ToList(),
            Buttons: buttons,
            Menus: _menus.ToList(),
            Diagnostics: diagnostics,
            Status: _status,
            HitInstructionLimit: _hitInstructionLimit,
            Skin: _skin);
    }

    private void SyncOutput()
    {
        if (_vm is null)
        {
            return;
        }

        if (_consumedOutputLength >= _vm.OutputText.Length)
        {
            return;
        }

        var delta = _vm.OutputText[_consumedOutputLength..];
        _consumedOutputLength = _vm.OutputText.Length;
        var parsed = _richTextParser.Parse(delta);
        _textRuns.AddRange(parsed.Runs);
    }

    private SkinDefinition ResolveSkin(PlayerSessionConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.SkinPathOverride) && File.Exists(config.SkinPathOverride))
        {
            return LoadByExtension(config.SkinPathOverride!);
        }

        var questDir = Path.GetDirectoryName(config.QuestPath) ?? string.Empty;
        var legacyPath = Path.Combine(questDir, "skin.xml");
        if (File.Exists(legacyPath))
        {
            return _skinProvider.LoadLegacyXml(legacyPath);
        }

        var jsonPath = Path.Combine(questDir, "skin.json");
        if (File.Exists(jsonPath))
        {
            return _skinProvider.LoadJson(jsonPath);
        }

        return _skinProvider.LoadBuiltInDefault();
    }

    private SkinDefinition LoadByExtension(string path)
    {
        return Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? _skinProvider.LoadJson(path)
            : _skinProvider.LoadLegacyXml(path);
    }
}
