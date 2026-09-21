using System.Xml;
using Adamantium.UI.Markup.AST;

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

    public override string ToString()
    {
        return $"Line: {Line}, Position: {Position}";
    }
}