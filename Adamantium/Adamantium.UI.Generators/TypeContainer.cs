using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Generators;

public class TypeContainer
{
    public TypeContainer(IAssemblySymbol assembly, List<INamedTypeSymbol> types)
    {
        AssemblySymbol = assembly;
        Types = types;
    }

    public IAssemblySymbol AssemblySymbol { get; }

    public List<INamedTypeSymbol> Types { get; }


}