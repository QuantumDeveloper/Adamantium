using Adamantium.UI.Markup.AST;
using Adamantium.UI.Markup.AST.MarkupExtension;
using Adamantium.UI.Markup.AST.TypeReference;
using Adamantium.UI.Markup.Exceptions;
using Adamantium.UI.Markup.Parsers;

namespace Adamantium.UI.Markup.CodeGeneration;

public class DefaultAumlTransformer : IAumlTransformer
{
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

                var typeContainer = typeResolver.GetResolvedAssemblyByXmlDefinition(typeReference.Namespace);

                if (typeContainer == null)
                {
                    diagnostics.ReportError(document.FileName, $"Xml namespace {typeReference.Namespace} could not be found. {lineInfo}");
                    return typeReference;
                }

                var typeInfo = typeContainer.Types.FirstOrDefault(x => x.Name == typeReference.Name);
                if (typeInfo == null)
                {
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

                var typeContainer = typeResolver.GetResolvedAssemblyByXmlDefinition(typeReference.Namespace);

                if (typeContainer == null)
                {
                    diagnostics.ReportError(document.FileName, $"Xml namespace {typeReference.Namespace} could not be found. {lineInfo}");
                    return typeReference;
                }

                var typeInfo = typeContainer.Types.FirstOrDefault(x => x.Name == typeReference.Name);
                if (typeInfo == null)
                {
                    diagnostics.ReportError(document.FileName, $"Type {typeReference.Name} could not be found in namespace {typeReference.Namespace}. {lineInfo}");
                    return typeReference;
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
}