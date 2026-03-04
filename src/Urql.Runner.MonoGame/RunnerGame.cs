using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using Urql.Core.Runtime;
using Urql.Runner.MonoGame.Runner;

namespace Urql.Runner.MonoGame;

public sealed class RunnerGame : Game
{
    private const float Padding = 16f;
    private const float LineSpacing = 4f;

    private readonly GraphicsDeviceManager _graphics;
    private readonly string[] _args;
    private readonly QuestSession _session = new();
    private readonly List<ChoiceHitArea> _choiceHitAreas = [];

    private SpriteBatch? _spriteBatch;
    private FontSystem? _fontSystem;
    private DynamicSpriteFont? _font;
    private Texture2D? _pixel;
    private RunnerViewModel _viewModel = new([], [], VmStatus.Halted, false, []);
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private int _selectedChoiceIndex;
    private string? _fatalMessage;

    public RunnerGame(string[] args)
    {
        _args = args;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };
        IsMouseVisible = true;
        Window.Title = "FireURQ3 MonoGame Runner";
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        try
        {
            _fontSystem = new FontSystem();
            var fontPath = ResolveFontPath();
            _fontSystem.AddFont(File.ReadAllBytes(fontPath));
            _font = _fontSystem.GetFont(24);
        }
        catch (Exception ex)
        {
            _fatalMessage = $"Failed to load runtime font: {ex.Message}";
            return;
        }

        var questPath = _args.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(questPath))
        {
            questPath = TryPickQuestFilePath();
        }

        if (string.IsNullOrWhiteSpace(questPath))
        {
            _fatalMessage = "Quest path was not provided and no file was selected.";
            return;
        }

        _session.Load(questPath, new QuestRunnerConfig());
        _viewModel = _session.Advance();
        ClampSelectedChoice();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        if (IsNewKeyPress(keyboard, Keys.Escape))
        {
            Exit();
            return;
        }

        if (_fatalMessage is null && _viewModel.ActiveChoices.Count > 0)
        {
            if (IsNewKeyPress(keyboard, Keys.Up))
            {
                _selectedChoiceIndex = (_selectedChoiceIndex - 1 + _viewModel.ActiveChoices.Count) % _viewModel.ActiveChoices.Count;
            }

            if (IsNewKeyPress(keyboard, Keys.Down))
            {
                _selectedChoiceIndex = (_selectedChoiceIndex + 1) % _viewModel.ActiveChoices.Count;
            }

            var hovered = _choiceHitAreas.FirstOrDefault(h => h.Bounds.Contains(mouse.Position));
            if (hovered.ButtonId != 0)
            {
                _selectedChoiceIndex = hovered.ChoiceIndex;
            }

            if (IsNewKeyPress(keyboard, Keys.Enter))
            {
                SelectCurrentChoice();
            }
            else if (hovered.ButtonId != 0 &&
                     mouse.LeftButton == ButtonState.Pressed &&
                     _previousMouse.LeftButton == ButtonState.Released)
            {
                _viewModel = _session.SelectButton(hovered.ButtonId);
                ClampSelectedChoice();
            }
        }

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(9, 12, 18));

        if (_spriteBatch is null || _font is null)
        {
            base.Draw(gameTime);
            return;
        }

        _spriteBatch.Begin();

        if (!string.IsNullOrWhiteSpace(_fatalMessage))
        {
            _spriteBatch.DrawString(_font, _fatalMessage, new Vector2(Padding, Padding), Color.OrangeRed);
        }
        else
        {
            DrawTranscriptAndChoices();
            DrawOverlay();
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void DrawTranscriptAndChoices()
    {
        if (_spriteBatch is null || _font is null)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        var maxWidth = viewport.Width - (Padding * 2f);
        var lineHeight = _font.LineHeight + LineSpacing;

        var lines = BuildRenderLines(maxWidth);
        var contentHeight = lines.Count * lineHeight + (Padding * 2f);
        var scroll = MathF.Max(0f, contentHeight - viewport.Height);

        _choiceHitAreas.Clear();

        var y = Padding - scroll;
        foreach (var line in lines)
        {
            if (y + lineHeight >= 0 && y <= viewport.Height)
            {
                _spriteBatch.DrawString(_font, line.Text, new Vector2(Padding, y), line.Color);

                if (line.IsChoice)
                {
                    var size = _font.MeasureString(line.Text);
                    _choiceHitAreas.Add(new ChoiceHitArea(
                        line.ButtonId,
                        line.ChoiceIndex,
                        new Rectangle((int)Padding, (int)y, Math.Max(1, (int)Math.Ceiling(size.X)), Math.Max(1, (int)Math.Ceiling(lineHeight)))));
                }
            }

            y += lineHeight;
        }
    }

    private void DrawOverlay()
    {
        if (_spriteBatch is null || _font is null || _pixel is null)
        {
            return;
        }

        var overlay = $"Status: {_viewModel.Status}  Choices: {_viewModel.ActiveChoices.Count}  Diagnostics: {_viewModel.Diagnostics.Count}";
        if (_viewModel.HitInstructionLimit)
        {
            overlay += "  LIMIT";
        }

        var size = _font.MeasureString(overlay);
        var x = GraphicsDevice.Viewport.Width - size.X - Padding;
        var y = Padding;

        _spriteBatch.Draw(_pixel, new Rectangle((int)(x - 8), (int)(y - 4), (int)(size.X + 16), (int)(size.Y + 8)), new Color(0, 0, 0, 150));
        _spriteBatch.DrawString(_font, overlay, new Vector2(x, y), Color.LightGray);
    }

    private List<RenderLine> BuildRenderLines(float maxWidth)
    {
        var lines = new List<RenderLine>();

        foreach (var entry in _viewModel.Transcript)
        {
            var color = entry.Kind switch
            {
                TranscriptEntryKind.Output => new Color(226, 232, 240),
                TranscriptEntryKind.ChoiceEcho => new Color(180, 220, 120),
                TranscriptEntryKind.System => new Color(255, 179, 102),
                _ => Color.White
            };

            foreach (var wrapped in WrapText(entry.Text, maxWidth))
            {
                lines.Add(new RenderLine(wrapped, color, false, 0, -1));
            }
        }

        if (_viewModel.ActiveChoices.Count > 0)
        {
            lines.Add(new RenderLine(string.Empty, Color.White, false, 0, -1));

            for (var i = 0; i < _viewModel.ActiveChoices.Count; i++)
            {
                var button = _viewModel.ActiveChoices[i];
                var isSelected = i == _selectedChoiceIndex;
                var prefix = isSelected ? "> " : "  ";
                var color = isSelected ? new Color(255, 221, 89) : new Color(147, 197, 114);
                var text = $"{prefix}{button.Caption}";
                lines.Add(new RenderLine(text, color, true, button.Id, i));
            }
        }

        return lines;
    }

    private IEnumerable<string> WrapText(string text, float maxWidth)
    {
        if (_font is null)
        {
            yield break;
        }

        var normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var paragraphs = normalized.Split('\n');

        for (var p = 0; p < paragraphs.Length; p++)
        {
            var paragraph = paragraphs[p];
            if (paragraph.Length == 0)
            {
                yield return string.Empty;
            }
            else
            {
                var remaining = paragraph;
                while (remaining.Length > 0)
                {
                    var take = FindLineLengthThatFits(remaining, maxWidth);
                    var line = remaining[..take].TrimEnd();
                    yield return line;
                    remaining = remaining[take..].TrimStart();
                }
            }

            if (p < paragraphs.Length - 1)
            {
                yield return string.Empty;
            }
        }
    }

    private int FindLineLengthThatFits(string text, float maxWidth)
    {
        if (_font is null)
        {
            return text.Length;
        }

        if (_font.MeasureString(text).X <= maxWidth)
        {
            return text.Length;
        }

        var low = 1;
        var high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var slice = text[..mid];
            if (_font.MeasureString(slice).X <= maxWidth)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        var length = Math.Max(1, low);
        if (length < text.Length)
        {
            var lastSpace = text[..length].LastIndexOf(' ');
            if (lastSpace > 0)
            {
                return lastSpace;
            }
        }

        return length;
    }

    private void SelectCurrentChoice()
    {
        if (_selectedChoiceIndex < 0 || _selectedChoiceIndex >= _viewModel.ActiveChoices.Count)
        {
            return;
        }

        var buttonId = _viewModel.ActiveChoices[_selectedChoiceIndex].Id;
        _viewModel = _session.SelectButton(buttonId);
        ClampSelectedChoice();
    }

    private void ClampSelectedChoice()
    {
        if (_viewModel.ActiveChoices.Count == 0)
        {
            _selectedChoiceIndex = 0;
            return;
        }

        _selectedChoiceIndex = Math.Clamp(_selectedChoiceIndex, 0, _viewModel.ActiveChoices.Count - 1);
    }

    private bool IsNewKeyPress(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private static string ResolveFontPath()
    {
        var candidates = new List<string>();
        var bundledDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts");
        candidates.Add(Path.Combine(bundledDir, "NotoSans-Regular.ttf"));
        candidates.Add(Path.Combine(bundledDir, "NotoSans-Bold.ttf"));
        candidates.Add(Path.Combine(bundledDir, "NotoSansMono-Regular.ttf"));

        if (OperatingSystem.IsWindows())
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            candidates.Add(Path.Combine(windows, "Fonts", "arial.ttf"));
            candidates.Add(Path.Combine(windows, "Fonts", "segoeui.ttf"));
            candidates.Add(Path.Combine(windows, "Fonts", "calibri.ttf"));
        }

        if (OperatingSystem.IsMacOS())
        {
            candidates.Add("/System/Library/Fonts/Supplemental/Arial.ttf");
            candidates.Add("/System/Library/Fonts/Supplemental/Arial Unicode.ttf");
            candidates.Add("/System/Library/Fonts/Supplemental/Times New Roman.ttf");
        }

        if (OperatingSystem.IsLinux())
        {
            candidates.Add("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf");
            candidates.Add("/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf");
            candidates.Add("/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf");
        }

        var resolved = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        throw new FileNotFoundException("No usable TTF font was found on this system.");
    }

    private static string? TryPickQuestFilePath()
    {
        if (OperatingSystem.IsMacOS())
        {
            return RunPickerProcess(
                "osascript",
                "-e \"POSIX path of (choose file with prompt \\\"Select URQL quest file\\\" of type {\\\"qst\\\",\\\"txt\\\"})\"");
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

    private static string? RunPickerProcess(string fileName, string arguments)
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

    private sealed record RenderLine(string Text, Color Color, bool IsChoice, int ButtonId, int ChoiceIndex);

    private readonly record struct ChoiceHitArea(int ButtonId, int ChoiceIndex, Rectangle Bounds);
}
