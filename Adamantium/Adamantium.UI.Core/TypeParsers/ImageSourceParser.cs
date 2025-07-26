using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Core.TypeParsers;

public class ImageSourceParser : ITypeParser<BitmapSource>
{
    public BitmapSource Parse(string value)
    {
        return new BitmapImage(new Uri($"file://{value}"));
    }
}