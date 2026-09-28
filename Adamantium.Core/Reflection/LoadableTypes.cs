using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Adamantium.Core.Reflection;

/// <summary>Enumerates types across loaded assemblies without letting one unresolvable type crash the caller: when
/// <see cref="Assembly.GetTypes"/> throws, the types that did load are still returned.</summary>
public static class LoadableTypes
{
    /// <summary>The types this assembly can actually produce - all of them, or the ones that resolved.</summary>
    public static IEnumerable<Type> Of(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null);
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    /// <summary>Every type in every loaded assembly, skipping what cannot be resolved.</summary>
    public static IEnumerable<Type> FromLoadedAssemblies()
        => AppDomain.CurrentDomain.GetAssemblies().SelectMany(Of);
}
