using System.Diagnostics;
using System.Globalization;

namespace Urql.Runner.MonoGame.Runtime;

public static class QuestFilePicker
{
    public static string? TryPickQuestFilePath()
    {
        if (OperatingSystem.IsMacOS())
        {
            return RunMacPickerProcess();
        }

        if (OperatingSystem.IsWindows())
        {
            return RunPickerProcess(
                "powershell",
                "-NoProfile -STA -Command \"Add-Type -AssemblyName System.Windows.Forms; $dlg = New-Object System.Windows.Forms.OpenFileDialog; $dlg.Filter = 'Quest files (*.qst;*.txt)|*.qst;*.txt|All files (*.*)|*.*'; if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { $dlg.FileName }\"");
        }

        if (OperatingSystem.IsLinux())
        {
            return RunPickerProcess(
                "zenity",
                "--file-selection --title=\"Select URQL quest file\" --file-filter=\"Quest files | *.qst *.txt\" --file-filter=\"All files | *\"");
        }

        return null;
    }

    private static string? RunMacPickerProcess()
    {
        var swiftPath = RunPickerProcess("swift", "-e \"" + BuildMacOpenPanelSwiftScript() + "\"", macParentPid: true);
        if (!string.IsNullOrWhiteSpace(swiftPath))
        {
            return swiftPath;
        }

        // Fallback path for environments where swift/AppKit execution is unavailable.
        return RunPickerProcess(
            "osascript",
            "-e \"POSIX path of (choose file with prompt \\\"Select URQL quest file\\\" of type {\\\"qst\\\",\\\"txt\\\"})\"");
    }

    private static string? RunPickerProcess(string fileName, string arguments, bool macParentPid = false)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (macParentPid)
            {
                startInfo.Environment["FIREURQ_PARENT_PID"] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                return null;
            }

            var path = output.Trim();
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildMacOpenPanelSwiftScript()
    {
        // Use NSOpenPanel directly to avoid Apple Events/System Events permission prompts.
        var lines = new[]
        {
            "import AppKit",
            "let app = NSApplication.shared",
            "app.activate(ignoringOtherApps: true)",
            "let panel = NSOpenPanel()",
            "panel.canChooseFiles = true",
            "panel.canChooseDirectories = false",
            "panel.allowsMultipleSelection = false",
            "panel.prompt = \\\"Open\\\"",
            "panel.message = \\\"Select URQL quest file\\\"",
            "panel.allowedFileTypes = [\\\"qst\\\", \\\"txt\\\"]",
            "let result = panel.runModal()",
            "if result == .OK, let url = panel.url {",
            "    print(url.path)",
            "}",
            "if let pidText = ProcessInfo.processInfo.environment[\\\"FIREURQ_PARENT_PID\\\"], let pid = Int32(pidText), let parent = NSRunningApplication(processIdentifier: pid_t(pid)) {",
            "    parent.activate(options: [.activateIgnoringOtherApps])",
            "}"
        };

        return string.Join(';', lines);
    }
}
