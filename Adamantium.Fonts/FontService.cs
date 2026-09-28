using Adamantium.Fonts.Common;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Adamantium.Fonts
{
    public class FontService
    {
        private readonly List<Typeface> typeFaces;
        public IReadOnlyCollection<Typeface> TypeFaces => typeFaces.AsReadOnly();

        public FontService(Typeface typeface)
        {
            typeFaces = new List<Typeface>();
            typeFaces.Add(typeface);
            typeface.SetDefaultFont();
        }

        public Typeface GetTypeFace(int index)
        {
            return typeFaces[index];
        }
        
        public static async Task<FontService> LoadTypeFaceAsync(string font)
        {
            var typeFace = await Typeface.LoadFontAsync(font, 3); // @TODO think and change the resolution approach
            return new FontService(typeFace);
        }

        
    }
}
