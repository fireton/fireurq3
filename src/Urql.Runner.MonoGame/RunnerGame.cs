using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Urql.Player.Compat.RichText;
using Urql.Runner.MonoGame.AssetsSupport;
using Urql.Runner.MonoGame.Input;
using Urql.Runner.MonoGame.Layout;
using Urql.Runner.MonoGame.Rendering;
using Urql.Runner.MonoGame.Runtime;

namespace Urql.Runner.MonoGame;

public sealed class RunnerGame : Game
{
    private const float Padding = 16f;
    private const float LineSpacing = 4f;

    private readonly GraphicsDeviceManager _graphics;
    private readonly string _questPath;
    private readonly PlayerRuntimeController _runtime = new();

    private SpriteBatch? _spriteBatch;
    private RunnerRenderer? _renderer;
    private FontSystem? _fontSystem;
    private DynamicSpriteFont? _font;
    private Texture2D? _pixel;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private int _selectedChoiceIndex;
    private List<RenderLine> _lines = [];
    private List<ChoiceHitArea> _choiceHitAreas = [];
    private List<LinkHitArea> _linkHitAreas = [];

    public RunnerGame(string questPath)
    {
        _questPath = questPath;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "FireURQ3 Official Player";
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        try
        {
            _fontSystem = new FontSystem();
            _fontSystem.AddFont(File.ReadAllBytes(FontManager.ResolveFontPath()));
            _font = _fontSystem.GetFont(24);
            _renderer = new RunnerRenderer(_font, _pixel);
        }
        catch (Exception ex)
        {
            _runtime.Fail($"Failed to load runtime font: {ex.Message}");
            return;
        }

        _runtime.Load(_questPath, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        if (_runtime.Frame is not null)
        {
            _graphics.PreferredBackBufferWidth = _runtime.Frame.VirtualWidth;
            _graphics.PreferredBackBufferHeight = _runtime.Frame.VirtualHeight;
            _graphics.ApplyChanges();
            _runtime.RefreshFrame(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            BringWindowToFront();
        }

        ClampSelectedChoice();
        RebuildLayoutAndHitAreas();
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

        if (_runtime.IsReady && _runtime.Frame is not null)
        {
            _runtime.RefreshFrame(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            ClampSelectedChoice();
            RebuildLayoutAndHitAreas();

            var viewMouseX = ViewportInputMapper.GetMouseViewX(Window, GraphicsDevice, mouse.X);
            var viewMouseY = ViewportInputMapper.GetMouseViewY(Window, GraphicsDevice, mouse.Y);
            if (_runtime.Frame.ViewTransform.TryMapToVirtual(viewMouseX, viewMouseY, out var hoverVx, out var hoverVy))
            {
                var hoveredButtonId = _choiceHitAreas.FirstOrDefault(h => h.Bounds.Contains(hoverVx, hoverVy)).ButtonId;
                if (hoveredButtonId != 0)
                {
                    var hoveredIndex = _runtime.Frame.Buttons.ToList().FindIndex(b => b.Id == hoveredButtonId);
                    if (hoveredIndex >= 0)
                    {
                        _selectedChoiceIndex = hoveredIndex;
                    }
                }
            }

            if (_runtime.Frame.Buttons.Count > 0)
            {
                if (IsNewKeyPress(keyboard, Keys.Up))
                {
                    _selectedChoiceIndex = (_selectedChoiceIndex - 1 + _runtime.Frame.Buttons.Count) % _runtime.Frame.Buttons.Count;
                }

                if (IsNewKeyPress(keyboard, Keys.Down))
                {
                    _selectedChoiceIndex = (_selectedChoiceIndex + 1) % _runtime.Frame.Buttons.Count;
                }

                if (IsNewKeyPress(keyboard, Keys.Enter))
                {
                    var selected = _runtime.Frame.Buttons[_selectedChoiceIndex];
                    _runtime.SelectButton(selected.Id, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                    ClampSelectedChoice();
                    RebuildLayoutAndHitAreas();
                }
            }

            var clicked = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
            if (clicked && _runtime.Frame.ViewTransform.TryMapToVirtual(viewMouseX, viewMouseY, out var vx, out var vy))
            {
                var linkHit = _linkHitAreas.FirstOrDefault(h => h.Bounds.Contains(vx, vy));
                if (linkHit.Link is not null)
                {
                    _runtime.ActivateLink(linkHit.Link, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                    ClampSelectedChoice();
                    RebuildLayoutAndHitAreas();
                }
                else
                {
                    var choiceHit = _choiceHitAreas.FirstOrDefault(h => h.Bounds.Contains(vx, vy));
                    if (choiceHit.ButtonId != 0)
                    {
                        _runtime.SelectButton(choiceHit.ButtonId, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                        ClampSelectedChoice();
                        RebuildLayoutAndHitAreas();
                    }
                }
            }
        }

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(7, 10, 16));

        if (_spriteBatch is null || _renderer is null)
        {
            base.Draw(gameTime);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_runtime.FatalMessage))
        {
            _renderer.DrawFatal(_spriteBatch, _runtime.FatalMessage, Padding);
            base.Draw(gameTime);
            return;
        }

        if (_runtime.Frame is not null)
        {
            _renderer.DrawVirtualFrame(_spriteBatch, _runtime.Frame, _lines, Padding, LineSpacing);
        }

        base.Draw(gameTime);
    }

    private void RebuildLayoutAndHitAreas()
    {
        if (_runtime.Frame is null || _font is null)
        {
            _lines = [];
            _choiceHitAreas = [];
            _linkHitAreas = [];
            return;
        }

        _lines = TranscriptLayoutEngine.BuildRenderLines(_runtime.Frame, _font, _selectedChoiceIndex, Padding);
        var hitMaps = InteractionHitMapBuilder.Build(_runtime.Frame, _font, _lines, Padding, LineSpacing);
        _choiceHitAreas = hitMaps.choices;
        _linkHitAreas = hitMaps.links;
    }

    private void ClampSelectedChoice()
    {
        if (_runtime.Frame is null || _runtime.Frame.Buttons.Count == 0)
        {
            _selectedChoiceIndex = 0;
            return;
        }

        _selectedChoiceIndex = Math.Clamp(_selectedChoiceIndex, 0, _runtime.Frame.Buttons.Count - 1);
    }

    private bool IsNewKeyPress(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private void BringWindowToFront()
    {
        // Center window to reduce chance of opening off-screen/workspace after native file dialog.
        var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var x = Math.Max(0, (mode.Width - Window.ClientBounds.Width) / 2);
        var y = Math.Max(0, (mode.Height - Window.ClientBounds.Height) / 2);
        Window.Position = new Point(x, y);
    }
}
