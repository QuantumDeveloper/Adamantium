using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Adamantium.Graphics.Core.Models;

/// <summary>
/// MessagePack resolves a custom subclass of <see cref="Dictionary{TKey,TValue}"/> through its non-generic
/// dictionary formatter, which rebuilds it as a <c>Dictionary&lt;object,object&gt;</c> and then fails the
/// typed insert. This formatter (de)serializes such a subclass as a typed map and reconstructs the concrete
/// collection type.
/// </summary>
public sealed class DictionarySubclassFormatter<TDictionary, TKey, TValue> : IMessagePackFormatter<TDictionary>
    where TDictionary : Dictionary<TKey, TValue>, new()
{
    public void Serialize(ref MessagePackWriter writer, TDictionary value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        var keyFormatter = options.Resolver.GetFormatterWithVerify<TKey>();
        var valueFormatter = options.Resolver.GetFormatterWithVerify<TValue>();

        writer.WriteMapHeader(value.Count);
        foreach (var pair in value)
        {
            keyFormatter.Serialize(ref writer, pair.Key, options);
            valueFormatter.Serialize(ref writer, pair.Value, options);
        }
    }

    public TDictionary Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        var keyFormatter = options.Resolver.GetFormatterWithVerify<TKey>();
        var valueFormatter = options.Resolver.GetFormatterWithVerify<TValue>();

        var count = reader.ReadMapHeader();
        var result = new TDictionary();
        options.Security.DepthStep(ref reader);
        try
        {
            for (int i = 0; i < count; i++)
            {
                var key = keyFormatter.Deserialize(ref reader, options);
                var val = valueFormatter.Deserialize(ref reader, options);
                result[key] = val;
            }
        }
        finally
        {
            reader.Depth--;
        }

        return result;
    }
}
