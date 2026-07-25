using System.Collections.Generic;
using System.Linq;

namespace Adamantium.UI.Core.Input;

/// <summary>In-app <see cref="IDataPackage"/>: a dictionary of format -> value. A bare CLR object is stored under its
/// type's full name; the OLE bridge (level 3) will add named standard formats over the same interface.</summary>
public sealed class DataPackage : IDataPackage
{
    private readonly Dictionary<string, object> _byFormat = new();

    public DataPackage()
    {
    }

    public DataPackage(object data)
    {
        Set(data);
    }

    public void Set(object data)
    {
        if (data != null)
        {
            _byFormat[data.GetType().FullName!] = data;
        }
    }

    public void Set(string format, object data) => _byFormat[format] = data;

    public object Get(string format) => _byFormat.TryGetValue(format, out var v) ? v : null;

    public T Get<T>()
    {
        foreach (var v in _byFormat.Values)
        {
            if (v is T t) return t;
        }
        return default;
    }

    public bool Contains(string format) => _byFormat.ContainsKey(format);

    public bool Contains<T>() => _byFormat.Values.Any(v => v is T);

    public IReadOnlyList<string> GetFormats() => _byFormat.Keys.ToList();
}
