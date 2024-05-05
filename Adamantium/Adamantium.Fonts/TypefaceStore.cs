using System.Collections.Generic;

namespace Adamantium.Fonts;

public static class TypefaceStore
{
    private static readonly Dictionary<string, Typeface> typefaceMap;

    static TypefaceStore()
    {
        typefaceMap = new Dictionary<string, Typeface>();
    }
        
    public static Typeface GetTypeface(string path, bool isSystem = false)
    {
        if (typefaceMap.TryGetValue(path, out var typeface))
        {
            return typeface;
        }

        typeface = isSystem ? Typeface.LoadSystemFont(path) : Typeface.LoadFont(path);
        
        typefaceMap[path] = typeface;

        return typeface;
    }
}