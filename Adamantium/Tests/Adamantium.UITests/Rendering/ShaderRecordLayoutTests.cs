using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Adamantium.EffectsCompiler;
using Adamantium.Mathematics;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

// Every batch record is read by the shader through a raw pointer (BDA), so the two sides agree on the layout or the
// draw reads the wrong bytes - with no link error and nothing from the validation layer. Nothing checked that
// agreement until now: while both sides are solid float4 they match TRIVIALLY, and the first scalar field is what
// splits them. So this compares the C# record against the offsets the SHIPPED shader binary declares, not against a
// hand-copied opinion of what the shader ought to say.
[TestFixture]
public class ShaderRecordLayoutTests
{
    public sealed record Record(Type Item, string EffectType, string StructName);

    private static readonly Record[] Records =
    {
        new(typeof(RectItem), "Adamantium.UI.Effects.Generated.BatchEffect", "RectData"),
        new(typeof(EllipseItem), "Adamantium.UI.Effects.Generated.BatchEffect", "EllipseData"),
        new(typeof(PolygonItem), "Adamantium.UI.Effects.Generated.BatchEffect", "PolygonData"),
        new(typeof(HaloRectItem), "Adamantium.UI.Effects.Generated.BatchEffect", "HaloRectData"),
        new(typeof(HaloLivingItem), "Adamantium.UI.Effects.Generated.BatchEffect", "HaloLivingData"),
        new(typeof(GradientRectItem), "Adamantium.UI.Effects.Generated.BrushEffect", "GradientRectData"),
        new(typeof(PatternRectItem), "Adamantium.UI.Effects.Generated.BrushEffect", "PatternRectData"),
        new(typeof(TextureItem), "Adamantium.UI.Effects.Generated.BrushEffect", "TexRectData"),
        new(typeof(FractalRectItem), "Adamantium.UI.Effects.Generated.BrushEffect", "FractalRectData"),
        new(typeof(MaterialRectItem), "Adamantium.UI.Effects.Generated.MaterialEffect", "MaterialRectData"),
        new(typeof(GlyphItem), "Adamantium.FX.Effects.Generated.FontEffect", "GlyphData"),
        // The RETAINED families read their instances by BDA exactly as the SDF batches do, and were missed by the first
        // pass of this list because they live under Rendering/Retained and are named "...Instance" rather than "...Item".
        // A record left out of here is a record whose two sides can drift in silence, which is the whole point.
        new(typeof(Adamantium.UI.Rendering.Retained.GeometryInstance), "Adamantium.UI.Effects.Generated.BatchEffect", "GeometryInstance"),
        new(typeof(Adamantium.UI.Rendering.Retained.GradientGeometryInstance), "Adamantium.UI.Effects.Generated.BrushEffect", "GradGeomData"),
        new(typeof(Adamantium.UI.Rendering.Retained.TextureGeometryInstance), "Adamantium.UI.Effects.Generated.BrushEffect", "TextureGeomData"),
        new(typeof(Adamantium.UI.Rendering.Retained.PatternGeometryInstance), "Adamantium.UI.Effects.Generated.BrushEffect", "PatternGeomData"),
    };

    private static IEnumerable<TestCaseData> Cases() =>
        Records.Select(r => new TestCaseData(r).SetName($"{r.Item.Name} agrees with {r.StructName}"));

    [TestCaseSource(nameof(Cases))]
    public void TheRecordAgreesWithTheShaderThatReadsIt(Record record)
    {
        var declared = FindStruct(record);

        var fields = record.Item.GetFields(BindingFlags.Public | BindingFlags.Instance);
        Assert.That(declared.Members.Select(m => m.Name), Is.EqualTo(fields.Select(f => f.Name)),
            $"{record.Item.Name} and {record.StructName} do not carry the same fields in the same order");

        foreach (var member in declared.Members)
        {
            var offset = (int)Marshal.OffsetOf(record.Item, member.Name);
            Assert.That(member.Offset, Is.EqualTo(offset),
                $"{record.StructName}.{member.Name} sits at {member.Offset} in the shader but at {offset} in " +
                $"{record.Item.Name} - every instance past the first would be read from the wrong place");
        }

        var size = Marshal.SizeOf(record.Item);
        Assert.That(declared.Stride, Is.EqualTo(size),
            $"{record.StructName} strides {declared.Stride} bytes per instance, {record.Item.Name} is {size}");
    }

    // A colour put back into bytes has to come back as the colour that went in. It can, because a colour reaches a
    // record as a byte over 255 - so the round trip is exact for every one of the 256 values a channel can hold, and
    // "lossless" is then a claim that can be checked rather than a hope. Rounding is the part that bites: truncating
    // sends channels a step darker, which is a whole theme drifting.
    [Test]
    public void AColourSurvivesTheRoundTripThroughAVector()
    {
        for (var b = 0; b < 256; b++)
        {
            var source = new Color((byte)b, (byte)b, (byte)b, (byte)b);
            var back = new Color(source.ToVector4());
            Assert.That(new[] { back.R, back.G, back.B, back.A }, Is.EqualTo(new[] { (byte)b, (byte)b, (byte)b, (byte)b }),
                $"byte {b} did not survive Color -> Vector4F -> Color");
        }
    }

    private sealed record Member(string Name, int Offset);

    private sealed record DeclaredStruct(IReadOnlyList<Member> Members, int Stride);

    private static DeclaredStruct FindStruct(Record record)
    {
        var effect = Type.GetType($"{record.EffectType}, {AssemblyOf(record.EffectType)}")
                     ?? AppDomain.CurrentDomain.GetAssemblies()
                         .Select(a => a.GetType(record.EffectType)).FirstOrDefault(t => t != null);
        Assert.That(effect, Is.Not.Null, $"the generated effect {record.EffectType} is not in this build");

        var field = effect.GetField("bytecode", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(field, Is.Not.Null, $"{record.EffectType} no longer carries its compiled bytecode");
        var data = (EffectData)field.GetValue(null);

        foreach (var shader in data.Shaders)
        {
            var found = ReadStruct(shader.Bytecode, record.StructName);
            if (found != null) return found;
        }

        Assert.Fail($"no shader in {record.EffectType} declares {record.StructName}");
        return null;
    }

    private static string AssemblyOf(string effectType) =>
        effectType.StartsWith("Adamantium.FX.") ? "Adamantium.FX" : "Adamantium.UI.FX";

    // Slang names the pointed-at struct "<Name>_natural" after the layout it chose for it, which is the whole point:
    // the suffix IS the compiler saying which rule it used, and "natural" is the C-like one - a float4 aligned to its
    // component, exactly like the C# side. Anything else here means the compiler changed its mind about the layout.
    private const string NaturalSuffix = "_natural";

    private static DeclaredStruct ReadStruct(byte[] spirv, string name)
    {
        if (spirv == null || spirv.Length < 20) return null;
        var words = new uint[spirv.Length / 4];
        Buffer.BlockCopy(spirv, 0, words, 0, words.Length * 4);
        if (words[0] != 0x07230203) return null;

        var names = new Dictionary<uint, string>();
        var memberNames = new Dictionary<(uint, uint), string>();
        var offsets = new Dictionary<(uint, uint), int>();
        var strides = new Dictionary<uint, int>();
        var pointees = new Dictionary<uint, uint>();

        for (var i = 5; i < words.Length;)
        {
            var count = (int)(words[i] >> 16);
            if (count <= 0 || i + count > words.Length) break;
            var op = words[i] & 0xFFFF;

            switch (op)
            {
                case 5:
                    names[words[i + 1]] = ReadString(words, i + 2, i + count);
                    break;
                case 6:
                    memberNames[(words[i + 1], words[i + 2])] = ReadString(words, i + 3, i + count);
                    break;
                case 71 when count >= 4 && words[i + 2] == 6:
                    strides[words[i + 1]] = (int)words[i + 3];
                    break;
                case 72 when count >= 5 && words[i + 3] == 35:
                    offsets[(words[i + 1], words[i + 2])] = (int)words[i + 4];
                    break;
                case 32 when count >= 4:
                    pointees[words[i + 1]] = words[i + 3];
                    break;
            }

            i += count;
        }

        var id = names.Where(p => p.Value == name || p.Value == name + NaturalSuffix)
            .Select(p => (uint?)p.Key).FirstOrDefault();
        if (id == null) return null;

        var members = new List<Member>();
        for (uint index = 0; memberNames.TryGetValue((id.Value, index), out var member); index++)
        {
            Assert.That(offsets.ContainsKey((id.Value, index)), Is.True,
                $"{name}.{member} carries no Offset decoration - the shader was compiled without an explicit layout");
            members.Add(new Member(member, offsets[(id.Value, index)]));
        }

        if (members.Count == 0) return null;

        var stride = pointees.Where(p => p.Value == id.Value && strides.ContainsKey(p.Key))
            .Select(p => (int?)strides[p.Key]).FirstOrDefault();
        Assert.That(stride, Is.Not.Null,
            $"no pointer to {name} carries an ArrayStride - the per-instance stride cannot be read from the binary");

        return new DeclaredStruct(members, stride.Value);
    }

    private static string ReadString(uint[] words, int start, int end)
    {
        var bytes = new List<byte>();
        for (var i = start; i < end; i++)
        {
            for (var b = 0; b < 4; b++)
            {
                var value = (byte)(words[i] >> (b * 8));
                if (value == 0) return Encoding.UTF8.GetString(bytes.ToArray());
                bytes.Add(value);
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
