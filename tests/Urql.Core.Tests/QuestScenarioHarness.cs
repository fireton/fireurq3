using Urql.Core.Diagnostics;
using Urql.Core.IO;
using Urql.Core.Intermediate;
using Urql.Core.Runtime;
using Urql.Core.Syntax;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Urql.Core.Tests;

internal sealed record ButtonPick(
    string? Caption = null,
    int? Index = null);

internal sealed record QuestCheckpoint(
    VmStatus? Status = null,
    IReadOnlyDictionary<string, double>? NumberVariables = null,
    IReadOnlyDictionary<string, string>? StringVariables = null,
    IReadOnlyDictionary<string, double>? Inventory = null,
    IReadOnlyList<string>? OutputContains = null,
    bool? HasErrorDiagnostics = null);

internal sealed record QuestScenarioStep(
    ButtonPick Pick,
    QuestCheckpoint? Checkpoint = null);

internal sealed record QuestScenario(
    string ScriptPath,
    string EncodingName = "auto",
    int MaxInstructionsPerRun = 10_000,
    QuestCheckpoint? InitialCheckpoint = null,
    IReadOnlyList<QuestScenarioStep>? Steps = null,
    QuestCheckpoint? FinalCheckpoint = null,
    string? Name = null);

internal sealed record QuestScenarioResult(
    VirtualMachine Vm,
    string DetectedEncodingName,
    IReadOnlyList<string> PickedCaptions);

internal static class QuestScenarioHarness
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static QuestScenarioResult Run(QuestScenario scenario)
    {
        var load = UrqlTextLoader.LoadFile(scenario.ScriptPath, new UrqlTextLoadOptions(scenario.EncodingName));
        var parse = Parser.Parse(load.Text, new ParserOptions(CompatibilityMode.DosUrq));
        var ir = Compiler.Compile(parse.Program, parse.Diagnostics);
        var context = new EvalContext
        {
            CharCodeEncodingName = load.EncodingName
        };
        var vm = new VirtualMachine(ir, context);
        var picked = new List<string>();

        _ = vm.RunUntilWaitOrHalt(scenario.MaxInstructionsPerRun);
        AssertCheckpoint(vm, scenario.InitialCheckpoint);

        foreach (var step in scenario.Steps ?? [])
        {
            if (vm.Status != VmStatus.WaitingForChoice)
            {
                throw new Xunit.Sdk.XunitException($"Expected VM status WaitingForChoice before pick, got {vm.Status}.");
            }

            var button = ResolveButton(vm, step.Pick);
            picked.Add(button.Caption);
            var chosen = vm.ChooseButton(button.Id);
            if (!chosen)
            {
                throw new Xunit.Sdk.XunitException($"Failed to choose button '{button.Caption}' ({button.Id}).");
            }

            _ = vm.RunUntilWaitOrHalt(scenario.MaxInstructionsPerRun);
            AssertCheckpoint(vm, step.Checkpoint);
        }

        AssertCheckpoint(vm, scenario.FinalCheckpoint);
        return new QuestScenarioResult(vm, load.EncodingName, picked);
    }

    public static IReadOnlyList<QuestScenario> LoadScenariosFromJson(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var root = JsonSerializer.Deserialize<QuestScenarioFileDto>(json, JsonOptions)
                   ?? throw new InvalidOperationException($"Failed to parse scenario file: {jsonPath}");

        var scenarios = new List<QuestScenario>();
        foreach (var dto in root.Scenarios ?? [])
        {
            if (string.IsNullOrWhiteSpace(dto.ScriptPath))
            {
                throw new InvalidOperationException($"Scenario '{dto.Name}' has empty scriptPath.");
            }

            scenarios.Add(new QuestScenario(
                ScriptPath: TestPaths.ResolveFromRepo(dto.ScriptPath),
                EncodingName: string.IsNullOrWhiteSpace(dto.EncodingName) ? "auto" : dto.EncodingName!,
                MaxInstructionsPerRun: dto.MaxInstructionsPerRun <= 0 ? 10_000 : dto.MaxInstructionsPerRun,
                InitialCheckpoint: dto.InitialCheckpoint?.ToCheckpoint(),
                Steps: dto.Steps?.Select(s => s.ToStep()).ToList() ?? [],
                FinalCheckpoint: dto.FinalCheckpoint?.ToCheckpoint(),
                Name: dto.Name));
        }

        return scenarios;
    }

    private static ButtonAction ResolveButton(VirtualMachine vm, ButtonPick pick)
    {
        if (pick.Index is { } idx)
        {
            if (idx < 0 || idx >= vm.Buttons.Count)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Button index {idx} is out of range. Buttons count: {vm.Buttons.Count}.");
            }

            return vm.Buttons[idx];
        }

        if (!string.IsNullOrWhiteSpace(pick.Caption))
        {
            var byCaption = vm.Buttons.FirstOrDefault(b =>
                string.Equals(b.Caption, pick.Caption, StringComparison.OrdinalIgnoreCase));
            if (byCaption is not null)
            {
                return byCaption;
            }

            // Keep scenario fixtures resilient when quests use decorative trailing dots in captions.
            var normalizedPick = NormalizeCaptionForMatch(pick.Caption);
            byCaption = vm.Buttons.FirstOrDefault(b =>
                string.Equals(NormalizeCaptionForMatch(b.Caption), normalizedPick, StringComparison.OrdinalIgnoreCase));
            if (byCaption is not null)
            {
                return byCaption;
            }

            throw new Xunit.Sdk.XunitException(
                $"Button with caption '{pick.Caption}' was not found. Available: {string.Join(", ", vm.Buttons.Select(b => b.Caption))}.");
        }

        throw new Xunit.Sdk.XunitException("Button pick must define either Caption or Index.");
    }

    private static void AssertCheckpoint(VirtualMachine vm, QuestCheckpoint? checkpoint)
    {
        if (checkpoint is null)
        {
            return;
        }

        if (checkpoint.Status is { } status)
        {
            Assert.Equal(status, vm.Status);
        }

        foreach (var pair in checkpoint.NumberVariables ?? new Dictionary<string, double>())
        {
            Assert.True(vm.Context.Variables.TryGet(pair.Key, out var value), $"Expected variable '{pair.Key}' to exist.");
            Assert.Equal(pair.Value, value.NumberValue, 6);
        }

        foreach (var pair in checkpoint.StringVariables ?? new Dictionary<string, string>())
        {
            Assert.True(vm.Context.Variables.TryGet(pair.Key, out var value), $"Expected variable '{pair.Key}' to exist.");
            Assert.Equal(pair.Value, value.StringValue);
        }

        foreach (var pair in checkpoint.Inventory ?? new Dictionary<string, double>())
        {
            Assert.Equal(pair.Value, vm.Context.Inventory.GetCount(pair.Key), 6);
        }

        foreach (var fragment in checkpoint.OutputContains ?? Array.Empty<string>())
        {
            Assert.Contains(fragment, vm.OutputText);
        }

        if (checkpoint.HasErrorDiagnostics is { } hasErrors)
        {
            var actualHasErrors = vm.Context.Diagnostics.Items.Any(d => d.Severity == DiagnosticSeverity.Error);
            Assert.Equal(hasErrors, actualHasErrors);
        }
    }

    private static string NormalizeCaptionForMatch(string caption)
    {
        return caption.Trim().TrimEnd('.');
    }
}

internal sealed class QuestScenarioFileDto
{
    public List<QuestScenarioDto>? Scenarios { get; set; }
}

internal sealed class QuestScenarioDto
{
    public string? Name { get; set; }
    public string? ScriptPath { get; set; }
    public string? EncodingName { get; set; }
    public int MaxInstructionsPerRun { get; set; } = 10_000;
    public QuestCheckpointDto? InitialCheckpoint { get; set; }
    public List<QuestScenarioStepDto>? Steps { get; set; }
    public QuestCheckpointDto? FinalCheckpoint { get; set; }
}

internal sealed class QuestScenarioStepDto
{
    public ButtonPickDto? Pick { get; set; }
    public QuestCheckpointDto? Checkpoint { get; set; }

    public QuestScenarioStep ToStep()
    {
        return new QuestScenarioStep(
            Pick: Pick?.ToPick() ?? throw new InvalidOperationException("Scenario step is missing 'pick'."),
            Checkpoint: Checkpoint?.ToCheckpoint());
    }
}

internal sealed class ButtonPickDto
{
    public string? Caption { get; set; }
    public int? Index { get; set; }

    public ButtonPick ToPick() => new(Caption, Index);
}

internal sealed class QuestCheckpointDto
{
    public VmStatus? Status { get; set; }
    public Dictionary<string, double>? NumberVariables { get; set; }
    public Dictionary<string, string>? StringVariables { get; set; }
    public Dictionary<string, double>? Inventory { get; set; }
    public List<string>? OutputContains { get; set; }
    public bool? HasErrorDiagnostics { get; set; }

    public QuestCheckpoint ToCheckpoint()
    {
        return new QuestCheckpoint(
            Status,
            NumberVariables,
            StringVariables,
            Inventory,
            OutputContains,
            HasErrorDiagnostics);
    }
}
