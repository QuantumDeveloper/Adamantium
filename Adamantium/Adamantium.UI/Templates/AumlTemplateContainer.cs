using System;
using System.Collections.Generic;
using Adamantium.UI.Markup;

namespace Adamantium.UI.Templates;

public class AumlTemplateContainer
{
    public AumlTemplateContainer()
    {
        NamedElementsMap = new Dictionary<IAumlAstNode, string>();
        NamedElements = new List<NamedElement>();
        TypesMap = new Dictionary<string, Type>();
    }
    
    public Dictionary<IAumlAstNode, string> NamedElementsMap { get; set; }

    // All named elements in the hierarchy
    public List<NamedElement> NamedElements { get; }

    public Dictionary<string, Type> TypesMap { get; }

    public IAumlAstNode RootNode { get; set; }
}