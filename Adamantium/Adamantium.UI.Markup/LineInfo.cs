using System.Xml;

namespace Adamantium.UI.Markup;

public class LineInfo : IAumlLineInfo
{
    public LineInfo(IXmlLineInfo info)
    {
        Line = info.LineNumber;
        Position = info.LinePosition;
    }
    public int Line { get; set; }
    public int Position { get; set; }
}