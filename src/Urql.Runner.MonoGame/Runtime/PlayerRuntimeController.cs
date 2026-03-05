using Urql.Player.Compat;
using Urql.Player.Compat.RichText;
using Urql.Player.Compat.Session;
using Urql.Player.Compat.Skin;

namespace Urql.Runner.MonoGame.Runtime;

public sealed class PlayerRuntimeController
{
    private PlayerSession? _session;

    public PlayerFrame? Frame { get; private set; }
    public string? FatalMessage { get; private set; }

    public bool IsReady => FatalMessage is null && _session is not null && Frame is not null;

    public void Fail(string message)
    {
        FatalMessage = message;
        _session = null;
        Frame = null;
    }

    public void Load(string? questPath, int viewportWidth, int viewportHeight)
    {
        FatalMessage = null;
        Frame = null;
        _session = null;

        if (string.IsNullOrWhiteSpace(questPath))
        {
            FatalMessage = "Quest path was not provided.";
            return;
        }

        var defaultSkinPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Skins", "default", "skin.xml");
        var skinProvider = new SkinProvider(defaultSkinPath);
        _session = new PlayerSession(skinProvider, new LegacyRichTextParser());
        _session.Load(new PlayerSessionConfig(questPath, CompatibilityProfile: CompatibilityProfile.FireUrqLegacy));
        Frame = _session.Advance(viewportWidth, viewportHeight);
    }

    public void RefreshFrame(int viewportWidth, int viewportHeight)
    {
        if (_session is null)
        {
            return;
        }

        Frame = _session.Snapshot(viewportWidth, viewportHeight);
    }

    public void SelectButton(int buttonId, int viewportWidth, int viewportHeight)
    {
        if (_session is null)
        {
            return;
        }

        Frame = _session.SelectButton(buttonId, viewportWidth, viewportHeight);
    }

    public void ActivateLink(LinkRun link, int viewportWidth, int viewportHeight)
    {
        if (_session is null)
        {
            return;
        }

        Frame = _session.ActivateLink(link, viewportWidth, viewportHeight);
    }
}
