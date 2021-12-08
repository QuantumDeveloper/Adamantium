using Adamantium.Fonts.Common;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Adamantium.Fonts
{
    public class FontService
    {
        private readonly List<TypeFace> typeFaces;
        public IReadOnlyCollection<TypeFace> TypeFaces => typeFaces.AsReadOnly();

        public FontService(TypeFace typeFace)
        {
            typeFaces = new List<TypeFace>();
            typeFaces.Add(typeFace);
            typeFace.SetDefaultFont();
        }

        public TypeFace GetTypeFace(int index)
        {
            return typeFaces[index];
        }
        
        public static async Task<FontService> LoadTypeFaceAsync(string font)
        {
            var typeFace = await TypeFace.LoadFontAsync(font, 3); // @TODO think and change the resolution approach
            return new FontService(typeFace);
        }

        
    }
}
