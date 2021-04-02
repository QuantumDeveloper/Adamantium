using System.IO;
using Adamantium.Fonts.OTF;

namespace Adamantium.Fonts
{
    public class TypeFace
    {
        public static TypeFace LoadFont(string path, uint sampleResolution)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(nameof(path));
            
            var parser = new OTFParser(path, sampleResolution);

            return parser.TypeFace;
        }
    }
}