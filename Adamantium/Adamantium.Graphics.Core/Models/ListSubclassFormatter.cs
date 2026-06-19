using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace Adamantium.Graphics.Core.Models;

/// <summary>
/// Same idea as <see cref="DictionarySubclassFormatter{TDictionary,TKey,TValue}"/> but for subclasses of
/// <see cref="List{T}"/>.
/// </summary>
public sealed class ListSubclassFormatter<TList, TItem> : IMessagePackFormatter<TList>
    where TList : List<TItem>, new()
{
    public void Serialize(ref MessagePackWriter writer, TList value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        var itemFormatter = options.Resolver.GetFormatterWithVerify<TItem>();
        writer.WriteArrayHeader(value.Count);
        foreach (var item in value)
        {
            itemFormatter.Serialize(ref writer, item, options);
        }
    }

    public TList Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        var itemFormatter = options.Resolver.GetFormatterWithVerify<TItem>();
        var count = reader.ReadArrayHeader();
        var result = new TList();
        options.Security.DepthStep(ref reader);
        try
        {
            for (int i = 0; i < count; i++)
            {
                result.Add(itemFormatter.Deserialize(ref reader, options));
            }
        }
        finally
        {
            reader.Depth--;
        }

        return result;
    }
}
