using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Adamantium.Mathematics;

namespace Adamantium.Game.Sandbox.DrawingBoard.Models;

/// <summary>A COLOUR AS FOUR NUMBERS in a graph file - the same shape the node's own accent is written in.
/// <para>Without it a colour is written as <c>{}</c> and read back as black: the type keeps its channels in FIELDS, and
/// a serializer that is only asked about properties finds nothing to say. A file that carries a colour graph and loses
/// every colour in it is worse than one that refuses to save.</para></summary>
public sealed class ColorJson : JsonConverter<Color>
{
    /// <summary>The options a graph's state is written with and read back by - the one place that knows a colour is
    /// four numbers, so both directions cannot drift apart.</summary>
    public static readonly JsonSerializerOptions Options = new() { Converters = { new ColorJson() } };

    public override Color Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) return default;

        Span<byte> channels = stackalloc byte[4];
        var at = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.Number || at > 3) continue;

            channels[at++] = (byte)Math.Clamp(reader.GetInt32(), 0, 255);
        }

        return at == 4 ? Color.FromRgba(channels[0], channels[1], channels[2], channels[3]) : default;
    }

    public override void Write(Utf8JsonWriter writer, Color color, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(color.R);
        writer.WriteNumberValue(color.G);
        writer.WriteNumberValue(color.B);
        writer.WriteNumberValue(color.A);
        writer.WriteEndArray();
    }
}
