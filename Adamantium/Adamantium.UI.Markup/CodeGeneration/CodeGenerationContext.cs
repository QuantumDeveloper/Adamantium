using Adamantium.Core;
using Adamantium.UI.Markup.AST;
using Adamantium.UI.Markup.AST.MarkupExtension;

namespace Adamantium.UI.Markup.CodeGeneration;

public class CodeGenerationContext
{
    public string ParentName { get; private set; }
    public Stack<string> ElementStack { get; }
    private readonly Stack<string> _templateStack = new();

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
    
    private void PushTemplate(string templateVar) => _templateStack.Push(templateVar);
    private void PopTemplate() { if (_templateStack.Count > 0) _templateStack.Pop(); }
    private string CurrentTemplate => _templateStack.Count > 0 ? _templateStack.Peek() : null;

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
        if (typeContainer == null)
        {
            diagnostics.ReportError(Metadata.ClassName, $"Could not resolve assembly '{element.TypeReference.Assembly}' for element '{element.TypeReference.Name}' (namespace '{element.TypeReference.Namespace}', resolved={element.TypeReference.IsResolved}).");
            return string.Empty;
        }
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
            var isNamed = Metadata.NamedElementsMap.TryGetValue(element, out var named);
            elementName = isNamed
                ? named
                : GenerateNextElementName();
            // A named element in a control file has a backing field (declared in GenerateControlFile) — assign to
            // the field, otherwise a local var would shadow it (the field would stay null). Inside a ControlTemplate and in
            // theme/resource files there is no field → local var (+ RegisterName for the template below).
            var hasBackingField = isNamed
                && CurrentTemplate == null
                && EntityType is not (EntityType.ResourceDictionary or EntityType.StyleSet or EntityType.Theme);
            var declaration = hasBackingField ? elementName : $"var {elementName}";
            TextGenerator.WriteLine($"{declaration} = new {typeInfo.FullName}();");
            if (isNamed && CurrentTemplate != null)
            {
                TextGenerator.WriteLine($"{CurrentTemplate}.RegisterName(\"{named}\", {elementName});");
            }
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
                    diagnostics.ReportError(Metadata.ClassName,
                        $"Unknown property {propRef.Name} on type {propertyType.FullName}");
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
                    var templateTypeName = value.TypeReference.GetFullTypeName();
                    
                    var templateBuilderMethod = $"Build_{templateName}";

                    TextGenerator.WriteLine($"var {templateName} = new {templateTypeName}({templateBuilderMethod});");
                    TextGenerator.WriteLine($"{CurrentParent}.{propRef.Name} = {templateName};");
                    
                    TextGenerator.NewLine();
                    TextGenerator.WriteLine($"{Metadata.DefaultTypeContainer.TemplateResult.FullName} {templateBuilderMethod}()");
                    TextGenerator.WriteOpenBraceAndIndent();
                    
                    TextGenerator.WriteLine($"var result = new {Metadata.DefaultTypeContainer.TemplateResult.FullName}();");
                    
                    PushTemplate("result");

                    var child = value.GetLogicalChildrenObjects().FirstOrDefault();
                    
                    var childName = ProcessControlElements(child, diagnostics, false);
                    TextGenerator.WriteLine($"result.RootComponent = {childName};");
                    
                    var templateProperties = value.GetProperties().ToList();
                    WithElement("result", () => // Temporary Context for object "result"
                    {
                        PushTypeContext(Metadata.DefaultTypeContainer.ControlTemplate);
                        foreach (var templateProp in templateProperties)
                        {
                            var templatePropRef = (AumlAstPropertyReference)templateProp.Property;
                            if (templatePropRef.Name == "Triggers")
                            {
                                foreach (var triggerValueNode in templateProp.Values)
                                {
                                    if (triggerValueNode is AumlAstObjectNode triggerObjectNode)
                                    {
                                        var triggerVariableName = ProcessControlElements(triggerObjectNode, diagnostics, isResource);
                                        TextGenerator.WriteLine($"result.Triggers.Add({triggerVariableName});");
                                    }
                                }
                            }
                        }
                        PopTypeContext();
                    });

                    PopTemplate();
                    
                    TextGenerator.WriteLine($"return result;");
                    TextGenerator.UnindentAndWriteCloseBrace();
                    continue;
                }
                else if (value is AumlAstMarkupExtensionLiteral literal)
                {
                    var typeContainer = Metadata.TypeResolver.GetResolvedAssembly(literal.TypeReference.Assembly);
                    var typeInfo = typeContainer.Types.FirstOrDefault(x => x.Name == literal.TypeReference.Name);
                    var literalName = GenerateNextElementName();
                    TextGenerator.WriteLine($"var {literalName} = new {typeInfo.FullName}();");
                }
                else if (value is AumlAstMarkupExtensionNode extension)
                {
                    switch (extension.TypeReference.Name)
                    {
                        case "ResourceReference":
                        {
                            var key = extension.Arguments[0].Value.GetTextValue();
                            if (isResource && element.TypeReference.Namespace == "Adamantium.UI.Core.Resources")
                            {
                                TextGenerator.WriteLine(
                                    $"{symbolName} = new {Metadata.DefaultTypeContainer.ResourceReference.FullName}(\"{key}\");");
                            }
                            else
                            {
                                TextGenerator.WriteLine(
                                    $"{symbolName} = {Metadata.DefaultTypeContainer.ResourceResolver.FullName}.Resolve<{resolvedType.FullName}>(\"{key}\");");
                            }

                            break;
                        }
                        case "TemplateBinding":
                        {
                            var tbVar = GenerateNextElementName("tb");
                            TextGenerator.WriteLine($"var {tbVar} = new {extension.TypeReference.GetFullTypeName()}();");

                            foreach (var argument in extension.Arguments)
                            {
                                if (string.IsNullOrEmpty(argument.Name))
                                {
                                    TextGenerator.WriteLine($"{tbVar}.Path = \"{argument.Value.GetTextValue()}\";");
                                }
                                else
                                {
                                    TextGenerator.WriteLine(
                                        $"{tbVar}.{argument.Name} = {argument.Value.TypeReference.GetFullTypeName()}.{argument.Value.GetTextValue()};");
                                }
                            }

                            if (CurrentTemplate == null)
                            {
                                diagnostics.ReportError(Metadata.ClassName,
                                    "TemplateBinding can only be used inside ControlTemplate.");
                            }
                            else
                            {
                                TextGenerator.WriteLine(
                                    $"{CurrentTemplate}.AddTemplateBinding({CurrentParent}, \"{propRef.Name}\", {tbVar});");
                            }

                            break;
                        }
                        default:
                            string nestedName = ProcessNestedValue(extension, diagnostics, isResource);
                            if (propRef.IsAttachedProperty)
                            {
                                TextGenerator.WriteLine(
                                    $"{propRef.OwnerType.GetFullTypeName()}.Set{propRef.Name}({elementName}, {nestedName});");
                            }
                            else
                            {
                                TextGenerator.WriteLine(
                                    $"{propRef.OwnerType.GetFullTypeName()}.{propRef.Name} = {nestedName};");
                            }
                            break;
                    }
                }
                else if (propRef.IsAttachedProperty)
                {
                    var textVale = prop.GetTextValue();
                    TextGenerator.WriteLine(
                        $"{propRef.OwnerType.GetFullTypeName()}.Set{propRef.Name}({CurrentParent}, {textVale});");
                }
                else if (resolvedType.IsCollection() && !resolvedType.HasAttribute("TypeParserAttribute"))
                {
                    if (resolvedMember.MemberKind == ResolvedMemberKind.Property && resolvedMember.HasSetter())
                    {
                        TextGenerator.WriteLine($"{symbolName} = new {resolvedType.FullName}();");
                    }
                    
                    foreach (var propertyValue in prop.Values)
                    {
                        string nestedName = ProcessNestedValue(propertyValue, diagnostics, isResource);
                        TextGenerator.WriteLine($"{symbolName}.Add({nestedName});");
                    }
                }
                else if (resolvedType.FullName == "System.Type")
                {
                    var type = Metadata.TypeResolver.Resolve(prop.GetFullTypeValue());
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
            var resolvedAssembly = Metadata.TypeResolver.ResolveAssembly(literal.TypeReference.Assembly);
            var type = resolvedAssembly.Types.First(x => x.Name == literal.TypeReference.Name);
            var name = GenerateNextElementName();
            GenerateSimpleAssignment($"var {name}", literal.Text, type);
            return name;
        }

        if (value is AumlAstMarkupExtensionNode extension)
        {
            var type = Metadata.TypeResolver.ResolveAssembly(extension.TypeReference.Assembly)
                .Types.First(x => x.Name == extension.TypeReference.Name);
            var name = GenerateNextElementName();
            TextGenerator.WriteLine($"var {name} = new {type.FullName}();");

            foreach (var arg in extension.Arguments)
            {
                var target = $"{name}.{arg.Name}";

                if (arg.Value is AumlAstTypeReferenceValueNode typeValueNode)
                {
                    var fullTypeName = typeValueNode.TypeReference.GetFullTypeName();
                    TextGenerator.WriteLine($"{target} = typeof({fullTypeName});");
                }
                else if (arg.Value is IAumlAstMarkupExtensionLiteral or IAumlAstMarkupExtensionNode || arg.Value is AumlAstObjectNode)
                {
                    var nested = ProcessNestedValue(arg.Value, diagnostics, isResource);
                    TextGenerator.WriteLine($"{target} = {nested};");
                }
                else
                {
                    var resolvedMember = Metadata.TypeResolver.ResolveAssembly(arg.Value.TypeReference.Assembly).Types
                        .First(x => x.Name == arg.Value.TypeReference.Name);
                    GenerateSimpleAssignment(target, arg.Value.GetTextValue(), resolvedMember);
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

        public bool HasSetter()
        {
            return true;
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