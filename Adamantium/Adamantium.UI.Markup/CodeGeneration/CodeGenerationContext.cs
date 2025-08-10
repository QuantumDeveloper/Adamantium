using Adamantium.Core;
using Adamantium.UI.Markup.AST;
using Adamantium.UI.Markup.AST.MarkupExtension;

namespace Adamantium.UI.Markup.CodeGeneration;

public class CodeGenerationContext
{
    public string ParentName { get; private set; }
    public Stack<string> ElementStack { get; }

    public Stack<IResolvedType> TargetTypeContext { get; }
    public TextGenerator TextGenerator { get; }
    public AumlMetadataContainer Metadata { get; }
    public int Id { get; set; }
    
    public EntityType EntityType { get; }

    public CodeGenerationContext(TextGenerator textGenerator, AumlMetadataContainer metadata, EntityType entityType)
    {
        Id = 1;
        ElementStack = new();
        TargetTypeContext = new();
        TextGenerator = textGenerator;
        Metadata = metadata;
        EntityType = entityType;
    }

    public string GenerateNextElementName(string name = "") =>
        string.IsNullOrEmpty(name) ? $"element_{Id++}" : $"{name}_{Id++}";

    public void PushElement(string name)
    {
        ElementStack.Push(name);
        ParentName = name;
    }
    
    public string PeekElement() => ElementStack.Count > 0 ? ElementStack.Peek() : null;

    public void PopElement()
    {
        ElementStack.Pop();
        ParentName = ElementStack.Count > 0 ? ElementStack.Peek() : string.Empty;
    }

    public void PushTypeContext(IResolvedType resolvedType)
    {
        TargetTypeContext.Push(resolvedType);
    }
    
    public void PopTypeContext()
    {
        TargetTypeContext.Pop();
    }
    
    private IResolvedType GetEffectiveTargetType(IResolvedType fallback)
    {
        if (TargetTypeContext.Count > 0)
            return TargetTypeContext.Peek();
        return fallback;
    }

    
    public void WithElement(string name, Action action)
    {
        PushElement(name);
        try
        {
            action();
        }
        finally
        {
            PopElement();
        }
    }
    
    public string CurrentParent => PeekElement();
    
    public string ProcessControlElements(
        AumlAstObjectNode element,
        IDiagnosticSink diagnostics,
        bool isResource)
    {
        var typeContainer = Metadata.TypeResolver.GetResolvedAssembly(element.TypeReference.Assembly);
        var typeInfo = typeContainer.Types.FirstOrDefault(x => x.Name == element.TypeReference.Name);
        var properties = element.GetProperties();
        var children = element.GetLogicalChildrenObjects();

        string elementName = string.Empty;
        bool isRoot = element == Metadata.RootNode;

        if (isRoot)
        {
            elementName = "this";
        }
        if (!isRoot)
        {
            elementName = Metadata.NamedElementsMap.TryGetValue(element, out var named) ? named : GenerateNextElementName();
            TextGenerator.WriteLine($"var {elementName} = new {typeInfo.FullName}();");
        }
        
        var directives = element.Children.Where(x => x is AumlAstDirective).ToList();
        string key = string.Empty;
        foreach (var aumlAstNode in directives)
        {
            var directive = (AumlAstDirective)aumlAstNode;
            if (directive.Name == AumlDirectives.Key)
            {
                var textNode = directive.Value as AumlAstTextNode;
                key = textNode.Text;
                break;
            }
        }
        
        PushTypeContext(typeInfo);

        void ProcessProperties(IEnumerable<AumlAstPropertyNode> propertyNodes)
        {
            foreach (var prop in propertyNodes)
            {
                var propRef = (AumlAstPropertyReference)prop.Property;
                string symbolName = isRoot ? propRef.Name : $"{CurrentParent}.{propRef.Name}";
                // Property type it's always a type of the parent object for the current property.
                // It will always be one level up.
                // For ex. TargetType property -> ControlTemplate
                // This means that for the property "TargetType" inside the ControlTemplate class, the current parent type will be ControlTemplate
                var propertyType = GetEffectiveTargetType(typeInfo);
                var propertyName = propRef.Name;
                if (propRef.IsAttachedProperty)
                {
                    propertyType = typeContainer.Types.FirstOrDefault(x => x.Name == propRef.OwnerType.Name);
                    // We are faced with Attached property, so its really not a property, but a method with Get and Set prefixes,
                    // so we need to handle this case properly
                    propertyName = $"Get{propRef.Name}";
                    //symbolName = $"{propRef.OwnerType.GetFullTypeName()}.Set{propRef.Name}";
                }

                var resolvedMember = propertyType.GetMemberByName(propertyName);
                var resolvedType = resolvedMember.MemberType;

                if (resolvedType == null)
                {
                    diagnostics.ReportError(Metadata.ClassName, $"Unknown property {propRef.Name} on type {propertyType.FullName}");
                    continue;
                }

                if (resolvedType.MemberKind == ResolvedMemberKind.Event)
                {
                    TextGenerator.WriteLine($"{symbolName} += {prop.GetTextValue()};");
                    continue;
                }

                var value = prop.Values[0];

                if (value.TypeReference.GetFullTypeName() == Metadata.DefaultTypeContainer.ControlTemplate.FullName)
                {
                    var templateName = GenerateNextElementName("controlTemplate");
                    TextGenerator.WriteLine($"var {templateName} = new {value.TypeReference.GetFullTypeName()}(() =>");
                    TextGenerator.WriteLine("{");
                    TextGenerator.PushIndent();
                    var child = value.GetLogicalChildrenObjects().FirstOrDefault();
                    var childName = ProcessControlElements(child, diagnostics, false);
                    TextGenerator.WriteLine($"return {childName};");
                    TextGenerator.PopIndent();
                    TextGenerator.WriteLine("});");
                    
                    TextGenerator.WriteLine($"{CurrentParent}.{propRef.Name} = {templateName};");
                    
                    var templateProperties = value.GetProperties().ToList();
                    PushElement(templateName);

                    PushTypeContext(Metadata.DefaultTypeContainer.ControlTemplate);
                    
                    ProcessProperties(templateProperties);

                    PopTypeContext();
                    
                    PopElement();
                }
                else if (value is AumlAstMarkupExtensionNode extension && (extension.TypeReference.Name == "ResourceReference"))
                {
                    var key = extension.Arguments[0].Value.GetTextValue();
                    if (isResource && element.TypeReference.Namespace == "Adamantium.UI.Core.Resources")
                    {
                        TextGenerator.WriteLine($"{symbolName} = new {Metadata.DefaultTypeContainer.ResourceReference.FullName}(\"{key}\");");
                    }
                    else
                    {
                        TextGenerator.WriteLine($"{symbolName} = {Metadata.DefaultTypeContainer.ResourceResolver.FullName}.Resolve<{resolvedType.FullName}>(\"{key}\");");
                    }
                }
                else if (propRef.IsAttachedProperty)
                {
                    var textVale = prop.GetTextValue();
                    TextGenerator.WriteLine($"{propRef.OwnerType.GetFullTypeName()}.Set{propRef.Name}({CurrentParent}, {textVale});");
                }
                else if (resolvedType.IsCollection() && !resolvedType.HasAttribute("TypeParserAttribute"))
                {
                    TextGenerator.WriteLine($"{symbolName} = new {resolvedType.FullName}();");
                    foreach (var propertyValue in prop.Values)
                    {
                        string nestedName = ProcessNestedValue(propertyValue, diagnostics, isResource);
                        TextGenerator.WriteLine($"{symbolName}.Add({nestedName});");
                    }
                }
                else if(resolvedType.FullName == "System.Type")
                {
                    var type = Metadata.TypeResolver.ResolveByShortName(prop.GetTextValue());
                    if (type == null)
                    {
                        diagnostics.ReportError(Metadata.ClassName, $"Cannot find type {prop.GetTextValue()} for property {propRef.Name}");
                        continue;
                    }
                    TextGenerator.WriteLine($"{symbolName} = typeof({type.FullName});");
                }
                else if (value.IsTextNode())
                {
                    GenerateSimpleAssignment(symbolName, prop.GetTextValue(), resolvedType);
                }
                else
                {
                    foreach (var propertyValue in prop.Values)
                    {
                        string nestedName = ProcessNestedValue(propertyValue, diagnostics, isResource);
                        TextGenerator.WriteLine($"{symbolName} = {nestedName};");
                    }
                }
            }
        }

        void ProcessLogicalChildren(params AumlAstObjectNode[] childNodes)
        {
            foreach (var child in childNodes)
            {
                var childName = ProcessControlElements(child, diagnostics, isResource);

                // Тут нужно отдельно обработать кейс, когда мы работаем с контрол темплейтом или вообще с темплейтами,
                // потому что генератор не знает как корректно добавлять контент в него

                if (element.TypeReference.Name == "Style")
                {
                    TextGenerator.WriteLine($"{ParentName}.Add({childName});");
                }
                else if (typeInfo.ImplementsInterface("ITrigger"))
                {
                    TextGenerator.WriteLine($"{ParentName}.Add({childName});");
                }
                else if (typeInfo.ImplementsInterface("IContainer"))
                {
                    TextGenerator.WriteLine($"(({typeInfo.GetInterface("IContainer").FullName}){elementName}).AddOrSetChildComponent({childName});");
                }
                else if (typeInfo.FindPropertyWithAttribute("ContentAttribute", out var contentProp))
                {
                    if (contentProp.PropertyType.IsCollection())
                    {
                        TextGenerator.WriteLine($"{elementName}.{contentProp.Name}.Add({childName});");
                    }
                    else
                    {
                        TextGenerator.WriteLine($"{elementName}.{contentProp.Name} = {childName};");
                    }
                }
                else if (isResource && Metadata.RootEntityType != EntityType.ResourceDictionary)
                {
                    TextGenerator.WriteLine($"Add({childName});");
                }
            }
        }

        Action emitBody = () =>
        {
            ProcessProperties(properties.ToArray());
            ProcessLogicalChildren(children.ToArray());
        };

        if (isRoot)
        {
            emitBody();
        }
        else
        {
            WithElement(elementName, emitBody);
        }

        if (isResource && !string.IsNullOrEmpty(key))
        {
            TextGenerator.WriteLine($@"Add(""{key}"", {elementName});");
        }
        
        PopTypeContext();

        return elementName;
    }
    
    private void GenerateSimpleAssignment(string symbolName, string valueText, IResolvedType member)
    {
        if (member.TypeKind == ResolvedTypeKind.Enum)
        {
            TextGenerator.WriteLine($"{symbolName} = {member.FullName}.{valueText};");
            return;
        }
        
        switch (member.SpecialType)
        {
            case ResolvedSpecialType.System_String:
                TextGenerator.WriteLine($"{symbolName} = \"{valueText}\";");
                break;
            case ResolvedSpecialType.System_Double:
            case ResolvedSpecialType.System_Single:
            case ResolvedSpecialType.System_Decimal:
            case ResolvedSpecialType.System_SByte:
            case ResolvedSpecialType.System_Byte:
            case ResolvedSpecialType.System_Int16:
            case ResolvedSpecialType.System_Int32:
            case ResolvedSpecialType.System_Int64:
            case ResolvedSpecialType.System_UInt16:
            case ResolvedSpecialType.System_UInt32:
            case ResolvedSpecialType.System_UInt64:
                TextGenerator.WriteLine($"{symbolName} = {valueText};");
                break;
            case ResolvedSpecialType.System_Boolean:
                TextGenerator.WriteLine($"{symbolName} = {valueText.ToLowerInvariant()};");
                break;
            case ResolvedSpecialType.System_Object:
                TextGenerator.WriteLine($"{symbolName} = \"{valueText}\";");
                break;
            default:
                TextGenerator.WriteLine($"{symbolName} = {Metadata.DefaultTypeContainer.TypeParser.FullName}.Parse<{member.FullName}>({Quote(valueText)});");
                break;
        }
    }
    
    private string Quote(string str) => $"\"{str}\"";
    
    private string ProcessNestedValue(IAumlAstNode value, IDiagnosticSink diagnostics, bool isResource)
    {
        if (value is AumlAstObjectNode objNode)
        {
            return ProcessControlElements(objNode, diagnostics, isResource);
        }

        if (value is AumlAstMarkupExtensionLiteral literal)
        {
            var type = Metadata.TypeResolver.GetResolvedAssembly(literal.TypeReference.Assembly)
                .Types.First(x => x.Name == literal.TypeReference.Name);
            var name = GenerateNextElementName();
            TextGenerator.WriteLine($"var {name} = new {type.FullName}();");
            return name;
        }

        if (value is AumlAstMarkupExtensionNode extension)
        {
            var type = Metadata.TypeResolver.GetResolvedAssembly(extension.TypeReference.Assembly)
                .Types.First(x => x.Name == extension.TypeReference.Name);
            var name = GenerateNextElementName();
            TextGenerator.WriteLine($"var {name} = new {type.FullName}();");

            foreach (var arg in extension.Arguments)
            {
                var target = $"{name}.{arg.Name}";
                if (arg.Value.IsTextNode())
                {
                    //GenerateSimpleAssignment(target, arg.Value.GetTextValue(), new StubResolvedMember(target, arg.Value.GetTextValue()));
                    GenerateSimpleAssignment(target, arg.Value.GetTextValue(), new StubResolvedMember(target, arg.Value.GetTextValue()).MemberType);
                }
                else
                {
                    var nested = ProcessNestedValue(arg.Value, diagnostics, isResource);
                    TextGenerator.WriteLine($"{target} = {nested};");
                }
            }

            return name;
        }

        return "null";
    }
    
    private class StubResolvedMember : IResolvedMember
    {
        public string Name { get; }
        public string DummyValue { get; }
        public StubResolvedMember(string name, string value)
        {
            Name = name;
            DummyValue = value;
        }
        public string FullName => Name;
        public bool HasAttribute(string attributeMetadataName)
        {
            return false;
        }

        public ResolvedMemberKind MemberKind => ResolvedMemberKind.Property;
        public IResolvedType MemberType => new StubResolvedType();
        public IResolvedType DeclaringType => new StubResolvedType();
    }

    private class StubResolvedType : IResolvedType
    {
        public string Name => "string";
        public string Namespace { get; }
        public string FullName => "System.String";
        public string AssemblyName => "System";
        public bool IsNamedType => false;
        public bool IsGenericType => false;
        public bool InheritsFrom(string baseTypeName) => false;

        public bool HasAttribute(string attributeName) => false;

        public IResolvedAttribute GetAttribute(string fullName) => null;

        public IEnumerable<IResolvedAttribute> GetAttributes() => [];

        public EntityType EntityType { get; }
        public IEnumerable<IResolvedType> TypeArguments { get; }
        public IResolvedType BaseType { get; }
        public IEnumerable<IResolvedMember> Members { get; }
        public IResolvedMember GetMemberByName(string memberName) => null;

        public List<IResolvedProperty> GetAllProperties() => [];

        public bool IsAssignableTo(string fullName) => false;

        public bool ImplementsInterface(string name) => false;
        public IResolvedType GetInterface(string interfaceName) => null;

        public bool InheritsFromMarkupExtension(string fullyQualifiedName) => false;

        public bool FindPropertyWithAttribute(string attributeFullName, out IResolvedProperty property)
        {
            property = null;
            return false;
        }

        public bool IsAssignableFrom(IResolvedType other) => false;

        public bool IsCollection()
        {
            return false;
        }

        public ResolvedSpecialType SpecialType => ResolvedSpecialType.System_String;
        public ResolvedTypeKind TypeKind => ResolvedTypeKind.Class;
        public ResolvedMemberKind MemberKind => ResolvedMemberKind.Property;
    }
}