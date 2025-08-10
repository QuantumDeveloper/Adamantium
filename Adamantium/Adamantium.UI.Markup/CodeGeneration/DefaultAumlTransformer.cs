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
            RootNamespace = document.RootNamespace,
        };

        typeResolver.ScanXmlnsAttributes();
        foreach(var mapping in document.NamespaceMappings)
        {
            if (mapping.IsClrNamespace)
            {
                typeResolver.ResolveAssembly(mapping.Assembly);
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
                    markupExtension.TypeReference = ProcessTypeReference(markupExtension.TypeReference, markupExtension.GetLineInfo());
                    break;
                //case AumlAstMarkupExtensionLiteral literal:
                //    literal.TypeReference = ProcessTypeReference(literal.TypeReference, literal.GetLineInfo());
                //    break;
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
                    if (objectNode is AumlAstTemplateNode)
                    {
                        int x = 0;
                    }
                    foreach (var child in objectNode.Children)
                    {
                        queue.Enqueue(child);
                    }
                    break;
                case AumlAstPropertyNode propertyNode:
                    if (propertyNode.Property is AumlAstPropertyReference propertyReference)
                    {
                        if (propertyReference.Name == "Name")
                        {
                            if (propertyNode.Values[0] is AumlAstTextNode textNode)
                            {
                                container.NamedElements.Add(new NamedElement(textNode.Text, propertyReference.ParentNode));
                            }
                        }
                        
                        foreach (var value in propertyNode.Values)
                        {
                            queue.Enqueue(value);
                        }
                    }
                    break;
                case AumlAstDirective directive:
                    if (directive.Name == "Name")
                    {
                        if (directive.Value is AumlAstTextNode textNode)
                        {
                            container.NamedElements.Add(new NamedElement(textNode.Text, directive.ParentNode));
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

        if (container.FileName.StartsWith("ButtonStyleSet"))
        {
            int x = 0;
        }

        return container;
    }
}