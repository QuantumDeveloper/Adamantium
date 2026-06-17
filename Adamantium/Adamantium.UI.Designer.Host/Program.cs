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

        Console.Error.WriteLine(
            "usage:\n" +
            "  host serve                                long-running JSON/stdio protocol (live preview)\n" +
            "  host render <file.auml> <out.png> [scale]  one-shot render at the window's design size x scale");
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
}
