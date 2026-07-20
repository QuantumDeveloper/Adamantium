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

        // A UiTemplate (DataTemplate/ControlTemplate/...) as a standalone element - e.g. a keyed entry in a
        // ResourceDictionary - carries its content ONLY through a builder Func. Build it via the builder form (below),
        // not as a plain `new()` with orphaned children, which produces an empty template that renders nothing.
        var isTemplate = typeInfo != null
                         && Metadata.DefaultTypeContainer.UiTemplate != null
                         && typeInfo.InheritsFrom(Metadata.DefaultTypeContainer.UiTemplate.FullName);

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

            // A VALUE-TYPE element that carries its value as text content (<Color x:Key="X">#33FFFFFF</Color>): a struct/enum
            // has no properties to new()+set - its value IS the text - so PARSE the text into the type instead of constructing
            // it empty (which silently produced a default and, as a resource, a dropped entry). This is what lets a
            // Color/Thickness/Vector2/... be a keyed resource that {ResourceReference} then applies to a like-typed property
            // on a UI element. (It does NOT reach a non-element target like a GradientStop.Color - {ResourceReference} resolves
            // through SetDeferred, which needs an IFundamentalUIComponent.)
            var valueText = typeInfo is { TypeKind: ResolvedTypeKind.Struct or ResolvedTypeKind.Enum }
                ? element.Children.OfType<AumlAstTextNode>().FirstOrDefault()?.Text?.Trim()
                : null;

            if (isTemplate)
            {
                EmitTemplateBuilder(declaration, typeInfo.FullName, element, isResource, diagnostics);
            }
            else if (!string.IsNullOrEmpty(valueText))
            {
                TextGenerator.WriteLine($"{declaration} = {Metadata.DefaultTypeContainer.TypeParser.FullName}.Parse<{typeInfo.FullName}>({Quote(valueText)});");
            }
            else
            {
                TextGenerator.WriteLine($"{declaration} = new {typeInfo.FullName}();");
                // Carry the x:Name at runtime too, so {Binding ..., ElementName=X} resolves it by walking the tree (a view
                // exposes named elements as fields; Name is what an ElementName binding matches on). Only for types that
                // HAVE a Name property - a named non-UI object (e.g. a Transform an animation targets) does not.
                if (isNamed && typeInfo.GetMemberByName("Name") is { MemberType: not null })
                    TextGenerator.WriteLine($"{elementName}.Name = \"{named}\";");
            }
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
                    // An attached property's owner (Grid, ToolTipService, ResourceContext, ...) can live in a DIFFERENT
                    // assembly than the element it's set on, so resolve it from ITS OWN assembly - not the element's
                    // container, which only happened to work while the owner shared the element's assembly (e.g. Grid.Row
                    // on a Controls element). ResourceContext.Source on a View (owner in Adamantium.UI.Core) NRE'd here.
                    var ownerContainer = Metadata.TypeResolver.GetResolvedAssembly(propRef.OwnerType.Assembly);
                    propertyType = ownerContainer?.Types.FirstOrDefault(x => x.Name == propRef.OwnerType.Name)
                                   ?? typeContainer.Types.FirstOrDefault(x => x.Name == propRef.OwnerType.Name);
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

                
                var valueTypeName = value.TypeReference.GetFullTypeName();
                var valueResolvedType = Metadata.TypeResolver.Resolve(valueTypeName);

                // A ResourceDictionary-typed property authored as INLINE keyed children (ResourceContext.Resources):
                //   <X><ResourceContext.Resources><SolidColorBrush x:Key="Accent"/></ResourceContext.Resources></X>
                // Build a dictionary instance, add each x:Key'd child to it, and hand it to the setter. The child build
                // runs with isResource:false so it does NOT emit the resource-file's trailing keyed Add - we add here,
                // into THIS dictionary. Local + tree-scoped + live via {ObservableResource}.
                if (Metadata.DefaultTypeContainer.ResourceDictionary != null && resolvedType != null
                    && (resolvedType.FullName == Metadata.DefaultTypeContainer.ResourceDictionary.FullName
                        || resolvedType.InheritsFrom(Metadata.DefaultTypeContainer.ResourceDictionary.FullName))
                    && !value.IsTextNode())
                {
                    var rdVar = GenerateNextElementName("res");
                    TextGenerator.WriteLine($"var {rdVar} = new {Metadata.DefaultTypeContainer.ResourceDictionary.FullName}();");
                    foreach (var entry in prop.Values.OfType<AumlAstObjectNode>())
                    {
                        var entryKey = GetKeyDirective(entry);
                        var entryName = ProcessControlElements(entry, diagnostics, isResource: false);
                        if (!string.IsNullOrEmpty(entryKey))
                            TextGenerator.WriteLine($@"{rdVar}.Add(""{entryKey}"", {entryName});");
                        else
                            diagnostics.ReportError(Metadata.ClassName,
                                $"An inline resource in {propRef.Name} needs an x:Key.");
                    }

                    if (propRef.IsAttachedProperty)
                    {
                        var setTarget = isRoot ? "this" : CurrentParent;
                        TextGenerator.WriteLine($"{propRef.OwnerType.GetFullTypeName()}.Set{propRef.Name}({setTarget}, {rdVar});");
                    }
                    else
                    {
                        TextGenerator.WriteLine($"{symbolName} = {rdVar};");
                    }
                    continue;
                }

                // ControlTemplate, DataTemplate (ItemTemplate/ContentTemplate) and ItemsPanelTemplate all derive UiTemplate
                // and take a Func<TemplateResult> builder: emit a builder method that constructs the template's tree and
                // returns a TemplateResult, then `new <Template>(builder)`. Without this a DataTemplate would be emitted as
                // an empty `new DataTemplate()` whose Build() NREs (no builder, no container).
                if (valueResolvedType != null
                    && Metadata.DefaultTypeContainer.UiTemplate != null
                    && valueResolvedType.InheritsFrom(Metadata.DefaultTypeContainer.UiTemplate.FullName))
                {
                    var templateName = GenerateNextElementName("template");
                    EmitTemplateBuilder($"var {templateName}", valueTypeName, value, isResource, diagnostics);
                    // A template assigned to a PART's property inside a ControlTemplate goes to TEMPLATE priority (not the CLR
                    // setter's LOCAL), so a theme's own Template setter/trigger can still override it - same rule as every
                    // other templated-part property. Outside a template it stays a plain assignment.
                    if (CurrentTemplate != null && !propRef.IsAttachedProperty && typeInfo != null && typeInfo.ImplementsInterface("IAdamantiumComponent"))
                    {
                        var target = isRoot ? "this" : CurrentParent;
                        TextGenerator.WriteLine($"{target}.SetValue(\"{propRef.Name}\", {templateName}, Adamantium.UI.Core.ValuePriority.Template);");
                    }
                    else
                    {
                        TextGenerator.WriteLine($"{CurrentParent}.{propRef.Name} = {templateName};");
                    }
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
                            // A Setter/trigger value (its target type lives in Adamantium.UI.Core.Resources) is stored as
                            // a ResourceReference MARKER, resolved later by Setter.Apply / the trigger activator - which
                            // hold the styled element, so the lookup is TREE-SCOPED (a Local resource resolves only within
                            // the owner's subtree). True even outside a resource file (e.g. an inline <Style> in a View),
                            // so this is NOT gated on isResource.
                            if (element.TypeReference.Namespace == "Adamantium.UI.Core.Resources")
                            {
                                TextGenerator.WriteLine(
                                    $"{symbolName} = new {Metadata.DefaultTypeContainer.ResourceReference.FullName}(\"{key}\");");
                            }
                            else
                            {
                                // A direct element property: the element isn't in the tree yet, so defer - resolve
                                // Theme/Global now and upgrade to a tree-scoped Local hit once the element attaches (see
                                // ResourceResolver.SetDeferred). Keeps Local resources out of reach of an eager,
                                // context-free resolve.
                                var target = isRoot ? "this" : CurrentParent;
                                TextGenerator.WriteLine(
                                    $"{Metadata.DefaultTypeContainer.ResourceResolver.FullName}.SetDeferred({target}, \"{propRef.Name}\", \"{key}\");");
                            }

                            break;
                        }
                        case "ThemeResource":
                        {
                            // {ThemeResource Key} -> a live binding to the active theme's property. As a Setter value
                            // it's stored as the marker (Setter.Apply -> SetBinding); as a normal property it's bound now.
                            var key = extension.Arguments[0].Value.GetTextValue();
                            if (isResource && element.TypeReference.Namespace == "Adamantium.UI.Core.Resources")
                            {
                                TextGenerator.WriteLine(
                                    $"{symbolName} = new {Metadata.DefaultTypeContainer.ThemeResource.FullName}(\"{key}\");");
                            }
                            else
                            {
                                var trVar = GenerateNextElementName("tr");
                                TextGenerator.WriteLine(
                                    $"var {trVar} = new {Metadata.DefaultTypeContainer.ThemeResource.FullName}(\"{key}\");");
                                var trTarget = isRoot ? "this" : CurrentParent;
                                TextGenerator.WriteLine($"{trVar}.Apply({trTarget}, \"{propRef.Name}\");");
                            }

                            break;
                        }
                        case "ObservableResource":
                        {
                            // {ObservableResource Key} -> a LIVE, tree-scoped keyed-resource reference (re-resolves on a
                            // theme swap / dictionary load-unload). As a Setter/trigger value it's stored as the marker
                            // (Setter.Apply / the trigger activator call Apply); as a normal property it's connected now.
                            var key = extension.Arguments[0].Value.GetTextValue();
                            if (isResource && element.TypeReference.Namespace == "Adamantium.UI.Core.Resources")
                            {
                                TextGenerator.WriteLine(
                                    $"{symbolName} = new {Metadata.DefaultTypeContainer.ObservableResource.FullName}(\"{key}\");");
                            }
                            else
                            {
                                var orVar = GenerateNextElementName("or");
                                TextGenerator.WriteLine(
                                    $"var {orVar} = new {Metadata.DefaultTypeContainer.ObservableResource.FullName}(\"{key}\");");
                                var orTarget = isRoot ? "this" : CurrentParent;
                                TextGenerator.WriteLine($"{orVar}.Apply({orTarget}, \"{propRef.Name}\");");
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

                            if (isResource && element.TypeReference.Namespace == "Adamantium.UI.Core.Resources")
                            {
                                // {TemplateBinding X} as a Setter value (a trigger overriding a part). Store it as the
                                // marker; the trigger resolves it against the templated parent when it fires - see
                                // PropertyTriggerActivator.ApplyTemplateBinding. (Distinct from a normal template
                                // element binding below, which wires up immediately at template build.)
                                TextGenerator.WriteLine($"{symbolName} = {tbVar};");
                            }
                            else if (CurrentTemplate == null)
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
                        case "Binding":
                        case "MultiBinding":
                        {
                            var bindingVar = EmitBinding(extension, diagnostics, isResource);
                            if (IsBindingTypedProperty(resolvedType))
                            {
                                // The property itself HOLDS a binding object (e.g. DataTrigger.Binding) - assign it.
                                TextGenerator.WriteLine($"{symbolName} = {bindingVar};");
                            }
                            else
                            {
                                // Ordinary property: establish a live binding on it.
                                var bindingTarget = isRoot ? "this" : CurrentParent;
                                TextGenerator.WriteLine(EmitSetBinding(bindingTarget, propRef, bindingVar));
                            }
                            break;
                        }
                        case "Ancestor":
                        {
                            // {Ancestor Type, Path, Skip=, Name=, Stop=, Logical=, Mode=} -> resolve the ancestor type
                            // (short name -> typeof, preserving is-a) and wire a live ancestor binding via .Apply, which
                            // (re)resolves against the tree on attach. Works in a ControlTemplate, a DataTemplate, or on a
                            // plain element - target is just the current element.
                            var acVar = GenerateNextElementName("anc");
                            TextGenerator.WriteLine($"var {acVar} = new {extension.TypeReference.GetFullTypeName()}();");

                            var positional = 0;
                            foreach (var argument in extension.Arguments)
                            {
                                var text = argument.Value.GetTextValue();
                                if (string.IsNullOrEmpty(argument.Name))
                                {
                                    if (positional == 0)
                                    {
                                        var resolved = Metadata.TypeResolver.ResolveByShortName(text);
                                        if (resolved == null)
                                            diagnostics.ReportError(Metadata.ClassName, $"Ancestor: type '{text}' could not be resolved.");
                                        else
                                            TextGenerator.WriteLine($"{acVar}.AncestorType = typeof({resolved.FullName});");
                                    }
                                    else if (positional == 1)
                                    {
                                        TextGenerator.WriteLine($"{acVar}.Path = \"{text}\";");
                                    }
                                    positional++;
                                }
                                else
                                {
                                    switch (argument.Name)
                                    {
                                        case "Path": TextGenerator.WriteLine($"{acVar}.Path = \"{text}\";"); break;
                                        case "Skip": TextGenerator.WriteLine($"{acVar}.Skip = {text};"); break;
                                        case "Name": TextGenerator.WriteLine($"{acVar}.Name = \"{text}\";"); break;
                                        case "Logical": TextGenerator.WriteLine($"{acVar}.Logical = {text.ToLowerInvariant()};"); break;
                                        case "Stop":
                                        {
                                            var st = Metadata.TypeResolver.ResolveByShortName(text);
                                            if (st == null)
                                                diagnostics.ReportError(Metadata.ClassName, $"Ancestor: Stop type '{text}' could not be resolved.");
                                            else
                                                TextGenerator.WriteLine($"{acVar}.Stop = typeof({st.FullName});");
                                            break;
                                        }
                                        case "Mode":
                                            TextGenerator.WriteLine($"{acVar}.Mode = {argument.Value.TypeReference.GetFullTypeName()}.{text};");
                                            break;
                                        case "Converter":
                                            var acConv = EmitValueExpression(argument.Value, ValueConverterFqn, diagnostics, isResource);
                                            if (acConv != null) TextGenerator.WriteLine($"{acVar}.Converter = {acConv};");
                                            else diagnostics.ReportError(Metadata.ClassName, "Ancestor: Converter must be a resource or converter markup extension.");
                                            break;
                                        case "ConverterParameter":
                                            TextGenerator.WriteLine($"{acVar}.ConverterParameter = {EmitValueExpression(argument.Value, "object", diagnostics, isResource) ?? $"\"{text}\""};");
                                            break;
                                        case "FallbackValue":
                                            TextGenerator.WriteLine($"{acVar}.FallbackValue = {EmitValueExpression(argument.Value, "object", diagnostics, isResource) ?? $"\"{text}\""};");
                                            break;
                                        case "TargetNullValue":
                                            TextGenerator.WriteLine($"{acVar}.TargetNullValue = {EmitValueExpression(argument.Value, "object", diagnostics, isResource) ?? $"\"{text}\""};");
                                            break;
                                        default:
                                            diagnostics.ReportError(Metadata.ClassName, $"Ancestor: unknown argument '{argument.Name}'.");
                                            break;
                                    }
                                }
                            }

                            if (isResource && element.TypeReference.Namespace == "Adamantium.UI.Core.Resources")
                            {
                                // {Ancestor ...} as a Setter/trigger value: store the marker; Setter.Apply (or the trigger
                                // activator) calls .Apply against each matched element when the setter fires.
                                TextGenerator.WriteLine($"{symbolName} = {acVar};");
                            }
                            else
                            {
                                var acTarget = isRoot ? "this" : CurrentParent;
                                TextGenerator.WriteLine($"{acVar}.Apply({acTarget}, \"{propRef.Name}\");");
                            }
                            break;
                        }
                        case "Self":
                        {
                            // {Self Path, Mode=} -> bind a target property to another property on the SAME element.
                            var slfVar = GenerateNextElementName("self");
                            TextGenerator.WriteLine($"var {slfVar} = new {extension.TypeReference.GetFullTypeName()}();");
                            foreach (var argument in extension.Arguments)
                            {
                                var text = argument.Value.GetTextValue();
                                if (string.IsNullOrEmpty(argument.Name) || argument.Name == "Path")
                                    TextGenerator.WriteLine($"{slfVar}.Path = \"{text}\";");
                                else switch (argument.Name)
                                {
                                    case "Mode":
                                        TextGenerator.WriteLine($"{slfVar}.Mode = {argument.Value.TypeReference.GetFullTypeName()}.{text};");
                                        break;
                                    case "Converter":
                                        var slfConv = EmitValueExpression(argument.Value, ValueConverterFqn, diagnostics, isResource);
                                        if (slfConv != null) TextGenerator.WriteLine($"{slfVar}.Converter = {slfConv};");
                                        else diagnostics.ReportError(Metadata.ClassName, "Self: Converter must be a resource or converter markup extension.");
                                        break;
                                    case "ConverterParameter":
                                        TextGenerator.WriteLine($"{slfVar}.ConverterParameter = {EmitValueExpression(argument.Value, "object", diagnostics, isResource) ?? $"\"{text}\""};");
                                        break;
                                    case "FallbackValue":
                                        TextGenerator.WriteLine($"{slfVar}.FallbackValue = {EmitValueExpression(argument.Value, "object", diagnostics, isResource) ?? $"\"{text}\""};");
                                        break;
                                    case "TargetNullValue":
                                        TextGenerator.WriteLine($"{slfVar}.TargetNullValue = {EmitValueExpression(argument.Value, "object", diagnostics, isResource) ?? $"\"{text}\""};");
                                        break;
                                    default:
                                        diagnostics.ReportError(Metadata.ClassName, $"Self: unknown argument '{argument.Name}'.");
                                        break;
                                }
                            }
                            if (isResource && element.TypeReference.Namespace == "Adamantium.UI.Core.Resources")
                            {
                                TextGenerator.WriteLine($"{symbolName} = {slfVar};");
                            }
                            else
                            {
                                var slfTarget = isRoot ? "this" : CurrentParent;
                                TextGenerator.WriteLine($"{slfVar}.Apply({slfTarget}, \"{propRef.Name}\");");
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
                    // Convert the literal to the attached property's value type (quote strings, resolve enums, parse the
                    // rest) exactly like a regular property. Emitting the raw text only ever compiled for the int-typed
                    // ones (Grid.Column="0"); a string like ToolTip="hint" broke as bare C# identifiers.
                    var expr = BuildValueExpression(prop.GetTextValue(), resolvedType);
                    TextGenerator.WriteLine(
                        $"{propRef.OwnerType.GetFullTypeName()}.Set{propRef.Name}({CurrentParent}, {expr});");
                }
                // A collection populated by CHILD ELEMENTS (<Grid.RowDefinitions><RowDefinition/>...) -> new + Add per
                // child. The STRING form (StrokeDashArray="10,6", RowDefinitions="Auto,*") is a text node, so `!IsTextNode`
                // excludes it here and it falls through to the text-node branch, which routes it through the TypeParser.
                // Key off the value being elements, NOT off the presence of a parser: a collection can support BOTH forms
                // (RowDefinitions has a TypeParser AND takes <RowDefinition> children) - excluding parser-typed collections
                // sent the child form to the plain-assignment branch (RowDefinitions = a single RowDefinition -> CS0029).
                else if (resolvedType.IsCollection() && !value.IsTextNode())
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
                    // Inside a ControlTemplate, apply a PART's literal at TEMPLATE priority instead of the CLR setter's
                    // LOCAL priority, so a theme trigger targeting that part can override it. WPF precedence is
                    // Template < Trigger < Local; emitting "part.Prop = value" makes it Local, which outranks the
                    // trigger and froze e.g. a scrollbar's IsHitTestVisible="False" so the hover trigger could never
                    // arm it. The cast keeps the value the property's exact type before it is boxed for SetValue.
                    // Guard on IAdamantiumComponent: a ControlTemplate also hosts plain CLR objects in its Triggers
                    // (Setter/Animation/KeyFrame/PropertyTrigger) whose attributes are ordinary CLR setters - those have
                    // no SetValue and must stay plain assignments.
                    if (CurrentTemplate != null && typeInfo.ImplementsInterface("IAdamantiumComponent"))
                    {
                        var target = isRoot ? "this" : CurrentParent;
                        var expr = BuildValueExpression(prop.GetTextValue(), resolvedType);
                        TextGenerator.WriteLine(
                            $"{target}.SetValue(\"{propRef.Name}\", ({resolvedType.FullName})({expr}), Adamantium.UI.Core.ValuePriority.Template);");
                    }
                    else
                    {
                        GenerateSimpleAssignment(symbolName, prop.GetTextValue(), resolvedType);
                    }
                }
                else
                {
                    foreach (var propertyValue in prop.Values)
                    {
                        // <X.Y><Binding/></X.Y> or <X.Y><MultiBinding>...</MultiBinding></X.Y> -> a live binding, unless the
                        // property itself holds a binding object (e.g. DataTrigger.Binding), in which case assign it.
                        if (IsBindingNode(propertyValue))
                        {
                            var bindingVar = EmitBinding(propertyValue, diagnostics, isResource);
                            if (IsBindingTypedProperty(resolvedType))
                            {
                                TextGenerator.WriteLine($"{symbolName} = {bindingVar};");
                            }
                            else
                            {
                                var bindingTarget = isRoot ? "this" : CurrentParent;
                                TextGenerator.WriteLine(EmitSetBinding(bindingTarget, propRef, bindingVar));
                            }
                            continue;
                        }
                        string nestedName = ProcessNestedValue(propertyValue, diagnostics, isResource);
                        // Same rule as the text-node branch above: a PART's property set inside a ControlTemplate lands at
                        // TEMPLATE priority, not the CLR setter's LOCAL - so a theme trigger/style can override it. Nested
                        // object values (e.g. <X.Template><ControlTemplate/>) used to skip this and go to Local.
                        if (CurrentTemplate != null && !propRef.IsAttachedProperty && typeInfo.ImplementsInterface("IAdamantiumComponent"))
                        {
                            var target = isRoot ? "this" : CurrentParent;
                            TextGenerator.WriteLine($"{target}.SetValue(\"{propRef.Name}\", {nestedName}, Adamantium.UI.Core.ValuePriority.Template);");
                        }
                        else
                        {
                            TextGenerator.WriteLine($"{symbolName} = {nestedName};");
                        }
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
                else if (typeInfo.FindPropertyWithAttribute("Adamantium.UI.Core.ContentAttribute", out var contentProp))
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
            // A template's whole body (child tree + Triggers) is emitted into its builder by EmitTemplateBuilder above,
            // so there are no further properties/children to process here.
            if (isTemplate) return;
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

    // Emits `{targetVar} = new {templateType}(Build_xxx);` plus the local builder function that constructs the template's
    // visual tree and returns a TemplateResult (RootComponent = the single child, plus any Triggers). A UiTemplate
    // (DataTemplate/ControlTemplate/ItemsPanelTemplate) carries its content ONLY through this builder, so it's used both
    // when a template is a PROPERTY value (ItemTemplate/Template/...) AND when it's a KEYED RESOURCE entry - a
    // DataTemplate stored in a ResourceDictionary must build its builder too, else it resolves to an empty template that
    // renders nothing (a bare `new DataTemplate()` with an orphaned child).
    private void EmitTemplateBuilder(string targetVar, string templateTypeName, IAumlAstNode templateNode,
        bool isResource, IDiagnosticSink diagnostics)
    {
        var templateBuilderMethod = $"Build_{GenerateNextElementName("template")}";

        TextGenerator.WriteLine($"{targetVar} = new {templateTypeName}({templateBuilderMethod});");

        TextGenerator.NewLine();
        TextGenerator.WriteLine($"{Metadata.DefaultTypeContainer.TemplateResult.FullName} {templateBuilderMethod}()");
        TextGenerator.WriteOpenBraceAndIndent();

        TextGenerator.WriteLine($"var result = new {Metadata.DefaultTypeContainer.TemplateResult.FullName}();");

        PushTemplate("result");

        var child = templateNode.GetLogicalChildrenObjects().FirstOrDefault();
        var childName = ProcessControlElements(child, diagnostics, false);
        TextGenerator.WriteLine($"result.RootComponent = {childName};");

        var templateProperties = templateNode.GetProperties().ToList();
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
    }

    private void GenerateSimpleAssignment(string symbolName, string valueText, IResolvedType member)
    {
        TextGenerator.WriteLine($"{symbolName} = {BuildValueExpression(valueText, member)};");
    }

    // The C# expression for a literal attribute value, typed as the property's type (so it can be assigned directly OR
    // boxed for a priority-aware SetValue without losing its type, e.g. Opacity="0" must be (double)0, not a boxed int).
    private string BuildValueExpression(string valueText, IResolvedType member)
    {
        if (member.TypeKind == ResolvedTypeKind.Enum)
            return $"{member.FullName}.{valueText}";

        // Floating "specials" are not valid C# numeric literals - emit the framework constant instead (mirrors the
        // runtime TypeCastFactory, which maps "Auto" -> NaN). Lets a markup double carry them, e.g. an animation's
        // IterationCount="Infinity" (loop forever) or a Width="NaN".
        if (member.SpecialType is ResolvedSpecialType.System_Double or ResolvedSpecialType.System_Single)
        {
            var floatType = member.SpecialType == ResolvedSpecialType.System_Double ? "double" : "float";
            switch (valueText)
            {
                case "Infinity":
                case "+Infinity": return $"{floatType}.PositiveInfinity";
                case "-Infinity": return $"{floatType}.NegativeInfinity";
                case "NaN":
                case "Auto": return $"{floatType}.NaN";
            }
        }

        switch (member.SpecialType)
        {
            case ResolvedSpecialType.System_String:
                return $"\"{valueText}\"";
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
                return valueText;
            case ResolvedSpecialType.System_Boolean:
                return valueText.ToLowerInvariant();
            case ResolvedSpecialType.System_Object:
                return $"\"{valueText}\"";
            default:
                return $"{Metadata.DefaultTypeContainer.TypeParser.FullName}.Parse<{member.FullName}>({Quote(valueText)})";
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

    // ---- bindings -------------------------------------------------------------------------------------------------

    // Emit the SetBinding call. An ATTACHED property (ToolTipService.ToolTip, Grid.Row, ...) is NOT registered on the
    // target's type, so binding by NAME (SetBinding("ToolTip", ...)) can't resolve it and throws. Bind to the property
    // OBJECT instead - <Owner>.<Name>Property - exactly as WPF binds to the DependencyProperty; the target type is
    // irrelevant, the property object fully identifies it.
    private static string EmitSetBinding(string bindingTarget, AumlAstPropertyReference propRef, string bindingVar)
    {
        if (propRef.IsAttachedProperty)
            return $"{bindingTarget}.SetBinding({propRef.OwnerType.GetFullTypeName()}.{propRef.Name}Property, {bindingVar});";
        return $"{bindingTarget}.SetBinding(\"{propRef.Name}\", {bindingVar});";
    }

    private const string BindingFqn = "global::Adamantium.UI.Core.Data.Binding";
    private const string MultiBindingFqn = "global::Adamantium.UI.Core.Data.MultiBinding";
    private const string PropertyPathFqn = "global::Adamantium.UI.Core.PropertyPath";
    private const string BindingModeFqn = "global::Adamantium.UI.Core.Data.BindingMode";
    private const string ValueConverterFqn = "global::Adamantium.UI.Core.Data.IValueConverter";
    private const string MultiValueConverterFqn = "global::Adamantium.UI.Core.Data.IMultiValueConverter";
    private const string ResourceResolverFqn = "global::Adamantium.UI.Core.Resources.ResourceResolver";

    // The x:Key directive of an inline object node (an inline resource entry), or null if it has none.
    private static string GetKeyDirective(IAumlAstNode node)
    {
        if (node is not AumlAstObjectNode obj) return null;
        foreach (var child in obj.Children)
            if (child is AumlAstDirective d && d.Name == AumlDirectives.Key && d.Value is AumlAstTextNode t)
                return t.Text;
        return null;
    }

    // A Binding/MultiBinding written as a markup extension ({Binding}/{MultiBinding}) or as an element
    // (<Binding/>, <MultiBinding>...</MultiBinding>). The codegen treats these by NAME (like ResourceReference).
    private static bool IsBindingNode(IAumlAstNode node) => node switch
    {
        AumlAstMarkupExtensionNode me => me.TypeReference?.Name is "Binding" or "MultiBinding",
        AumlAstObjectNode obj => obj.TypeReference?.Name is "Binding" or "MultiBinding",
        _ => false,
    };

    // True when a property is typed to HOLD a binding object itself (Binding/MultiBinding/BindingBase) - e.g.
    // DataTrigger.Binding. Such a property takes the {Binding} value by direct assignment; an ordinary property instead
    // gets a live binding established on it via SetBinding.
    private static bool IsBindingTypedProperty(IResolvedType type) =>
        type?.FullName is "Adamantium.UI.Core.Data.Binding"
                       or "Adamantium.UI.Core.Data.MultiBinding"
                       or "Adamantium.UI.Core.Data.BindingBase";

    // Emits the code that constructs a Binding/MultiBinding and returns the variable holding it. Recurses for nested
    // multi-bindings, so multibinding-inside-multibinding generates correctly.
    private string EmitBinding(IAumlAstNode node, IDiagnosticSink diagnostics, bool isResource) => node switch
    {
        AumlAstMarkupExtensionNode me when me.TypeReference?.Name == "MultiBinding"
            => EmitMultiBindingFromMarkup(me, diagnostics, isResource),
        AumlAstMarkupExtensionNode me => EmitBindingFromMarkup(me, diagnostics, isResource),
        AumlAstObjectNode obj when obj.TypeReference?.Name == "MultiBinding"
            => EmitMultiBindingFromObject(obj, diagnostics, isResource),
        AumlAstObjectNode obj => EmitBindingFromObject(obj, diagnostics, isResource),
        _ => "null",
    };

    private string EmitBindingFromMarkup(AumlAstMarkupExtensionNode me, IDiagnosticSink diagnostics, bool isResource)
    {
        var name = GenerateNextElementName("binding");
        TextGenerator.WriteLine($"var {name} = new {BindingFqn}();");
        foreach (var arg in me.Arguments)
            EmitBindingProperty(name, string.IsNullOrEmpty(arg.Name) ? "Path" : arg.Name, arg.Value, isMulti: false, diagnostics, isResource);
        return name;
    }

    private string EmitBindingFromObject(AumlAstObjectNode obj, IDiagnosticSink diagnostics, bool isResource)
    {
        var name = GenerateNextElementName("binding");
        TextGenerator.WriteLine($"var {name} = new {BindingFqn}();");
        foreach (var child in obj.Children)
            if (child is AumlAstPropertyNode { Property: AumlAstPropertyReference pref } pn && pn.Values.Count > 0)
                EmitBindingProperty(name, pref.Name, pn.Values[0], isMulti: false, diagnostics, isResource);
        return name;
    }

    private string EmitMultiBindingFromMarkup(AumlAstMarkupExtensionNode me, IDiagnosticSink diagnostics, bool isResource)
    {
        var name = GenerateNextElementName("multiBinding");
        TextGenerator.WriteLine($"var {name} = new {MultiBindingFqn}();");
        foreach (var arg in me.Arguments)
        {
            if (string.IsNullOrEmpty(arg.Name))                       // positional = a child binding
            {
                if (IsBindingNode(arg.Value))
                    TextGenerator.WriteLine($"{name}.Bindings.Add({EmitBinding(arg.Value, diagnostics, isResource)});");
            }
            else EmitBindingProperty(name, arg.Name, arg.Value, isMulti: true, diagnostics, isResource);
        }
        return name;
    }

    private string EmitMultiBindingFromObject(AumlAstObjectNode obj, IDiagnosticSink diagnostics, bool isResource)
    {
        var name = GenerateNextElementName("multiBinding");
        TextGenerator.WriteLine($"var {name} = new {MultiBindingFqn}();");
        foreach (var child in obj.Children)
        {
            switch (child)
            {
                case AumlAstObjectNode childObj when IsBindingNode(childObj):
                    TextGenerator.WriteLine($"{name}.Bindings.Add({EmitBinding(childObj, diagnostics, isResource)});");
                    break;
                case AumlAstPropertyNode { Property: AumlAstPropertyReference pref } pn when pn.Values.Count > 0:
                    if (pref.Name == "Bindings")
                    {
                        foreach (var v in pn.Values)
                            if (IsBindingNode(v))
                                TextGenerator.WriteLine($"{name}.Bindings.Add({EmitBinding(v, diagnostics, isResource)});");
                    }
                    else EmitBindingProperty(name, pref.Name, pn.Values[0], isMulti: true, diagnostics, isResource);
                    break;
            }
        }
        return name;
    }

    private void EmitBindingProperty(string bindingVar, string name, IAumlAstValueNode value, bool isMulti,
        IDiagnosticSink diagnostics, bool isResource)
    {
        switch (name)
        {
            case "Path":
                if ((value as AumlAstTextNode)?.Text?.Trim() is { Length: > 0 } path)
                    TextGenerator.WriteLine($"{bindingVar}.Path = new {PropertyPathFqn}(\"{path}\");");
                break;
            case "Mode":
                if ((value as AumlAstTextNode)?.Text is { Length: > 0 } mode)
                    TextGenerator.WriteLine($"{bindingVar}.Mode = {BindingModeFqn}.{mode};");
                break;
            case "Converter":
                var converter = EmitValueExpression(value, isMulti ? MultiValueConverterFqn : ValueConverterFqn, diagnostics, isResource);
                if (converter != null) TextGenerator.WriteLine($"{bindingVar}.Converter = {converter};");
                break;
            case "ConverterParameter":
                var parameter = EmitValueExpression(value, "object", diagnostics, isResource);
                if (parameter != null) TextGenerator.WriteLine($"{bindingVar}.ConverterParameter = {parameter};");
                break;
            case "StringFormat":
                if ((value as AumlAstTextNode)?.Text is { } format)
                    TextGenerator.WriteLine($"{bindingVar}.StringFormat = \"{format}\";");
                break;
            case "Source":
                var source = EmitValueExpression(value, "object", diagnostics, isResource);
                if (source != null) TextGenerator.WriteLine($"{bindingVar}.Source = {source};");
                break;
            case "ElementName":
                if ((value as AumlAstTextNode)?.Text?.Trim() is { Length: > 0 } elementName)
                    TextGenerator.WriteLine($"{bindingVar}.ElementName = \"{elementName}\";");
                break;
            default:
                diagnostics.ReportWarning(Metadata.ClassName, $"Unknown binding property '{name}'");
                break;
        }
    }

    // A binding sub-value (Converter / ConverterParameter / Source) as a C# expression: a {ResourceReference}, an
    // inline element, a converter authored as a markup extension ({local:MyConverter} -> new MyConverter()), or a literal.
    private string EmitValueExpression(IAumlAstValueNode value, string targetTypeFqn, IDiagnosticSink diagnostics, bool isResource)
    {
        switch (value)
        {
            case AumlAstMarkupExtensionNode me when me.TypeReference?.Name?.StartsWith("ResourceReference") == true:
                var key = (me.Arguments.FirstOrDefault()?.Value as AumlAstTextNode)?.Text;
                return string.IsNullOrEmpty(key) ? null : $"{ResourceResolverFqn}.Resolve<{targetTypeFqn}>(\"{key}\")";
            case AumlAstMarkupExtensionNode me:
                return ProcessNestedValue(me, diagnostics, isResource);          // converter authored as a markup extension
            case AumlAstObjectNode obj:
                return ProcessControlElements(obj, diagnostics, isResource);     // inline element
            case AumlAstTextNode text:
                return $"\"{text.Text}\"";
            default:
                return null;
        }
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