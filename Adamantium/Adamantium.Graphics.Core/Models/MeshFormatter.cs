using System;
using Adamantium.Mathematics;
using MessagePack;
using MessagePack.Formatters;

namespace Adamantium.Graphics.Core.Models;

/// <summary>
/// Writes a <see cref="Mesh"/> as a fixed row of flat blobs rather than letting the contractless resolver spell
/// out "X", "Y", "Z" for every element of every array. Losing nothing is the rule, so the narrower forms are the
/// ones that provably cost nothing: positions as float where every one of them is exactly representable,
/// indices as ushort where the mesh has fewer than 65536 vertices, and no bitangents at all when they are
/// exactly <c>cross(normal, tangent) * tangent.W</c>, which is how they were made. Each of those is checked per
/// mesh and falls back to the wide form on its own.
/// </summary>
public sealed class MeshFormatter : IMessagePackFormatter<Mesh>
{
    /// <summary>Bumped whenever the row below changes shape. A file written by an older engine is refused by
    /// name instead of being read as nonsense.</summary>
    private const int Version = 1;

    private const int Fields = 17;

    private const byte BitangentsNone = 0;
    private const byte BitangentsDerived = 1;
    private const byte BitangentsStored = 2;

    public void Serialize(ref MessagePackWriter writer, Mesh value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        var geometry = value.ToGeometry();

        writer.WriteArrayHeader(Fields);
        writer.Write(Version);
        writer.Write(geometry.Name);
        writer.Write((int)geometry.MeshTopology);
        writer.Write((int)geometry.UpAxis);
        writer.Write(geometry.MaterialID);

        MeshBlobs.WritePoints(ref writer, geometry.Points);
        MeshBlobs.WriteIndices(ref writer, geometry.Indices);
        MeshBlobs.WriteSingles(ref writer, MeshBlobs.Flatten(geometry.Normals));
        MeshBlobs.WriteSingles(ref writer, MeshBlobs.Flatten(geometry.Tangents));

        if (geometry.BiTangents is not { Length: > 0 })
        {
            writer.Write(BitangentsNone);
            writer.WriteNil();
        }
        else if (MeshBlobs.AreDerivable(geometry.BiTangents, geometry.Normals, geometry.Tangents))
        {
            writer.Write(BitangentsDerived);
            writer.WriteNil();
        }
        else
        {
            writer.Write(BitangentsStored);
            MeshBlobs.WriteSingles(ref writer, MeshBlobs.Flatten(geometry.BiTangents));
        }

        MeshBlobs.WriteSingles(ref writer, MeshBlobs.Flatten(geometry.UV0));
        MeshBlobs.WriteSingles(ref writer, MeshBlobs.Flatten(geometry.UV1));
        MeshBlobs.WriteSingles(ref writer, MeshBlobs.Flatten(geometry.UV2));
        MeshBlobs.WriteSingles(ref writer, MeshBlobs.Flatten(geometry.UV3));
        MeshBlobs.WriteColors(ref writer, geometry.Colors);
        MeshBlobs.WriteSingles(ref writer, MeshBlobs.Flatten(geometry.JointIndices));
        MeshBlobs.WriteSingles(ref writer, MeshBlobs.Flatten(geometry.JointWeights));
    }

    public Mesh Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        var fields = reader.ReadArrayHeader();
        var version = reader.ReadInt32();
        if (version != Version)
        {
            throw new MessagePackSerializationException(
               $"This baked model is version {version}, and this engine reads version {Version}. Re-bake the content.");
        }
        if (fields != Fields)
        {
            throw new MessagePackSerializationException(
               $"A baked mesh of version {version} should carry {Fields} fields, this one has {fields}.");
        }

        var geometry = new MeshGeometry
        {
            Name = reader.ReadString(),
            MeshTopology = (Vulkan.Core.PrimitiveTopology)reader.ReadInt32(),
            UpAxis = (UpAxis)reader.ReadInt32(),
            MaterialID = reader.ReadString(),
            Points = MeshBlobs.ReadPoints(ref reader),
            Indices = MeshBlobs.ReadIndices(ref reader),
            Normals = MeshBlobs.ToVector3(MeshBlobs.ReadSingles(ref reader)),
            Tangents = MeshBlobs.ToVector4(MeshBlobs.ReadSingles(ref reader))
        };

        var bitangents = reader.ReadByte();
        var stored = MeshBlobs.ToVector3(MeshBlobs.ReadSingles(ref reader));
        geometry.BiTangents = bitangents switch
        {
            BitangentsDerived when geometry.Normals != null && geometry.Tangents != null
                => MeshBlobs.Derive(geometry.Normals, geometry.Tangents),
            BitangentsStored => stored,
            _ => null
        };

        geometry.UV0 = MeshBlobs.ToVector2(MeshBlobs.ReadSingles(ref reader));
        geometry.UV1 = MeshBlobs.ToVector2(MeshBlobs.ReadSingles(ref reader));
        geometry.UV2 = MeshBlobs.ToVector2(MeshBlobs.ReadSingles(ref reader));
        geometry.UV3 = MeshBlobs.ToVector2(MeshBlobs.ReadSingles(ref reader));
        geometry.Colors = MeshBlobs.ReadColors(ref reader);
        geometry.JointIndices = MeshBlobs.ToVector4(MeshBlobs.ReadSingles(ref reader));
        geometry.JointWeights = MeshBlobs.ToVector4(MeshBlobs.ReadSingles(ref reader));

        return geometry.ToMesh();
    }
}
