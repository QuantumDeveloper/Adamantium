using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Adamantium.Core.Reflection;

/// <summary>
/// Enumerating types across loaded assemblies WITHOUT letting one broken assembly take the process down.
///
/// <para><see cref="Assembly.GetTypes"/> throws if any single type in the assembly cannot be resolved - a missing
/// dependency, a version mismatch, a plugin compiled against something else. Unguarded, that turns a partially loadable
/// assembly nobody was even asking about into a crash: the headless designer died on startup because a selector lookup
/// walked every assembly, and one of them could not resolve a handful of ECS component types.</para>
///
/// <para>The exception carries what DID load, so that is what is returned. Dropping the whole assembly instead (as one
/// of the hand-rolled guards used to) throws away the types that were fine, which for a lookup by name is the
/// difference between finding a control and silently not styling it.</para>
/// </summary>
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
