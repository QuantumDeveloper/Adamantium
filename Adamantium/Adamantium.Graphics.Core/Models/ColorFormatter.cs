using Adamantium.Mathematics;
using MessagePack;
using MessagePack.Formatters;

namespace Adamantium.Graphics.Core.Models;

/// <summary>
/// <see cref="Color"/> exposes public R/G/B/A byte fields, but all of its constructors take parameters
/// named red/green/blue/alpha, which the contractless resolver can't match to those fields (and structs
/// expose no implicit parameterless constructor via reflection). This formatter serializes the four bytes
/// directly.
/// </summary>
public sealed class ColorFormatter : IMessagePackFormatter<Color>
{
    public void Serialize(ref MessagePackWriter writer, Color value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(4);
        writer.Write(value.R);
        writer.Write(value.G);
        writer.Write(value.B);
        writer.Write(value.A);
    }

    public Color Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        reader.ReadArrayHeader();
        var r = reader.ReadByte();
        var g = reader.ReadByte();
        var b = reader.ReadByte();
        var a = reader.ReadByte();
        return new Color(r, g, b, a);
    }
}
