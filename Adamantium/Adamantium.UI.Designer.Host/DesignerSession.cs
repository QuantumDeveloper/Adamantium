using System.Reflection;
using System.Text.RegularExpressions;
using Adamantium.Core;
using Adamantium.Game;
using Adamantium.Game.Core;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
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
    private readonly IGameService _gameService;

    private OffscreenRenderer _renderer;
    private uint _rendererWidth;
    private uint _rendererHeight;

    // The last rendered tree, kept so a follow-up hittest request (designer click/hover) can map a point back to
    // the authored element and its markup position without re-rendering.
    private IWindow _lastWindow;
    private IReadOnlyDictionary<object, AumlSourceSpan> _lastSourceMap;

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

        // The engine's game services are normally registered by GameApplication.RegisterServices, which only runs
        // via Run()/Initialize() - which the headless designer skips. Register the game service explicitly so a
        // design-aware behavior can resolve IGameService and host a game in the preview (see DriveDesignTimeGames).
        _gameService = new GameService();
        _app.Container.RegisterInstance<IGameService>(_gameService);

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

        // Design-time DataContext (x:DataType view-model): rendered with real sample data so {Binding} paths resolve
        // in the preview - the WPF d:DesignInstance behaviour. Applied after the first layout (below), once the
        // logical tree exists so it propagates to every element. Suppressed by x:DesignCreatable="false".
        var designContext = CreateDesignDataContext(aumlText);

        var designWidth = ResolveDimension(sizeSource?.Width, requestWidth, DefaultWidth);
        var designHeight = ResolveDimension(sizeSource?.Height, requestHeight, DefaultHeight);

        window.ClientWidth = designWidth;
        window.ClientHeight = designHeight;

        // Layout (Measure/Arrange + theme) at the design size - geometry only, no native window.
        window.Update(_app.ThemeManager, new AppTime());

        // The logical tree (and templates) now exist: assign the design-time DataContext so it propagates to every
        // element and their {Binding}s resolve, then re-run layout so the bound values participate in measure/arrange.
        if (designContext != null && load.Root is IFundamentalUIComponent rootComponent)
        {
            rootComponent.DataContext = designContext;
            window.Update(_app.ThemeManager, new AppTime());
        }

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

        // Keep the laid-out tree + source map for a follow-up hittest (designer click → go-to-source / hover frame).
        _lastWindow = window;
        _lastSourceMap = load.SourceMap;

        // Design-time game preview: a design-aware behavior (e.g. GameHostBehavior) created+bound a game to its
        // RenderTargetPanel while the tree was built. Drive a short snapshot now so the game loads its content and
        // publishes a frame to the panel; the OffscreenRenderer composites that below. No-op when no game is hosted.
        DriveDesignTimeGames();

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

        return RenderResult.Ok(outPath, load.Diagnostics, targetWidth, targetHeight, renderScale,
            (uint)Math.Round(designWidth), (uint)Math.Round(designHeight));
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

    // Project assemblies loaded this session: load each once so the warm host doesn't add duplicate type copies.
    private static readonly HashSet<string> _loadedProjectAssemblies = new(StringComparer.OrdinalIgnoreCase);

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
        if (dll == null) return;
        lock (_loadedProjectAssemblies)
        {
            if (!_loadedProjectAssemblies.Add(dll)) return;   // already loaded this session
        }

        // Load from a byte copy, not Assembly.LoadFrom: LoadFrom keeps the .dll file locked, which would stop the
        // user rebuilding their project while this warm preview host has it open. Loaded once per session (a rebuild
        // is picked up after a host restart; full hot-reload is a separate, deferred workstream).
        try { Assembly.Load(File.ReadAllBytes(dll)); } catch { /* ignore unloadable */ }
    }

    /// <summary>
    /// The project's own compiled assembly under its build output. Looks in (in order) an explicit
    /// <c>&lt;BaseOutputPath&gt;</c>, the conventional per-project <c>bin</c>, and a consolidated <c>artifacts/bin</c>
    /// found by walking up from the project (this engine builds every project into one such folder via a root
    /// Directory.Build.props). Returns the most recently built match, so the current target framework wins over stale
    /// leftover TFM builds.
    /// </summary>
    private static string? FindProjectAssembly(string csprojPath)
    {
        var dllName = Path.GetFileNameWithoutExtension(csprojPath) + ".dll";

        foreach (var binBase in CandidateOutputRoots(csprojPath))
        {
            if (!Directory.Exists(binBase)) continue;
            var dll = Directory.EnumerateFiles(binBase, dllName, SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (dll != null) return dll;
        }
        return null;
    }

    private static IEnumerable<string> CandidateOutputRoots(string csprojPath)
    {
        var projectDir = Path.GetDirectoryName(csprojPath)!;

        // 1. An explicit <BaseOutputPath> in the csproj.
        string baseOutput = null;
        try { baseOutput = System.Xml.Linq.XDocument.Load(csprojPath).Descendants("BaseOutputPath").FirstOrDefault()?.Value?.Trim(); }
        catch { /* unreadable csproj - skip */ }
        if (!string.IsNullOrEmpty(baseOutput))
            yield return Path.GetFullPath(Path.Combine(projectDir, baseOutput));

        // 2. The conventional per-project bin.
        yield return Path.Combine(projectDir, "bin");

        // 3. A consolidated artifacts/bin (this engine's layout: a root Directory.Build.props redirects every
        //    project's OutputPath there, so the project has no local bin). Walk up from the project to find it.
        for (var dir = new DirectoryInfo(projectDir); dir != null; dir = dir.Parent)
        {
            var artifactsBin = Path.Combine(dir.FullName, "artifacts", "bin");
            if (Directory.Exists(artifactsBin)) { yield return artifactsBin; break; }
        }
    }

    /// <summary>
    /// Instantiates the markup's design-time view-model (<c>x:DataType="prefix:Type"</c>) so the preview shows real
    /// sample data through its bindings - the WPF <c>d:DesignInstance</c> / <c>IsDesignTimeCreatable</c> behaviour.
    /// Returns null when there is no x:DataType, when <c>x:DesignCreatable="false"</c> opts out, or when the type has
    /// no public parameterless constructor (we never run a parameterised one at design time).
    /// </summary>
    private static object CreateDesignDataContext(string aumlText)
    {
        var dataType = MatchAttributeValue(aumlText, "DataType");
        if (dataType == null) return null;
        if (string.Equals(MatchAttributeValue(aumlText, "DesignCreatable"), "false", StringComparison.OrdinalIgnoreCase))
            return null;

        var type = ResolveMarkupType(aumlText, dataType);
        if (type?.GetConstructor(Type.EmptyTypes) == null) return null;
        try { return Activator.CreateInstance(type); } catch { return null; }
    }

    /// <summary>Value of the first <c>[prefix:]localName="..."</c> attribute in the text (prefix optional).</summary>
    private static string MatchAttributeValue(string text, string localName)
    {
        var m = Regex.Match(text, $@"(?:\w+:)?{Regex.Escape(localName)}\s*=\s*""([^""]*)""");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    /// <summary>Resolves a markup type reference (<c>prefix:Type</c>) to a CLR type using the buffer's xmlns
    /// declarations (clr-namespace) against the assemblies loaded in this host (engine + the project assembly).</summary>
    private static Type ResolveMarkupType(string aumlText, string qualified)
    {
        var prefix = "";
        var local = qualified;
        var colon = qualified.IndexOf(':');
        if (colon >= 0) { prefix = qualified[..colon]; local = qualified[(colon + 1)..]; }

        var xmlnsPattern = prefix.Length == 0 ? @"xmlns\s*=\s*""([^""]*)""" : $@"xmlns:{Regex.Escape(prefix)}\s*=\s*""([^""]*)""";
        var xm = Regex.Match(aumlText, xmlnsPattern);
        string clrNamespace = null, assemblyName = null;
        if (xm.Success && xm.Groups[1].Value.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            var rest = xm.Groups[1].Value["clr-namespace:".Length..];
            var sc = rest.IndexOf(';');
            clrNamespace = (sc < 0 ? rest : rest[..sc]).Trim();
            var ai = rest.IndexOf("assembly=", StringComparison.Ordinal);
            if (ai >= 0) assemblyName = rest[(ai + "assembly=".Length)..].Trim();
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assemblyName != null && !string.Equals(asm.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase)) continue;
            var type = clrNamespace != null
                ? asm.GetType(clrNamespace + "." + local, throwOnError: false)
                : SafeGetTypes(asm).FirstOrDefault(t => t.Name == local);
            if (type != null) return type;
        }
        return null;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
    }

    /// <summary>Design size for a dimension: the declared value if set, else the request, else the default.</summary>
    private static double ResolveDimension(double? declared, uint? request, double fallback)
    {
        if (declared is > 0 && !double.IsNaN(declared.Value)) return declared.Value;
        if (request is > 0) return request.Value;
        return fallback;
    }

    // Wall-clock budget for the design-time game snapshot: enough for the game to parse+load its model content
    // (async) and publish a stable frame. A one-shot preview, so a few seconds is acceptable.
    private const double GameSnapshotSeconds = 15.0;

    /// <summary>
    /// Drives every game hosted in the markup (a design-aware behavior created it bound to a RenderTargetPanel) for a
    /// short snapshot: each iteration runs one game frame and waits the device idle, with a tiny sleep so the game's
    /// async content load completes. RunOnce publishes the frame to the panel (CopyOutput), so the panel samples it
    /// when the OffscreenRenderer composites below. No-op when the markup hosts no game.
    /// </summary>
    private void DriveDesignTimeGames()
    {
        var games = _gameService.Games;
        if (games.Count == 0) return;

        var total = TimeSpan.Zero;
        const double dt = 1.0 / 60.0;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(GameSnapshotSeconds);
        ulong frame = 0;
        while (DateTime.UtcNow < deadline)
        {
            total += TimeSpan.FromSeconds(dt);
            var time = new AppTime { FramesCount = ++frame, FrameTime = dt, TotalTime = total };
            foreach (var game in games)
            {
                game.RunOnce(time);
                game.Submit();
            }
            _device.DeviceWaitIdle();
            Thread.Sleep(8);   // let the async content-load tasks make progress between frames
        }
        _device.DeviceWaitIdle();
    }

    /// <summary>
    /// Maps a point in the rendered frame's design space to the authored element under it (designer go-to-source /
    /// hover frame): hit-tests the last rendered tree, walks up to the nearest element that came from the AUML
    /// (template-internal parts have no source position), and returns its markup line/column plus its rect in design
    /// space. Null when nothing is rendered or nothing authored sits under the point.
    /// </summary>
    public HitTestResult HitTest(double x, double y)
    {
        if (_lastWindow is not IInputComponent root || _lastSourceMap is null) return null;

        var hit = root.HitTest(new Vector2(x, y));
        for (var current = hit as IUIComponent; current is not null; current = current.VisualParent)
        {
            if (_lastSourceMap.TryGetValue(current, out var span))
            {
                // WorldTransform is already accumulated up the tree → its translation is the element's origin in
                // design space; most controls' rect is origin + RenderSize. A shape's layout box, though, spans from
                // (0,0) to the geometry's max (it reserves space for the geometry's coordinate origin, like WPF
                // Stretch=None), so for a Path we draw the selection frame from the geometry's tight bounds instead —
                // the Blend/WPF designer approach (a designer-side adorner, the engine's layout is left untouched).
                var pos = current.WorldTransform.TranslationVector;
                if (current is Adamantium.UI.Controls.Shapes.Path { Data: { } geometry })
                {
                    geometry.RecalculateBounds();
                    var b = geometry.Bounds;
                    return new HitTestResult(span.Line, span.Position, pos.X + b.X, pos.Y + b.Y, b.Width, b.Height);
                }
                var size = current.RenderSize;
                return new HitTestResult(span.Line, span.Position, pos.X, pos.Y, size.Width, size.Height);
            }
        }
        return null;
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
