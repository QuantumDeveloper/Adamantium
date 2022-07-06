using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Adamantium.Core;
using Adamantium.UI.Markup;
using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Generators
{
    [Generator]
    public class AumlCodeBehindGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var aumlFiles = context.AdditionalTextsProvider.Where(file => file.Path.EndsWith(".xml"));

            //Debugger.Launch();

            // read their contents and save their name
            var namesAndContents = aumlFiles.Select((text, cancellationToken) => (
                name: Path.GetFileNameWithoutExtension(text.Path),
                content: text.GetText(cancellationToken)!.ToString()));

            var sourceProvider = namesAndContents.Combine(context.CompilationProvider);

            context.RegisterSourceOutput(sourceProvider, (spc, provider) =>
            {
                var (file, compilation) = provider;
                var text = file.content;
                var aumlDoc = AumlParser.Parse(text);
                var result = TransformAumlDocument(aumlDoc, compilation);
                spc.AddSource($"{file.name}.g.cs", result);
            });
        }

        private string TransformAumlDocument(AumlDocument document, Compilation compilation)
        {
            var metaContainer = new AumlMetadataContainer();
            var stack = new Stack<IAumlAstNode>();
            stack.Push(document.Root);

            var usings = new Dictionary<string, string>();
            while (stack.Count > 0)
            {
                var element = stack.Pop() as AumlAstObjectNode;

                if (!metaContainer.TypesMap.TryGetValue(element.Type.Namespace, out var typeContainer))
                {
                    var controlType = compilation.GetTypeByMetadataName(element.Type.GetFullTypeName());

                    typeContainer = compilation.GetTypeContainerForNamespace(element.Type.Namespace);
                    metaContainer.TypesMap[element.Type.Namespace] = typeContainer;

                    controlType = typeContainer.Types.FirstOrDefault(x => x.Name == element.Type.Name);
                    usings[controlType.ContainingNamespace.ToDisplayString()] = controlType.ContainingNamespace.ToDisplayString();
                }

                foreach (var child in element.Children)
                {
                    if (child is AumlAstObjectNode)
                    {
                        stack.Push(child);
                    }
                    else if (child is AumlAstPropertyNode property)
                    {
                        if (property.Property is AumlAstPropertyReference propertyReference)
                        {
                            if (propertyReference.Name == "Name")
                            {
                                var textNode = property.Values[0] as AumlAstTextNode;
                                metaContainer.NamedElements.Add(new NamedElement(textNode.Text, element));
                            }

                            var type = typeContainer.Types.FirstOrDefault(x => x.Name == propertyReference.TargetType.Name);
                            var symbol = type.GetMemberByName(propertyReference.Name);
                            if (symbol != null && symbol.Kind == SymbolKind.Property)
                            {
                                var propertySymbol = (IPropertySymbol)symbol;
                                usings[propertySymbol.Type.ContainingNamespace.ToDisplayString()] = propertySymbol.Type.ContainingNamespace.ToDisplayString();
                            }

                            foreach (var value in property.Values)
                            {
                                if (value is AumlAstObjectNode)
                                {
                                    stack.Push(value);
                                }
                            }
                        }
                    }
                    else if (child is AumlAstDirective directive)
                    {
                        if (directive.Name == "Name")
                        {
                            var textNode = directive.Value as AumlAstTextNode;
                            metaContainer.NamedElements.Add(new NamedElement(textNode.Text, element));
                        }
                    }
                }
            }

            foreach (var kvp in usings)
            {
                metaContainer.Usings.Add(kvp.Key);
            }

            foreach (var named in metaContainer.NamedElements)
            {
                metaContainer.NamedElementsMap.Add(named.Element, named.Name);
            }

            metaContainer.RootNode = document.Root;

            return GenerateSources(metaContainer, compilation);
        }

        private string GenerateSources(AumlMetadataContainer container, Compilation compilation)
        {
            var rootNode = container.RootNode as AumlAstObjectNode;
            var directives = rootNode.Children.Where(x => x is AumlAstDirective).ToList();
            AumlAstDirective classDirective = null;
            foreach (var aumlAstNode in directives)
            {
                var directive = (AumlAstDirective)aumlAstNode;
                if (directive.Name == "Class")
                {
                    classDirective = directive;
                    break;
                }
            }

            var directiveValue = classDirective.Value as AumlAstTextNode;

            var className = directiveValue.Text.Split('.').Last();

            var textGenerator = new TextGenerator();
            textGenerator.WriteLine("// Autogenerated code");

            foreach (var ns in container.Usings)
            {
                textGenerator.WriteLine($"using {ns};");
            }

            textGenerator.NewLine();

            var typeContainer = container.TypesMap[rootNode.Type.Namespace];
            var rootBaseType = typeContainer.Types.FirstOrDefault(x => x.Name == rootNode.Type.Name);
            var rootViewType = compilation.GetTypeByMetadataName(directiveValue.Text);

            textGenerator.WriteLine($"namespace {rootViewType.ContainingNamespace.ToDisplayString()};");
            textGenerator.NewLine();
            textGenerator.WriteLine($"public partial class {className} : {rootBaseType.Name}");
            textGenerator.WriteOpenBraceAndIndent();

            foreach (var item in container.NamedElements)
            {
                var typeName = item.Element.Type.GetFullTypeName();
                typeContainer = container.TypesMap[item.Element.Type.Namespace];
                var typeInfo = typeContainer.Types.FirstOrDefault(x => x.Name == item.Element.Type.Name);
                textGenerator.WriteLine($"{typeInfo.ToDisplayString()} {item.Name};");
            }

            textGenerator.NewLine();

            textGenerator.WriteLine($"protected override void InitializeComponent()");
            textGenerator.WriteOpenBraceAndIndent();


            int id = 1;
            ProcessElements(container, container.RootNode, textGenerator, ref id);

            textGenerator.UnindentAndWriteCloseBrace();
            textGenerator.UnindentAndWriteCloseBrace();

            return textGenerator;
        }

        private string ProcessElements(AumlMetadataContainer container, IAumlAstNode currentNode, TextGenerator textGenerator, ref int id)
        {
            var element = currentNode as AumlAstObjectNode;
            string elementName = string.Empty;

            var typeContainer = container.TypesMap[element.Type.Namespace];
            var typeInfo = typeContainer.Types.FirstOrDefault(x => x.Name == element.Type.Name);
            var properties = element.GetProperties();
            var objects = element.GetObjects();

            if (element != container.RootNode)
            {
                if (container.NamedElementsMap.TryGetValue(element, out var name))
                {
                    elementName = name;
                    textGenerator.WriteLine($"{elementName} = new {typeInfo.ToDisplayString()}();");
                }
                else
                {
                    elementName = $"element_{id}";
                    textGenerator.WriteLine($"var {elementName} = new {typeInfo.ToDisplayString()}();");
                    id++;
                }
            }

            foreach (var prop in properties)
            {
                var propertyRef = prop.Property as AumlAstPropertyReference;
                var symbolInfo = typeInfo.GetMemberByName(propertyRef.Name);
                string symbolName = string.Empty;

                if (string.IsNullOrEmpty(elementName))
                {
                    symbolName = propertyRef.Name;
                }
                else
                {
                    symbolName = $"{elementName}.{propertyRef.Name}";
                }

                if (symbolInfo.Kind == SymbolKind.Event)
                {
                    if (prop.Values.Count != 1)
                    {
                        // Here we need to throw exception because we cannot have more than 1 subscription per 1 event for one property
                        break;
                    }
                    else
                    {
                        textGenerator.WriteLine($"{symbolName} += {prop.GetTextValue()};");
                    }
                }
                else if (symbolInfo.Kind == SymbolKind.Property)
                {
                    var objectNodes = new List<IAumlAstValueNode>();
                    foreach (var value in prop.Values)
                    {
                        var propertyInfo = (IPropertySymbol)symbolInfo;
                        if (value.IsTextNode()) // Value is simple text
                        {
                            switch (propertyInfo.Type.SpecialType)
                            {
                                case SpecialType.System_String:
                                    textGenerator.WriteLine($@"{symbolName} = ""{prop.GetTextValue()}"";");
                                    break;

                                case SpecialType.None:
                                    if (propertyInfo.Type.TypeKind == TypeKind.Enum)
                                    {
                                        textGenerator.WriteLine($@"{symbolName} = {propertyInfo.Type.Name}.{prop.GetTextValue()};");
                                    }
                                    else
                                    {
                                        textGenerator.WriteLine($@"{symbolName} = {propertyInfo.Type.Name}.Parse(""{prop.GetTextValue()}"");");
                                    }
                                    break;
                                case SpecialType.System_Enum:
                                    textGenerator.WriteLine($@"{symbolName} = {propertyInfo.Type.Name}.{prop.GetTextValue()};");
                                    break;
                                case SpecialType.System_Boolean:
                                    textGenerator.WriteLine($@"{symbolName} = {prop.GetTextValue().ToLower()};");
                                    break;
                                default:
                                    textGenerator.WriteLine($@"{symbolName} = {prop.GetTextValue()};");
                                    break;
                            }
                        }
                        else // Value is Object node
                        {
                            objectNodes.Add(value);
                        }
                    }

                    foreach (var objectNode in objectNodes)
                    {
                        if (objectNode is AumlAstObjectNode)
                        {
                            var name = ProcessElements(container, objectNode, textGenerator, ref id);

                            textGenerator.WriteLine($"{symbolName} = {name};");
                        }
                    }
                }
            }

            foreach (var obj in objects)
            {
                //stack.Push(obj);
                var name = ProcessElements(container, obj, textGenerator, ref id);

                var isIContainer = typeInfo.ImplementsInterface("IContainer");
                if (typeInfo.FindAttributeByName("ContentAttribute", out var property))
                {

                }

                if (isIContainer)
                {
                    if (!string.IsNullOrEmpty(elementName))
                    {
                        textGenerator.WriteLine($"((IContainer){elementName}).AddOrSetChildComponent({name});");
                    }
                    else
                    {
                        textGenerator.WriteLine($"((IContainer)this).AddOrSetChildComponent({name});");
                    }
                }
                else
                {

                    var isCollection = property.Type.AllInterfaces.FirstOrDefault(x => x.Name.StartsWith("ICollection") || x.Name.StartsWith("IList")) != null;
                    if (isCollection)
                    {
                        if (!string.IsNullOrEmpty(elementName))
                        {
                            textGenerator.WriteLine($"{elementName}.{property.Name}.Add({name});");
                        }
                        else
                        {
                            textGenerator.WriteLine($"{property.Name}.Add({name});");
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(elementName))
                        {
                            textGenerator.WriteLine($"{elementName}.{property.Name} = {name};");
                        }
                        else
                        {
                            textGenerator.WriteLine($"{property.Name} = {name};");
                        }
                    }

                }
            }

            return elementName;
        }
    }
}