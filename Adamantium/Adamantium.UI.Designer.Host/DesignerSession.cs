using System.Reflection;
using Adamantium.Core;
using Adamantium.Game;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Markup;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Extensions;
using Adamantium.UI.Rendering;

namespace Adamantium.UI.Designer.Host;

/// <summary>
/// A warm headless engine session. Its constructor boots the engine once - loads every engine assembly (so
/// reflection type resolution sees all controls), creates the graphics device and applies a theme - then
/// <see cref="Render"/> turns an AUML buffer into a PNG on demand. The device and the <see cref="OffscreenRenderer"/>
/// stay alive between calls so live edits are cheap; the renderer is only rebuilt when the requested size changes.
/// </summary>
public sealed class DesignerSession : IDisposable
{
    private readonly DesignerApplication _app;
    private readonly IGraphicsDevice _device;
    private readonly RenderUnitFactory _factory;

    private OffscreenRenderer _renderer;
    private uint _rendererWidth;
    private uint _rendererHeight;

    public DesignerSession()
    {
        // Load every engine assembly so reflection type resolution can see all control types.
        foreach (var dll in Directory.GetFiles(AppContext.BaseDirectory, "Adamantium*.dll"))
        {
            try { Assembly.LoadFrom(dll); } catch { /* ignore unloadable */ }
        }

        _app = new DesignerApplication();

        // Headless: nothing opens a window, so trigger device creation explicitly.
        _app.Container.Resolve<IGraphicsDeviceService>().CreateMainDevice("Designer");

        // Same as UIApplication.LoadThemes() (skipped because we never call Run()): without a theme the controls
        // have no templates/brushes and render nothing.
        var theme = new Adamantium.UI.Themes.FluentDarkTheme.FluentDark();
        _app.ThemeManager.AddTheme(theme.Name, theme);
        _app.ThemeManager.SetTheme(theme);

        _device = _app.GraphicsContext.CreateGraphicsDevice();
        _factory = new RenderUnitFactory(_device, _app.GraphicsContext.GetResourceFactory());
    }

    /// <summary>Loads the AUML text into a live tree, lays it out and renders it to <paramref name="outPath"/>.</summary>
    public RenderResult Render(string aumlText, uint width, uint height, string outPath)
    {
        var load = AumlLoader.Load(
            aumlText,
            AppDomain.CurrentDomain.GetAssemblies(),
            t => typeof(IWindow).IsAssignableFrom(t) ? typeof(VirtualWindow) : t);

        if (load.Root is not IWindow window)
            return RenderResult.Fail($"root is not a window: {load.Root?.GetType().Name ?? "null"}", load.Diagnostics);

        window.AttachContextAndInitialize(_app.UIContext);
        window.ClientWidth = width;
        window.ClientHeight = height;

        // Layout (Measure/Arrange + theme) - geometry only, no native window.
        window.Update(_app.ThemeManager, new AppTime());

        var renderer = GetRenderer(width, height);
        if (!renderer.RenderFrame((IRootVisualComponent)window))
            return RenderResult.Fail("render failed", load.Diagnostics);

        renderer.Save(outPath, ImageFileType.Png);
        return RenderResult.Ok(outPath, load.Diagnostics);
    }

    private OffscreenRenderer GetRenderer(uint width, uint height)
    {
        if (_renderer != null && _rendererWidth == width && _rendererHeight == height)
            return _renderer;

        _renderer?.Dispose();
        _renderer = new OffscreenRenderer(_device, _factory, width, height) { ClearColor = Colors.White };
        _rendererWidth = width;
        _rendererHeight = height;
        return _renderer;
    }

    public void Dispose() => _renderer?.Dispose();
}
