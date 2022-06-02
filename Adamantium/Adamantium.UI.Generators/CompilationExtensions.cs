using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Generators
{
    public static class CompilationExtensions
    {
        public static ISymbol GetMemberByName(this INamedTypeSymbol typeSymbol, string name)
        {
            if (typeSymbol == null)
            {
                return null;
            }

            var @base = typeSymbol;
            while (@base != null)
            {
                var list = @base.GetMembers(name).ToList();
                if (list.Count > 0)
                {
                    var data = list[0];
                    return data;
                }
                @base = @base.BaseType;
            }

            return null;
        }

        public static List<ISymbol> GetAllProperties(this INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
            {
                return null;
            }

            var properties = new List<ISymbol>();
            var @base = typeSymbol;
            while (@base != null)
            {
                var list = @base.GetMembers().Where(x => x.Kind == SymbolKind.Property).ToList();
                if (list.Count > 0)
                {
                    properties.AddRange(list);
                }
                @base = @base.BaseType;
            }

            return properties;
        }

        public static bool FindAttributeByName(this INamedTypeSymbol typeSymbol, string name, out IPropertySymbol property)
        {
            var properties = typeSymbol.GetAllProperties();
            property = null;

            foreach (var p in properties)
            {
                var attrs = p.GetAttributes().ToList();
                var result = attrs.FirstOrDefault(x => x.AttributeClass.Name == name);
                property = (IPropertySymbol)p;
                if (result != null) return true;
            }

            return false;
        }

        public static bool ImplementsInterface(this INamedTypeSymbol typeSymbol, string interfaceName)
        {
            var @interface = typeSymbol.AllInterfaces.FirstOrDefault(x => x.Name == interfaceName);
            return @interface != null;
        }

        public static TypeContainer GetTypeContainerForNamespace(this Compilation compilation, string @namespace)
        {
            var ns = @namespace.Split(new char[] { '.' });
            var assemblySymbol = compilation.SourceModule.ReferencedAssemblySymbols.First(q => q.Name == @namespace);
            INamespaceSymbol namespaceSymbol = null;
            var currentNamespace = assemblySymbol.GlobalNamespace;

            // Find base namespace
            foreach (var nsPart in ns)
            {
                namespaceSymbol = currentNamespace.GetNamespaceMembers().First(x => x.Name == nsPart);
                currentNamespace = namespaceSymbol;
            }

            var typeMembers = new List<INamedTypeSymbol>();

            var nsStack = new Stack<INamespaceSymbol>();
            nsStack.Push(currentNamespace);
            while (nsStack.Count > 0)
            {
                var nsSymbol = nsStack.Pop();
                var members = nsSymbol.GetTypeMembers();
                typeMembers.AddRange(members);

                foreach (var nsMember in nsSymbol.GetNamespaceMembers())
                {
                    nsStack.Push(nsMember);
                }
            }

            return new TypeContainer(assemblySymbol, typeMembers);
        }
    }
}
