using System.Globalization;

namespace Adamantium.UI.Designer.Host;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "serve")
            return DesignerHost.Run();

        if (args.Length >= 3 && args[0] == "render")
            return RenderToFile(args);

        if (args.Length >= 4 && args[0] == "hittest")
            return HitTestToConsole(args);

        Console.Error.WriteLine(
            "usage:\n" +
            "  host serve                                  long-running JSON/stdio protocol (live preview)\n" +
            "  host render <file.auml> <out.png> [scale]   one-shot render at the window's design size x scale\n" +
            "  host hittest <file.auml> <x> <y>            render then map a design-space point to its markup line/col");
        return 2;
    }

    private static int RenderToFile(string[] args)
    {
        var aumlPath = args[1];
        var outPath = args[2];
        var scale = args.Length > 3 ? double.Parse(args[3], CultureInfo.InvariantCulture) : 1.0;

        using var session = new DesignerSession();
        var result = session.Render(File.ReadAllText(aumlPath), null, null, scale, outPath, aumlPath);

        foreach (var d in result.Diagnostics ?? Enumerable.Empty<string>())
            Console.Error.WriteLine($"[auml] {d}");

        if (!result.Success)
        {
            Console.Error.WriteLine($"[auml] {result.Error}");
            return 1;
        }

        Console.Error.WriteLine($"[auml] wrote {outPath} ({result.Width}x{result.Height})");
        return 0;
    }

    // Headless check for the designer hit-test (go-to-source / hover frame): render the file, then map a
    // design-space point to its authored element's markup line/column + rect.
    private static int HitTestToConsole(string[] args)
    {
        var aumlPath = args[1];
        var x = double.Parse(args[2], CultureInfo.InvariantCulture);
        var y = double.Parse(args[3], CultureInfo.InvariantCulture);

        using var session = new DesignerSession();
        var tmp = Path.Combine(Path.GetTempPath(), "auml-hittest.png");
        var result = session.Render(File.ReadAllText(aumlPath), null, null, 1.0, tmp, aumlPath);
        if (!result.Success)
        {
            Console.Error.WriteLine($"[auml] render failed: {result.Error}");
            return 1;
        }

        var hit = session.HitTest(x, y);
        if (hit is null)
            Console.Error.WriteLine($"[auml] hittest ({x},{y}): nothing authored there");
        else
            Console.Error.WriteLine($"[auml] hittest ({x},{y}): line {hit.Line} col {hit.Position} rect [{hit.X},{hit.Y} {hit.Width}x{hit.Height}]");
        return 0;
    }
}
