﻿
using System.Collections.Generic;
using Adamantium.UI.Markup;
using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Generators
{
    public class AumlMetadataContainer
    {
        public AumlMetadataContainer()
        {
            NamedElements = new List<NamedElement>();
            Usings = new List<string>();
            NamedElementsMap = new Dictionary<IAumlAstNode, string>();
            TypesMap = new Dictionary<string, TypeContainer>();
        }

        public string Namespace { get; set; }

        public string RootClassName { get; set; }

        public List<string> Usings { get; }

        public Dictionary<IAumlAstNode, string> NamedElementsMap { get; set; }

        // All named elements in the hierarchy
        public List<NamedElement> NamedElements { get; }

        public Dictionary<string, TypeContainer> TypesMap { get; }

        public IAumlAstNode RootNode { get; set; }
    }

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
}