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

    private const double DefaultWidth = 1280;
    private const double DefaultHeight = 720;

    // Cap the render target so an extreme zoom can't exhaust GPU memory (a render target is design x scale, and
    // memory grows with its area; 8192^2 BGRA is ~256 MB before the depth buffer). Past the cap the client
    // upscales the last crisp frame - like a WPF/Avalonia designer that stops re-rasterising beyond a point.
    // 8192 is plenty of crispness for a preview; the effective cap is clamped to the device's
    // maxImageDimension2D (guaranteed >= 4096) so it never asks for an image the GPU can't create.
    private const double PreferredMaxRenderDimension = 8192;
    private readonly double _maxRenderDimension;

    public DesignerSession()
    {
        // Tell design-unsafe code (game-hosting behaviors etc.) it is running in the previewer, so it stays dormant.
        Design.IsDesignMode = true;

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

        _maxRenderDimension = Math.Min(
            PreferredMaxRenderDimension,
            _device.Adapter.AdapterProperties.Limits.MaxImageDimension2D);
    }

    /// <summary>
    /// Loads the AUML text into a live tree, lays it out at the window's design size and renders it to
    /// <paramref name="outPath"/> at design size × <paramref name="scale"/>. The window is always laid out at its
    /// design size (declared Width/Height, else <paramref name="requestWidth"/>/<paramref name="requestHeight"/>,
    /// else a default), and only the render target is scaled - so zooming re-rasterises the same layout crisply
    /// rather than reflowing it.
    /// </summary>
    public RenderResult Render(string aumlText, uint? requestWidth, uint? requestHeight, double scale, string outPath, string? aumlSourcePath = null)
    {
        // Relative asset paths (e.g. <Image Source="Textures/foo.tga">) are loaded against the process working
        // directory, exactly as in the running app (which runs from its output dir). Point the CWD at the edited
        // file's project root so the live designer loads those assets straight from the project source.
        var assetRoot = ResolveAssetRoot(aumlSourcePath);
        if (assetRoot != null) Directory.SetCurrentDirectory(assetRoot);

        // Load the edited file's own project assembly so its types (clr-namespace: controls, behaviors) resolve,
        // not just engine types. Best-effort and idempotent (LoadFrom caches by path).
        LoadProjectAssembly(aumlSourcePath);

        var load = AumlLoader.Load(
            aumlText,
            AppDomain.CurrentDomain.GetAssemblies(),
            t => typeof(IWindow).IsAssignableFrom(t) ? typeof(VirtualWindow) : t);

        // The root may be a Window, or any visual control (a View / UserControl-style root, a panel, a single
        // control). Non-window roots are hosted in a design-time VirtualWindow so the designer previews them too,
        // the way WPF previews a UserControl. Non-visual roots (ResourceDictionary/StyleSet) aren't previewable.
        IWindow window;
        IMeasurableComponent sizeSource;
        switch (load.Root)
        {
            case IWindow w:
                window = w;
                sizeSource = w as IMeasurableComponent;
                break;
            case IUIComponent control:
                window = new VirtualWindow { Content = control };
                sizeSource = control as IMeasurableComponent;
                break;
            default:
                return RenderResult.Fail($"root is not a previewable visual: {load.Root?.GetType().Name ?? "null"}", load.Diagnostics);
        }

        window.AttachContextAndInitialize(_app.UIContext);

        var designWidth = ResolveDimension(sizeSource?.Width, requestWidth, DefaultWidth);
        var designHeight = ResolveDimension(sizeSource?.Height, requestHeight, DefaultHeight);

        window.ClientWidth = designWidth;
        window.ClientHeight = designHeight;

        // Layout (Measure/Arrange + theme) at the design size - geometry only, no native window.
        window.Update(_app.ThemeManager, new AppTime());

        // A hosted control with no declared size: shrink the design canvas to its natural (content) size so the
        // preview fits the control instead of a full default window. (Windows and explicitly-sized controls keep
        // their size.)
        if (load.Root is not IWindow && sizeSource is { } s && double.IsNaN(s.Width) && double.IsNaN(s.Height))
        {
            var desired = s.DesiredSize;
            if (desired.Width >= 1 && desired.Height >= 1 && (desired.Width < designWidth || desired.Height < designHeight))
            {
                designWidth = Math.Min(designWidth, desired.Width);
                designHeight = Math.Min(designHeight, desired.Height);
                window.ClientWidth = designWidth;
                window.ClientHeight = designHeight;
                window.Update(_app.ThemeManager, new AppTime());
            }
        }

        if (scale <= 0) scale = 1.0;
        var maxScale = Math.Max(1.0, Math.Min(_maxRenderDimension / designWidth, _maxRenderDimension / designHeight));
        var renderScale = Math.Min(scale, maxScale);
        var targetWidth = (uint)Math.Max(1.0, Math.Round(designWidth * renderScale));
        var targetHeight = (uint)Math.Max(1.0, Math.Round(designHeight * renderScale));

        var renderer = GetRenderer(targetWidth, targetHeight);
        // Each render is a fresh tree, so free the previous render's units now (the last frame left the GPU idle).
        renderer.ResetCache();
        if (!renderer.RenderFrame((IRootVisualComponent)window))
            return RenderResult.Fail("render failed", load.Diagnostics);

        renderer.Save(outPath, ImageFileType.Png);

        return RenderResult.Ok(outPath, load.Diagnostics, targetWidth, targetHeight, renderScale);
    }

    /// <summary>
    /// The directory relative asset paths resolve against for the file being previewed: the nearest .csproj ancestor
    /// (the project root, matching how the app finds assets relative to its output root), else the file's own folder.
    /// Accepts a plain path or a file:// URI; null/blank yields null (CWD left unchanged).
    /// </summary>
    private static string? ResolveAssetRoot(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return null;

        string path;
        try { path = sourcePath.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? new Uri(sourcePath).LocalPath : sourcePath; }
        catch { path = sourcePath; }

        string? dir;
        try { dir = Path.GetDirectoryName(Path.GetFullPath(path)); }
        catch { return null; }
        if (dir == null) return null;

        for (var d = new DirectoryInfo(dir); d != null; d = d.Parent)
            if (d.GetFiles("*.csproj").Length > 0) return d.FullName;

        return Directory.Exists(dir) ? dir : null;
    }

    /// <summary>
    /// Loads the previewed file's own project assembly from its build output, so the designer can resolve the
    /// project's own types (<c>clr-namespace:</c> controls, behaviors) - not only engine assemblies. Best-effort:
    /// no .csproj ancestor, an unbuilt project or an unloadable dll just leaves those types unresolved, as before.
    /// </summary>
    private static void LoadProjectAssembly(string? aumlSourcePath)
    {
        var projectDir = ResolveAssetRoot(aumlSourcePath);
        if (projectDir == null) return;
        var csproj = Directory.GetFiles(projectDir, "*.csproj").FirstOrDefault();
        if (csproj == null) return;   // ResolveAssetRoot fell back to a non-project folder

        var dll = FindProjectAssembly(csproj);
        if (dll != null)
        {
            try { Assembly.LoadFrom(dll); } catch { /* ignore unloadable */ }
        }
    }

    /// <summary>
    /// The project's own compiled assembly under its build output, honouring this engine's
    /// <c>&lt;BaseOutputPath&gt;</c> redirect (else the conventional <c>bin</c>). Searches for the project-named dll
    /// and returns the most recently built one, so the current target framework wins over stale leftover TFM builds.
    /// </summary>
    private static string? FindProjectAssembly(string csprojPath)
    {
        var projectDir = Path.GetDirectoryName(csprojPath)!;
        var dllName = Path.GetFileNameWithoutExtension(csprojPath) + ".dll";

        string baseOutput = null;
        try { baseOutput = System.Xml.Linq.XDocument.Load(csprojPath).Descendants("BaseOutputPath").FirstOrDefault()?.Value?.Trim(); }
        catch { /* unreadable csproj - fall back to the default bin location */ }

        var binBase = !string.IsNullOrEmpty(baseOutput)
            ? Path.GetFullPath(Path.Combine(projectDir, baseOutput))
            : Path.Combine(projectDir, "bin");
        if (!Directory.Exists(binBase)) return null;

        return Directory.EnumerateFiles(binBase, dllName, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    /// <summary>Design size for a dimension: the declared value if set, else the request, else the default.</summary>
    private static double ResolveDimension(double? declared, uint? request, double fallback)
    {
        if (declared is > 0 && !double.IsNaN(declared.Value)) return declared.Value;
        if (request is > 0) return request.Value;
        return fallback;
    }

    private OffscreenRenderer GetRenderer(uint width, uint height)
    {
        // Reuse the one renderer across renders, only resizing its target when the size changes. Recreating it
        // per zoom would abandon the render cache (its units leak) and churn large GPU allocations.
        if (_renderer == null)
        {
            _renderer = new OffscreenRenderer(_device, _factory, width, height, MSAALevel.X4) { ClearColor = Colors.White };
        }
        else if (_rendererWidth != width || _rendererHeight != height)
        {
            _renderer.Resize(width, height);
        }

        _rendererWidth = width;
        _rendererHeight = height;
        return _renderer;
    }

    public void Dispose() => _renderer?.Dispose();
}
