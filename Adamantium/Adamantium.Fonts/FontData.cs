using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Adamantium.Fonts;

[XmlRoot(ElementName = "FontData")]
public class FontData
{
    [XmlElement(ElementName = "GlyphTextureSize")]
    public UInt32 GlyphTextureSize { get; set; }
    [XmlElement(ElementName = "SampleRate")]
    public byte SampleRate { get; set; }
    [XmlElement(ElementName = "PixelRangle")]
    public double PixelRangle { get; set; }
    [XmlElement(ElementName = "StartGlyphIndex")]
    public uint StartGlyphIndex { get; set; }
    [XmlElement(ElementName = "GlyphCount")]
    public uint GlyphCount { get; set; }
    [XmlElement(ElementName = "FontName")]
    public string FontName { get; set; }
    [XmlElement(ElementName = "IsSystemFont")]
    public bool IsSystemFont { get; set; }
    [XmlElement(ElementName = "PathToFont")]
    public string PathToFont { get; set; }

    public static FontData Parse(string data)
    {
        XmlSerializer xmlSerializer = new XmlSerializer(typeof(FontData));

        using (StringReader stringReader = new StringReader(data))
        {
            using (XmlReader xmlReader = XmlReader.Create(stringReader))
            {
                var result = (FontData)xmlSerializer.Deserialize(xmlReader);
                return result;
            }
        }
    }
}
