namespace Adamantium.UI.Markup;

public static class PathExtension
{
    public static string ToNamespace(this string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        string cleanedPath = path.Replace('/', '.').Replace('\\', '.').Trim('.');
        
        cleanedPath = cleanedPath.Replace('-', '_').Replace(' ', '_');

        var segments = cleanedPath.Split('.')
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        for (int i = 0; i < segments.Count; i++)
        {
            if (char.IsDigit(segments[i][0]))
            {
                segments[i] = "_" + segments[i];
            }
        }

        return string.Join(".", segments);
    }
}