using Adamantium.UI.Markup.AST;
using Adamantium.UI.Markup.AST.MarkupExtension;
using Adamantium.UI.Markup.AST.TypeReference;
using Adamantium.UI.Markup.Exceptions;
using Adamantium.UI.Markup.Parsers;

namespace Adamantium.UI.Markup.CodeGeneration;

public class DefaultAumlTransformer : IAumlTransformer
{
    private const string MarkupItemAttributeName = "Adamantium.UI.Core.MarkupItemAttribute";

    // Tokens of a shorthand collection: commas or spaces, but only outside a markup extension, whose own arguments are
    // separated the same way ("Auto, {Binding A, Mode=OneWay}, *").
    private static IEnumerable<string> SplitShorthand(string text)
    {
        var depth = 0;
        var start = 0;

        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length)
            {
                if (text[i] == '{')
                {
                    depth++;
                    continue;
                }

                if (text[i] == '}')
                {
                    depth--;
                    continue;
                }

                if (depth > 0 || (text[i] != ',' && text[i] != ' '))
                {
                    continue;
                }
            }

            var token = text.Substring(start, i - start).Trim();
            start = i + 1;
            if (token.Length > 0)
            {
                yield return token;
            }
        }
    }

    public AumlMetadataContainer Transform(AumlDocument document, ITypeResolver typeResolver, IDiagnosticSink diagnostics)
    {
        var container = new AumlMetadataContainer(typeResolver)
        {
            RelativeFilePath = document.RelativeFilePath,
            AssemblyName = document.RootNamespace,
        };

        typeResolver.ScanXmlnsAttributes();
        foreach(var mapping in document.NamespaceMappings)
        {
            if (mapping.IsClrNamespace)
            {
                if (string.IsNullOrEmpty(mapping.Assembly))
                {
                    typeResolver.FindAssemblyByNamespace(mapping.Namespace);
                }
                else
                {
                    typeResolver.ResolveAssembly(mapping.Assembly);
                }
            }
        }

        void TransformTypeForElement(IAumlAstNode node)
        {
            switch (node)
            {
                case AumlAstObjectNode objectNode:
                    objectNode.TypeReference = ProcessTypeReference(objectNode.TypeReference, objectNode.GetLineInfo());
                    break;
                case AumlAstPropertyNode propertyNode:
                    if (propertyNode.Property is AumlAstPropertyReference reference)
                    {
                        reference.OwnerType = ProcessTypeReference(reference.OwnerType, reference.GetLineInfo());
                        if (reference.IsAttachedProperty)
                        {
                            reference.TargetType = ProcessTypeReference(reference.TargetType, reference.GetLineInfo());
                        }
                        else
                        {
                            reference.TargetType = ResolvePropertyType(reference, reference.TargetType, reference.GetLineInfo());
                        }
                    }
                    break;
                case AumlAstPropertyReference propertyReference:
                    propertyReference.OwnerType = ProcessTypeReference(propertyReference.OwnerType, propertyReference.GetLineInfo());
                    if (propertyReference.IsAttachedProperty)
                    {
                        propertyReference.TargetType = ProcessTypeReference(propertyReference.TargetType, propertyReference.GetLineInfo());
                    }
                    else
                    {
                        propertyReference.TargetType = ResolvePropertyType(propertyReference, propertyReference.TargetType, propertyReference.GetLineInfo());
                    }
                    
                    break;
                case AumlAstMarkupExtensionNode markupExtension:
                    ProcessMarkupExtension(markupExtension);
                    break;
                //case AumlAstMarkupExtensionLiteral literal:
                //    literal.TypeReference = ProcessTypeReference(literal.TypeReference, literal.GetLineInfo());
                //    break;
            }
        }

        void ProcessMarkupExtension(IAumlAstMarkupExtensionNode markupExtension)
        {
            markupExtension.TypeReference = ProcessTypeReference(markupExtension.TypeReference, markupExtension.GetLineInfo());
            var resolvedAssembly = typeResolver.GetResolvedAssembly(markupExtension.TypeReference.Assembly);

            if (resolvedAssembly == null)
            {
                diagnostics.ReportError(document.FileName,
                    $"Assembly {markupExtension.TypeReference.Assembly} could not be found. {markupExtension.GetLineInfo()}");
                return;           
            }
            
            var type = resolvedAssembly.GetTypeByFullName(markupExtension.TypeReference.GetFullTypeName());
            
            foreach (var argument in markupExtension.Arguments)
            {
                var transformedValue = ProcessValueNode(argument.Value);
                argument.Value = transformedValue;
                
                IResolvedProperty property = null;
                if (string.IsNullOrEmpty(argument.Name))
                {
                    var result = type.FindPropertyWithAttribute("Adamantium.UI.Core.MarkupExtensions.DefaultPropertyAttribute", out property);
                }
                else
                {
                    property = type.GetAllProperties().FirstOrDefault(x => x.Name == argument.Name);
                }

                if (property == null)
                    diagnostics.ReportError(document.FileName,
                        $"Property {argument.Name} could not be found in {markupExtension.TypeReference.GetFullTypeName()}. {markupExtension.GetLineInfo()}");
                
                if (transformedValue is AumlAstMarkupExtensionLiteral literal)
                {
                    literal.TypeReference = CreateResolved(property.PropertyType, markupExtension.GetLineInfo());
                }
            }
        }

        // The xml-namespace -> assembly registry only holds URIs registered via [XmlnsDefinition]. A property element
        // on a custom-namespace type (<local:TilesHost.ItemsPanel>) carries the raw clr-namespace declaration instead -
        // resolve that directly by CLR namespace (honouring an explicit ;assembly= part) so property elements work on
        // app-assembly controls, not only on framework types.
        IResolvedAssembly ResolveXmlDefinitionContainer(string xmlNamespace)
        {
            const string clrPrefix = "clr-namespace:";
            if (!xmlNamespace.StartsWith(clrPrefix, StringComparison.Ordinal))
                return typeResolver.GetResolvedAssemblyByXmlDefinition(xmlNamespace);

            var ns = xmlNamespace.Substring(clrPrefix.Length);
            var semi = ns.IndexOf(';');
            if (semi >= 0)
            {
                var assemblyName = ns.Substring(semi + 1).Replace("assembly=", string.Empty).Trim();
                ns = ns.Substring(0, semi);
                var byAssembly = typeResolver.ResolveAssembly(assemblyName);
                if (byAssembly != null) return byAssembly;
            }
            return typeResolver.FindAssemblyByNamespace(ns);
        }

        IAumlAstTypeReference ProcessTypeReference(IAumlAstTypeReference typeReference, IAumlLineInfo lineInfo)
        {
            if (typeReference == null || typeReference.IsResolved)
                return typeReference;

            if (typeReference.IsXmlNamespaceDeclaration)
            {
                if (string.IsNullOrEmpty(typeReference.Namespace))
                {
                    return ResolveByNameOnly(typeReference, lineInfo);
                }

                var typeContainer = ResolveXmlDefinitionContainer(typeReference.Namespace);

                if (typeContainer == null)
                {
                    diagnostics.ReportError(document.FileName, $"Xml namespace {typeReference.Namespace} could not be found. {lineInfo}");
                    return typeReference;
                }

                var typeInfo = typeContainer.Types.FirstOrDefault(x => x.Name == typeReference.Name);
                if (typeInfo == null)
                {
                    // A same-assembly (generated) type used WITHOUT a clr-namespace prefix - e.g. an embedded AUML view
                    // <ControlsView/> under the default xmlns. Such views live in the local assembly (pre-registered),
                    // not in a framework xmlns, so fall back to a short-name lookup before failing.
                    var local = typeResolver.ResolveByShortName(typeReference.Name);
                    if (local != null)
                        return CreateResolved(local, lineInfo);

                    diagnostics.ReportError(document.FileName, $"Type {typeReference.Name} could not be found in namespace {typeReference.Namespace}. {lineInfo}");
                    return typeReference;
                }

                return CreateResolved(typeInfo, lineInfo);
            }

            // not XmlNamespaceDeclaration
            if (string.IsNullOrEmpty(typeReference.Assembly))
            {
                if (string.IsNullOrEmpty(typeReference.Namespace))
                {
                    return ResolveByNameOnly(typeReference, lineInfo);
                }
                else
                {
                    return ResolveByFullTypeNameWithoutAssembly(typeReference, lineInfo);
                }
            }

            // CLR type reference
            var clrTypeContainer = typeResolver.ResolveAssembly(typeReference.Assembly);
            if (clrTypeContainer != null)
            {
                var typeInfo = clrTypeContainer.Types.FirstOrDefault(x => x.Name == typeReference.Name);
                if (typeInfo == null)
                {
                    diagnostics.ReportError(document.FileName, $"Type {typeReference.Name} could not be found in namespace {typeReference.Namespace}. {lineInfo}");
                    return typeReference;
                }
                return CreateResolved(typeInfo, lineInfo);
            }

            throw new TypeNotAvailableException($"Type {typeReference.Name} is not available");
        }
        
        IAumlAstTypeReference ResolvePropertyType(AumlAstPropertyReference propertyReference, IAumlAstTypeReference typeReference, IAumlLineInfo lineInfo)
        {
            if (typeReference == null || typeReference.IsResolved)
                return typeReference;

            if (typeReference.IsXmlNamespaceDeclaration)
            {
                if (string.IsNullOrEmpty(typeReference.Namespace))
                {
                    return ResolveByNameOnly(typeReference, lineInfo);
                }

                var typeContainer = ResolveXmlDefinitionContainer(typeReference.Namespace);

                if (typeContainer == null)
                {
                    diagnostics.ReportError(document.FileName, $"Xml namespace {typeReference.Namespace} could not be found. {lineInfo}");
                    return typeReference;
                }

                var typeInfo = typeContainer.Types.FirstOrDefault(x => x.Name == typeReference.Name);
                if (typeInfo == null)
                {
                    // A same-assembly (generated) type used WITHOUT a clr-namespace prefix - e.g. a property set on an
                    // embedded AUML view <ControlsView VerticalAlignment="Center"/> under the default xmlns. The view
                    // lives in the local assembly (pre-registered), not in a framework xmlns, so fall back to a
                    // short-name lookup before failing (mirrors ProcessTypeReference).
                    typeInfo = typeResolver.ResolveByShortName(typeReference.Name);
                    if (typeInfo == null)
                    {
                        diagnostics.ReportError(document.FileName, $"Type {typeReference.Name} could not be found in namespace {typeReference.Namespace}. {lineInfo}");
                        return typeReference;
                    }
                }

                var propertyInfo = typeInfo.GetAllProperties().FirstOrDefault(x=>x.Name == propertyReference.Name);

                if (propertyInfo == null)
                {
                    diagnostics.ReportError(document.FileName, $"Property {propertyReference.Name} could not be found in {typeReference.Name}. {lineInfo}");
                    return typeReference;
                }

                return CreateResolved(propertyInfo.PropertyType, lineInfo);
            }

            // not XmlNamespaceDeclaration
            if (string.IsNullOrEmpty(typeReference.Assembly) || string.IsNullOrEmpty(typeReference.Namespace))
            {
                return ResolveByNameOnly(typeReference, lineInfo);
            }

            // CLR type reference
            var clrTypeContainer = typeResolver.ResolveAssembly(typeReference.Assembly);
            if (clrTypeContainer != null)
            {
                var typeInfo = clrTypeContainer.Types.FirstOrDefault(x => x.Name == typeReference.Name);
                if (typeInfo == null)
                {
                    diagnostics.ReportError(document.FileName, $"Type {typeReference.Name} could not be found in namespace {typeReference.Namespace}. {lineInfo}");
                    return typeReference;
                }
                return CreateResolved(typeInfo, lineInfo);
            }

            throw new TypeNotAvailableException($"Type {typeReference.Name} is not available");
        }

        IAumlAstTypeReference ResolveByFullTypeNameWithoutAssembly(IAumlAstTypeReference typeReference, IAumlLineInfo lineInfo)
        {
            var typeInfo = typeResolver.FindAssemblyByNamespace(typeReference.Namespace);

            if (typeInfo == null)
            {
                diagnostics.ReportError(document.FileName, $"Type {typeReference.Name} could not be found in any linked assembly. {lineInfo}");
                return typeReference;
            }
            
            var type = typeInfo.Types.FirstOrDefault(x => x.FullName == typeReference.GetFullTypeName());
            if (type == null)
            {
                diagnostics.ReportError(document.FileName, $"Type {typeReference.Name} could not be found in namespace {typeReference.Namespace}. {lineInfo}");
                return typeReference;
            }

            return CreateResolved(type, lineInfo);
        }

        IAumlAstTypeReference ResolveByNameOnly(IAumlAstTypeReference typeReference, IAumlLineInfo lineInfo)
        {
            var typeInfo = typeResolver.ResolveByShortName(typeReference.Name);

            if (typeInfo == null)
            {
                diagnostics.ReportError(document.FileName, $"Type {typeReference.Name} could not be found in any linked assembly. {lineInfo}");
                return typeReference;
            }

            return CreateResolved(typeInfo, lineInfo);
        }

        IAumlAstTypeReference CreateResolved(IResolvedType typeInfo, IAumlLineInfo lineInfo)
        {
            bool isMarkupExtension = typeInfo.InheritsFromMarkupExtension(AumlParser.MarupExtensionClassFullName);

            return new AumlAstResolvedTypeReference(
                lineInfo,
                typeInfo.Namespace,
                typeInfo.Name,
                typeInfo.AssemblyName,
                isMarkupExtension
            );
        }
        
        // --- НОВЫЙ МЕТОД ДЛЯ ОБРАБОТКИ ВСЕХ ТИПОВ ЗНАЧЕНИЙ ---
        IAumlAstValueNode ProcessValueNode(IAumlAstValueNode valueNode)
        {
            switch (valueNode)
            {
                // Если значение - это директива (например, из {x:Type ...})
                case AumlAstDirective directive:
                    return ProcessDirectiveValue(directive);
                
                // Если значение - обычное расширение разметки (не директива)
                case AumlAstMarkupExtensionNode markupExtension:
                    ProcessMarkupExtension(markupExtension);
                    return markupExtension;

                // Если это текстовое значение или любой другой узел, оставляем как есть
                default:
                    return valueNode;
            }
        }
        
        IAumlAstValueNode ProcessDirectiveValue(AumlAstDirective directive)
        {
            var directiveBody = (directive.Value as AumlAstTextNode)?.Text;
            if (string.IsNullOrWhiteSpace(directiveBody))
            {
                diagnostics.ReportError(document.FileName, $"Directive '{directive.Name}' is missing a value. {directive.GetLineInfo()}");
                return directive;
            }

            switch (directive.Name)
            {
                case "Type":
                    // Используем тот же механизм, что и в парсере, чтобы распознать имя типа
                    var typeRef = MarkupExtensionParser.ParseTypeName(new ParserContext(null), directiveBody, directive.GetLineInfo(), document.NamespaceMappings.ToList());
                    
                    // Резолвим его в конкретный IResolvedType
                    var resolvedTypeRef = ProcessTypeReference(typeRef, directive.GetLineInfo());

                    // Возвращаем наш новый узел, который несет в себе информацию о типе
                    return new AumlAstTypeReferenceValueNode(directive.GetLineInfo(), resolvedTypeRef);
               
                default:
                    diagnostics.ReportError(document.FileName, $"Unknown directive 'x:{directive.Name}'. {directive.GetLineInfo()}");
                    return directive;
            }
        }

        // A [MarkupItem] collection written as the shorthand string with a markup extension among the tokens -
        // ColumnDefinitions="Auto,{TemplateBinding OverflowButtonWidth}" - becomes the element form the rest of the
        // pipeline already understands: one item object per token, each with the declared item property set. A plain
        // string keeps going to the TypeParser untouched.
        void ExpandShorthandCollection(AumlAstPropertyNode propertyNode)
        {
            if (propertyNode.Property is not AumlAstPropertyReference { IsAttachedProperty: false } reference) return;
            if (reference.TargetType is not { IsResolved: true }) return;
            if (propertyNode.Values.Count != 1 || propertyNode.Values[0] is not AumlAstTextNode text) return;
            if (text.Text.IndexOf('{') < 0) return;

            var markupItem = typeResolver.Resolve(reference.TargetType.GetFullTypeName())?.GetAttribute(MarkupItemAttributeName);
            if (markupItem == null) return;

            markupItem.NamedArguments.TryGetValue("ItemType", out var itemTypeArgument);
            markupItem.NamedArguments.TryGetValue("ItemProperty", out var itemPropertyArgument);
            var itemPropertyName = itemPropertyArgument?.ToString();

            var itemType = itemTypeArgument == null ? null : typeResolver.Resolve(itemTypeArgument.ToString());
            var itemProperty = itemType?.GetAllProperties().FirstOrDefault(x => x.Name == itemPropertyName);
            if (itemProperty == null)
            {
                diagnostics.ReportError(document.FileName,
                    $"[MarkupItem] on {reference.TargetType.GetFullTypeName()} names no reachable item property. {propertyNode.GetLineInfo()}");
                return;
            }

            var lineInfo = text.GetLineInfo();
            var itemTypeReference = CreateResolved(itemType, lineInfo);
            var itemPropertyTypeReference = CreateResolved(itemProperty.PropertyType, lineInfo);
            var items = new List<IAumlAstValueNode>();

            foreach (var token in SplitShorthand(text.Text))
            {
                var itemNode = new AumlAstObjectNode(lineInfo, itemTypeReference);
                var value = token.StartsWith("{")
                    ? MarkupExtensionParser.Parse(new ParserContext(null), token, lineInfo, document.NamespaceMappings.ToList())
                    : new AumlAstTextNode(lineInfo, token);

                itemNode.Children.Add(new AumlAstPropertyNode(
                    lineInfo,
                    new AumlAstPropertyReference(lineInfo, false, itemNode, itemTypeReference, itemPropertyTypeReference, itemPropertyName),
                    value));

                items.Add(itemNode);
            }

            propertyNode.Values = items;
        }


        var entityType = EntityType.Unknown;
        var usings = new Dictionary<string, string>();

        var node = document.Root;
        TransformTypeForElement(node);
        container.RootClassName = container.FileName;
                
        var rootType = typeResolver.Resolve(node.TypeReference.GetFullTypeName());
        if (rootType == null)
        {
            diagnostics.ReportError(document.FileName,
                $"{node.TypeReference.GetFullTypeName()} could not be found. Please, check correctness of namespace");
            return container;
        }
                
        entityType = rootType.EntityType;

        if (entityType == EntityType.Unknown)
        {
            return container;
        }
        
        container.RootEntityType = entityType;
        
        var queue = new Queue<IAumlAstNode>();
        queue.Enqueue(document.Root);
            
        while (queue.Count > 0)
        {
            var element = queue.Dequeue();
            TransformTypeForElement(element);
            switch (element)
            {
                case AumlAstObjectNode objectNode:
                    foreach (var child in objectNode.Children)
                    {
                        queue.Enqueue(child);
                    }
                    break;
                case AumlAstPropertyNode propertyNode:

                    ExpandShorthandCollection(propertyNode);

                    for (int i = 0; i < propertyNode.Values.Count; i++)
                    {
                        var transformedValue = ProcessValueNode(propertyNode.Values[i]);
                        propertyNode.Values[i] = transformedValue;
                        queue.Enqueue(transformedValue);
                    }
                    
                    //if (propertyNode.Property is AumlAstPropertyReference propertyReference)
                    //{
                    //    if (propertyReference.Name == "Name")
                    //    {
                    //        if (propertyNode.Values[0] is AumlAstTextNode textNode)
                    //        {
                    //            container.NamedElements.Add(new NamedElement(textNode.Text, propertyReference.ParentNode));
                    //        }
                    //    }
                        
                    //    foreach (var value in propertyNode.Values)
                    //    {
                    //        queue.Enqueue(value);
                    //    }
                    //}
                    
                    break;
                case AumlAstDirective directive:
                    if (directive.Name == AumlDirectives.Name)
                    {
                        if (directive.Value is AumlAstTextNode textNode)
                        {
                            container.NamedElements.Add(new NamedElement(textNode.Text, directive.ParentNode));
                        }
                    }
                    else if (directive.Name == AumlDirectives.ViewModel && directive.ParentNode == document.Root)
                    {
                        // x:ViewModel="prefix:Vm" on the root records the view-model type as metadata; the framework
                        // resolves it from the DI container and assigns it as DataContext when the view goes live.
                        if (directive.Value is AumlAstTextNode vmNode && !string.IsNullOrWhiteSpace(vmNode.Text))
                        {
                            // Accept both a plain type reference (x:ViewModel="local:Vm") and the x:Type markup-extension
                            // form (x:ViewModel="{x:Type local:Vm}"). The parser turns {x:Type body} into a 'Type'
                            // directive whose value is the inner type text.
                            var typeText = vmNode.Text.Trim();
                            if (typeText.StartsWith("{"))
                            {
                                var parsed = MarkupExtensionParser.Parse(new ParserContext(null), typeText, directive.GetLineInfo(), document.NamespaceMappings.ToList());
                                if (parsed is AumlAstDirective { Name: AumlDirectives.Type, Value: AumlAstTextNode innerType })
                                    typeText = innerType.Text.Trim();
                            }

                            var vmTypeRef = MarkupExtensionParser.ParseTypeName(new ParserContext(null), typeText, directive.GetLineInfo(), document.NamespaceMappings.ToList());
                            var resolvedVmType = ProcessTypeReference(vmTypeRef, directive.GetLineInfo());
                            if (resolvedVmType is { IsResolved: true })
                            {
                                container.RootViewModelTypeName = resolvedVmType.GetFullTypeName();
                            }
                            else
                            {
                                diagnostics.ReportError(document.FileName, $"x:ViewModel type '{vmNode.Text}' could not be resolved. {directive.GetLineInfo()}");
                            }
                        }
                    }
                    break;
                    
                case AumlAstMarkupExtensionNode markupExtensionNode:
                    foreach (var ext in markupExtensionNode.Arguments)
                    {
                        if (ext.Value is AumlAstMarkupExtensionLiteral literal)
                        {
                            queue.Enqueue(literal);
                        }
                        else if (ext.Value is AumlAstMarkupExtensionNode extNode)
                        {
                            queue.Enqueue(extNode);
                        }
                        else if (ext.Value is AumlAstObjectNode objectNode)
                        {
                            queue.Enqueue(objectNode);
                        }
                    }
                    break;
            }
        }

        foreach (var kvp in usings)
        {
            container.Usings.Add(kvp.Key);
        }

        foreach (var named in container.NamedElements)
        {
            container.NamedElementsMap.Add(named.Element, named.Name);
        }

        container.RootNode = document.Root;
        container.HasSemanticErrors = diagnostics.HasErrors;

        return container;
    }

    /// <summary>
    /// Pre-registers a control document (Window/View/Page/UIApplication) as a generated type, so a document that EMBEDS
    /// another - e.g. <c>&lt;local:ControlsView/&gt;</c> - can resolve it during its own transform, regardless of file
    /// processing order (and so a view-inside-a-view works too). Resolves only the ROOT type (a real framework type,
    /// always present in the compilation), computes the generated class name + namespace exactly as the source generator
    /// will, and registers a <see cref="MetadataResolvedType"/>. Non-control roots (resources/styles/themes are never
    /// embedded) are a no-op. Must run for ALL documents before any body is transformed.
    /// </summary>
    public IResolvedType PreRegisterDocument(AumlDocument document, ITypeResolver typeResolver)
    {
        typeResolver.ScanXmlnsAttributes();
        foreach (var mapping in document.NamespaceMappings)
        {
            if (!mapping.IsClrNamespace) continue;
            if (string.IsNullOrEmpty(mapping.Assembly))
                typeResolver.FindAssemblyByNamespace(mapping.Namespace);
            else
                typeResolver.ResolveAssembly(mapping.Assembly);
        }

        var rootRef = document.Root?.TypeReference;
        if (rootRef == null) return null;

        var resolvedRoot = ResolveRootReference(rootRef, typeResolver);
        if (resolvedRoot == null) return null;

        var rootType = typeResolver.Resolve(resolvedRoot.GetFullTypeName());
        if (rootType is not { EntityType: EntityType.Window or EntityType.View or EntityType.Page or EntityType.UIApplication })
            return null;

        // Reuse the resolved root reference in the full Transform (which short-circuits on IsResolved) and as the
        // registered type's BaseType chain (MetadataResolvedType.BaseType reads RootNode's type reference).
        document.Root.TypeReference = resolvedRoot;

        var className = Path.GetFileNameWithoutExtension(document.RelativeFilePath);
        var container = new AumlMetadataContainer(typeResolver)
        {
            RelativeFilePath = document.RelativeFilePath,
            AssemblyName = document.RootNamespace,
            RootNode = document.Root,
            RootEntityType = rootType.EntityType,
            ClassName = className,
            Namespace = AumlNaming.ComputeNamespace(document.RelativeFilePath, document.RootNamespace, document.Root, className),
        };

        var resolvedType = new MetadataResolvedType(container);
        typeResolver.RegisterGeneratedType(resolvedType);
        return resolvedType;
    }

    // Resolves the document ROOT element's type reference (Window/View/...) to a concrete type. The root is always a
    // real framework type, so it resolves against the compilation even in the lightweight pre-registration pass.
    private IAumlAstTypeReference ResolveRootReference(IAumlAstTypeReference rootRef, ITypeResolver typeResolver)
    {
        if (rootRef.IsResolved) return rootRef;

        IResolvedType type;
        if (rootRef.IsXmlNamespaceDeclaration && !string.IsNullOrEmpty(rootRef.Namespace))
        {
            type = typeResolver.GetResolvedAssemblyByXmlDefinition(rootRef.Namespace)
                ?.Types.FirstOrDefault(x => x.Name == rootRef.Name);
        }
        else if (string.IsNullOrEmpty(rootRef.Namespace))
        {
            type = typeResolver.ResolveByShortName(rootRef.Name);
        }
        else
        {
            type = typeResolver.FindAssemblyByNamespace(rootRef.Namespace)
                ?.Types.FirstOrDefault(x => x.FullName == rootRef.GetFullTypeName());
        }

        if (type == null) return null;

        return new AumlAstResolvedTypeReference(
            rootRef.GetLineInfo(),
            type.Namespace,
            type.Name,
            type.AssemblyName,
            type.InheritsFromMarkupExtension(AumlParser.MarupExtensionClassFullName));
    }
}