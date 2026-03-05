using NativeFileDialogs.Net;

namespace Urql.Runner.MonoGame.Runtime;

public static class QuestFilePicker
{
    public static string? TryPickQuestFilePath()
    {
        try
        {
            var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Quest files"] = "qst,txt",
                ["All files"] = "*"
            };

            var status = Nfd.OpenDialog(out var outPath, filters, null);
            return status == NfdStatus.Ok && !string.IsNullOrWhiteSpace(outPath) ? outPath : null;
        }
        catch (NfdException)
        {
            return null;
        }
    }
}
