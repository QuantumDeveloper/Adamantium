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

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 3 || args[0] != "render")
        {
            Console.Error.WriteLine("usage: host render <file.auml> <out.png> [width height]");
            return 2;
        }

        var aumlPath = args[1];
        var outPath = args[2];
        var width = args.Length > 3 ? uint.Parse(args[3]) : 1280u;
        var height = args.Length > 4 ? uint.Parse(args[4]) : 720u;

        // Load all engine assemblies so reflection type resolution can see every control type.
        foreach (var dll in Directory.GetFiles(AppContext.BaseDirectory, "Adamantium*.dll"))
        {
            try { System.Reflection.Assembly.LoadFrom(dll); } catch { /* ignore unloadable */ }
        }

        var app = new DesignerApplication();

        // The headless app never opens a window, so nothing triggers device creation - do it explicitly.
        var deviceService = app.Container.Resolve<IGraphicsDeviceService>();
        deviceService.CreateMainDevice("Designer");

        // Same as UIApplication.LoadThemes() (skipped because we don't call Run()): without a theme the
        // controls have no templates/brushes and render nothing.
        var theme = new Adamantium.UI.Themes.FluentDarkTheme.FluentDark();
        app.ThemeManager.AddTheme(theme.Name, theme);
        app.ThemeManager.SetTheme(theme);

        var text = File.ReadAllText(aumlPath);
        var result = AumlLoader.Load(
            text,
            AppDomain.CurrentDomain.GetAssemblies(),
            t => typeof(IWindow).IsAssignableFrom(t) ? typeof(VirtualWindow) : t);

        foreach (var d in result.Diagnostics)
            Console.Error.WriteLine($"[auml] {d}");

        if (result.Root is not IWindow window)
        {
            Console.Error.WriteLine($"[auml] root is not a window: {result.Root?.GetType().Name ?? "null"}");
            return 1;
        }

        window.AttachContextAndInitialize(app.UIContext);
        window.ClientWidth = width;
        window.ClientHeight = height;

        // Layout (Measure/Arrange + theme) - geometry only, no native window.
        window.Update(app.ThemeManager, new AppTime());

        var device = app.GraphicsContext.CreateGraphicsDevice();
        var factory = new RenderUnitFactory(device, app.GraphicsContext.GetResourceFactory());
        using var renderer = new OffscreenRenderer(device, factory, width, height);
        renderer.ClearColor = Colors.White;

        if (!renderer.RenderFrame((IRootVisualComponent)window))
        {
            Console.Error.WriteLine("[auml] render failed");
            return 1;
        }

        renderer.Save(outPath, ImageFileType.Png);
        Console.WriteLine($"[auml] wrote {outPath} ({width}x{height})");
        return 0;
    }
}
