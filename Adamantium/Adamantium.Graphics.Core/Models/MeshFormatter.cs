using MessagePack;
using MessagePack.Formatters;

namespace Adamantium.Graphics.Core.Models;

/// <summary>
/// Adapter formatter that serializes a <see cref="Mesh"/> through its flat <see cref="MeshGeometry"/>
/// snapshot, so the rich <see cref="Mesh"/> type needs no serialization attributes. Registered ahead of
/// the contractless resolver in <see cref="SceneDataSerializer"/>.
/// </summary>
public sealed class MeshFormatter : IMessagePackFormatter<Mesh>
{
    public void Serialize(ref MessagePackWriter writer, Mesh value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        MessagePackSerializer.Serialize(ref writer, value.ToGeometry(), options);
    }

    public Mesh Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        var geometry = MessagePackSerializer.Deserialize<MeshGeometry>(ref reader, options);
        return geometry?.ToMesh();
    }
}
