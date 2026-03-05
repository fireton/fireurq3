using Urql.Core.Diagnostics;
using Urql.Core.Runtime;
using Urql.Player.Compat.RichText;
using Urql.Player.Compat.Skin;
using Urql.Player.Compat.Viewport;

namespace Urql.Player.Compat.Session;

public sealed record PlayerFrame(
    int VirtualWidth,
    int VirtualHeight,
    ViewTransform ViewTransform,
    IReadOnlyList<RichRun> TextRuns,
    IReadOnlyList<FrameButton> Buttons,
    IReadOnlyList<FrameMenu> Menus,
    IReadOnlyList<Diagnostic> Diagnostics,
    VmStatus Status,
    bool HitInstructionLimit,
    SkinDefinition Skin);

public sealed record FrameButton(int Id, string Caption, string Target);

public sealed record FrameMenu(string Name, IReadOnlyList<string> Entries);
