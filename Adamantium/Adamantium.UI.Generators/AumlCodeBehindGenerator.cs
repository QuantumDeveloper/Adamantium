using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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

            // read their contents and save their name
            var namesAndContents = aumlFiles.Select((text, cancellationToken) => (
                path: text.Path,
                name: Path.GetFileNameWithoutExtension(text.Path),
                content: text.GetText(cancellationToken)!.ToString()));

            var sourceProvider = namesAndContents.Combine(context.CompilationProvider).Combine(context.AnalyzerConfigOptionsProvider);
            
            context.RegisterSourceOutput(sourceProvider, (spc, provider) =>
            {
                var ((file, compilation), configOptions) = provider;
                configOptions.GlobalOptions.TryGetValue("build_property.RootNamespace", out var @namespace);
                if (string.IsNullOrEmpty(@namespace))
                {
                    CreateDiagnostic(ref spc,
                        file.name,
                        "No RootNamespace Compiler option provided in project file. Please, add <CompilerVisibleProperty Include=\"RootNamespace\" /> to your csproj file",
                        DiagnosticSeverity.Error);
                }
                configOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir);
                var text = file.content;
                var aumlDoc = AumlParser.Parse(text);
                // Get relative file path for further calculations
                aumlDoc.RelativeFilePath = file.path.Replace(projectDir, string.Empty);
                aumlDoc.RootNamespace = @namespace;
                var result = TransformAumlDocument(aumlDoc, compilation, ref spc, ref context);
                spc.AddSource($"{file.name}.g.cs", result);
            });
        }

        private string TransformAumlDocument(
            AumlDocument document, 
            Compilation compilation,
            ref SourceProductionContext spc,
            ref IncrementalGeneratorInitializationContext context)
        {
            var metaContainer = new AumlMetadataContainer();
            metaContainer.RelativeFilePath = document.RelativeFilePath;
            metaContainer.RootNamespace = document.RootNamespace;
            EntityType entityType = EntityType.Unknown;
            if (document.Root is AumlAstObjectNode root)
            {
                var rootType = compilation.GetTypeByMetadataName(root.Type.GetFullTypeName());
                if (rootType == null)
                {
                    CreateDiagnostic(ref spc,
                            document.FileName,
                            $"{root.Type.GetFullTypeName()} could not be found. Please, check correctness of namespace",
                            DiagnosticSeverity.Error);
                    return string.Empty;
                }
                if (rootType.ImplementsInterface("IWindow"))
                {
                    entityType = EntityType.Window;
                }
                else if (rootType.ImplementsInterface("IPage"))
                {
                    entityType = EntityType.Page;
                }
                else if (rootType.ImplementsInterface("IView"))
                {
                    entityType = EntityType.View;
                }
                else if (rootType.ImplementsInterface("ITheme"))
                {
                    entityType = EntityType.Theme;
                }
                else if (rootType.ImplementsInterface("IUIApplication"))
                {
                    entityType = EntityType.UIApplication;
                }
                else if (rootType.ImplementsInterface("IResourceDictionary"))
                {
                    entityType = EntityType.ResourceDictionary;
                }
                else if (rootType.ImplementsInterface("IStyleRepository"))
                {
                    entityType = EntityType.StyleRepository;
                }
                else
                {
                    return String.Empty;
                }
            }

            var stack = new Stack<IAumlAstNode>();
            stack.Push(document.Root);

            var usings = new Dictionary<string, string>();
            while (stack.Count > 0)
            {
                var element = stack.Pop() as AumlAstObjectNode;

                if (!metaContainer.TypesMap.TryGetValue(element.Type.Namespace, out var typeContainer))
                {
                    var controlType = compilation.GetTypeByMetadataName(element.Type.GetFullTypeName());
                    // TODO: handle namespaces in auml in more generic way
                    if (element.Type.Name == "ResourceDictionary" ||
                        element.Type.Name == "StyleRepository" ||
                        element.Type.Name == "Theme")
                    {
                        typeContainer = compilation.GetTypeContainerForNamespace(AumlParser.BasicUiNamespace);
                    }
                    else
                    {
                        typeContainer = compilation.GetTypeContainerForNamespace(element.Type.Namespace);
                    }
                    
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

            return GenerateSources(metaContainer, entityType, compilation, ref spc);
        }

        private string GenerateSources(AumlMetadataContainer container, EntityType entityType, Compilation compilation, ref SourceProductionContext spc)
        {
            switch (entityType)
            {
                case EntityType.ResourceDictionary:
                case EntityType.StyleRepository:
                    return GenerateResourceFile(container, entityType, compilation, ref spc);
                case EntityType.Theme:
                    return GenerateThemeFile(container, entityType, compilation, ref spc);
                default:
                    return GenerateControlFile(container, entityType, compilation, ref spc);
            }
        }

        private string GenerateThemeFile(AumlMetadataContainer container, EntityType entityType, Compilation compilation, ref SourceProductionContext spc)
        {
            var rootNode = container.RootNode as AumlAstObjectNode;
            var typeContainer = container.TypesMap[rootNode.Type.Namespace];
            var rootBaseType = typeContainer.Types.FirstOrDefault(x => x.Name == rootNode.Type.Name);
            var className = $"{container.FileName}{rootBaseType.Name}";
            var @namespace = $"{container.RootNamespace}.Themes";

            var textGenerator = new TextGenerator();
            textGenerator.WriteLine("// Autogenerated code");

            foreach (var ns in container.Usings)
            {
                textGenerator.WriteLine($"using {ns};");
            }

            var element = container.RootNode as AumlAstObjectNode;
            var properties = element.GetProperties();
            var themeName = string.Empty;
            List<AumlAstObjectNode> resourceDictionaries = null;
            List<AumlAstObjectNode> styleRepos = new List<AumlAstObjectNode>();

            foreach (var prop in properties)
            {
                var propertyRef = prop.Property as AumlAstPropertyReference;
                if (propertyRef.Name == "Resources")
                {
                    resourceDictionaries = prop.Values[0].GetObjects().ToList();
                }
                else if (propertyRef.Name == "StyleRepositories")
                {
                    foreach (var value in prop.Values)
                    {
                        if (value is AumlAstObjectNode node)
                        {
                            styleRepos.Add(node);
                        }
                    }
                }
                else if (propertyRef.Name == "Name")
                {
                    themeName = ((AumlAstTextNode)prop.Values[0]).Text;
                }
            }

            if (string.IsNullOrEmpty(themeName))
            {
                themeName = className;
            }

            textGenerator.NewLine();

            textGenerator.WriteLine($"namespace {@namespace};");
            textGenerator.NewLine();
            textGenerator.WriteLine($"public sealed class {className} : {rootBaseType.Name}");
            textGenerator.WriteOpenBraceAndIndent();
            textGenerator.WriteLine($@"public {className}() : base(""{themeName}"")");
            textGenerator.WriteOpenBraceAndIndent();
            textGenerator.WriteLine($@"ResourcePaths = new string[{resourceDictionaries.Count}];");
            textGenerator.WriteLine($@"for (int i = 0; i<ResourcePaths.Length; ++i)");
            textGenerator.WriteOpenBraceAndIndent();
            foreach (var r in resourceDictionaries)
            {
                var props = r.GetProperties();
                foreach(var p in props)
                {
                    if (p.Property is AumlAstPropertyReference reference && reference.Name == "Source")
                    {
                        textGenerator.WriteLine($@"ResourcePaths[i] = @""{p.Values[0]}"";");
                    }
                }
                
            }
            textGenerator.UnindentAndWriteCloseBrace();

            textGenerator.WriteLine($@"StylesPaths = new string[{styleRepos.Count}];");
            textGenerator.WriteLine($@"for (int i = 0; i<StylesPaths.Length; ++i)");
            textGenerator.WriteOpenBraceAndIndent();
            foreach (var r in styleRepos)
            {
                var props = r.GetProperties();
                foreach (var p in props)
                {
                    if (p.Property is AumlAstPropertyReference reference && reference.Name == "Source")
                    {
                        textGenerator.WriteLine($@"StylesPaths[i] = @""{p.Values[0]}"";");
                    }
                }
            }
            textGenerator.UnindentAndWriteCloseBrace();

            textGenerator.UnindentAndWriteCloseBrace();
            textGenerator.UnindentAndWriteCloseBrace();

            return textGenerator;
        }

        private string GenerateResourceFile(AumlMetadataContainer container, EntityType entityType, Compilation compilation, ref SourceProductionContext spc)
        { 
            var rootNode = container.RootNode as AumlAstObjectNode;
            var typeContainer = container.TypesMap[rootNode.Type.Namespace];
            var rootBaseType = typeContainer.Types.FirstOrDefault(x => x.Name == rootNode.Type.Name);
            var className = $"{container.FileName}{rootBaseType.Name}";
            var @namespace = $"{container.RootNamespace}.GeneratedResources";
            
            var textGenerator = new TextGenerator();
            textGenerator.WriteLine("// Autogenerated code");

            foreach (var ns in container.Usings)
            {
                textGenerator.WriteLine($"using {ns};");
            }

            textGenerator.NewLine();

            textGenerator.WriteLine($"namespace {@namespace};");
            textGenerator.NewLine();
            textGenerator.WriteLine($"public sealed class {className} : {rootBaseType.Name}");
            textGenerator.WriteOpenBraceAndIndent();
            textGenerator.WriteLine($"public {className}()");
            textGenerator.WriteOpenBraceAndIndent();
            textGenerator.WriteLine($@"Source = new Uri(@""{container.RelativeFilePath}"", UriKind.Relative);");
            textGenerator.UnindentAndWriteCloseBrace();

            int id = 1;
            textGenerator.WriteLine($"protected override void OnInitialize()");
            textGenerator.WriteOpenBraceAndIndent();
            ProcessResourceDictionary(ref spc, entityType, className, container, container.RootNode, textGenerator, ref id);
            textGenerator.UnindentAndWriteCloseBrace();
            textGenerator.UnindentAndWriteCloseBrace();

            return textGenerator;
        }

        private string GenerateControlFile(AumlMetadataContainer container, EntityType entityType, Compilation compilation, ref SourceProductionContext spc)
        {
            var rootNode = container.RootNode as AumlAstObjectNode;
            var directives = rootNode.Children.Where(x => x is AumlAstDirective).ToList();
            string className = String.Empty;
            string @namespace = String.Empty;
            var typeContainer = container.TypesMap[rootNode.Type.Namespace];
            var rootBaseType = typeContainer.Types.FirstOrDefault(x => x.Name == rootNode.Type.Name);
            AumlAstDirective classDirective = null;
            foreach (var aumlAstNode in directives)
            {
                var directive = (AumlAstDirective)aumlAstNode;
                if (directive.Name == AumlDirectives.Class)
                {
                    classDirective = directive;
                    break;
                }
            }
            
            var directiveValue = classDirective.Value as AumlAstTextNode;
            className = directiveValue.Text.Split('.').Last();
            var rootViewType = compilation.GetTypeByMetadataName(directiveValue.Text);
            @namespace = rootViewType.ContainingNamespace.ToDisplayString();

            var textGenerator = new TextGenerator();
            textGenerator.WriteLine("// Autogenerated code");

            foreach (var ns in container.Usings)
            {
                textGenerator.WriteLine($"using {ns};");
            }

            textGenerator.NewLine();

            textGenerator.WriteLine($"namespace {@namespace};");
            textGenerator.NewLine();
            textGenerator.WriteLine($"public partial class {className} : {rootBaseType.Name}");
            textGenerator.WriteOpenBraceAndIndent();

            int id = 1;
            foreach (var item in container.NamedElements)
            {
                var typeName = item.Element.Type.GetFullTypeName();
                typeContainer = container.TypesMap[item.Element.Type.Namespace];
                var typeInfo = typeContainer.Types.FirstOrDefault(x => x.Name == item.Element.Type.Name);
                textGenerator.WriteLine($"{typeInfo.ToDisplayString()} {item.Name};");
            }

            textGenerator.NewLine();

            textGenerator.WriteLine($"private void InitializeComponent()");
            textGenerator.WriteOpenBraceAndIndent();

            ProcessControlElements(ref spc, className, container, container.RootNode, textGenerator, ref id);
            textGenerator.UnindentAndWriteCloseBrace();
            textGenerator.UnindentAndWriteCloseBrace();

            return textGenerator;
        }

        private string ProcessControlElements(ref SourceProductionContext spc, string className, AumlMetadataContainer container, IAumlAstNode currentNode, TextGenerator textGenerator, ref int id)
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

                if (propertyRef.IsAttachedProperty)
                {
                    var types = container.TypesMap[propertyRef.OwnerType.Namespace];
                    var attachedTypeInfo = types.Types.FirstOrDefault(x => x.Name == propertyRef.OwnerType.Name);
                    //var symbolInfo = typeInfo.GetMemberByName(propertyRef.Name);
                    //if (symbolInfo.Kind == SymbolKind.Property)
                    {
                        textGenerator.WriteLine($"{propertyRef.OwnerType.Name}.Set{propertyRef.Name}({elementName}, {prop.GetTextValue()});");
                    }
                }
                else
                {
                    var symbolInfo = typeInfo.GetMemberByName(propertyRef.Name);
                    if (symbolInfo == null)
                    {
                        CreateDiagnostic(ref spc, className, $"There is no property named {propertyRef.Name} in {className}", DiagnosticSeverity.Error);
                        continue;
                    }
                    string symbolName = string.Empty;

                    symbolName = string.IsNullOrEmpty(elementName) ? propertyRef.Name : $"{elementName}.{propertyRef.Name}";

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
                                GenerateTextForType(symbolName, prop, value, propertyInfo, textGenerator);
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
                                var name = ProcessControlElements(ref spc, className, container, objectNode, textGenerator, ref id);

                                textGenerator.WriteLine($"{symbolName} = {name};");
                            }
                        }
                    }
                }
            }

            foreach (var obj in objects)
            {
                var name = ProcessControlElements(ref spc, className, container, obj, textGenerator, ref id);

                var isIContainer = typeInfo.ImplementsInterface("IContainer");
                if (typeInfo.FindPropertyAttributeByName("ContentAttribute", out var property))
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
        
        private string ProcessResourceDictionary(ref SourceProductionContext spc, EntityType entityType, string className, AumlMetadataContainer container, IAumlAstNode currentNode, TextGenerator textGenerator, ref int id)
        {
            var element = currentNode as AumlAstObjectNode;
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
            string elementName = string.Empty;

            var typeContainer = container.TypesMap[element.Type.Namespace];
            var typeInfo = typeContainer.Types.FirstOrDefault(x => x.Name == element.Type.Name);
            var properties = element.GetProperties();
            var objects = element.GetObjects();

            if (element != container.RootNode)
            {
                elementName = $"element_{id}";
                textGenerator.WriteLine($"var {elementName} = new {typeInfo.ToDisplayString()}();");
                id++;
            }

            foreach (var prop in properties)
            {
                var propertyRef = prop.Property as AumlAstPropertyReference;

                if (propertyRef.IsAttachedProperty)
                {
                    var types = container.TypesMap[propertyRef.OwnerType.Namespace];
                    var attachedTypeInfo = types.Types.FirstOrDefault(x => x.Name == propertyRef.OwnerType.Name);
                    textGenerator.WriteLine($"{propertyRef.OwnerType.Name}.Set{propertyRef.Name}({elementName}, {prop.GetTextValue()});");
                }
                else
                {
                    var symbolInfo = typeInfo.GetMemberByName(propertyRef.Name);
                    if (symbolInfo == null)
                    {
                        CreateDiagnostic(ref spc, className, $"There is no property named {propertyRef.Name} in {className}", DiagnosticSeverity.Error);
                        continue;
                    }
                    string symbolName = string.Empty;

                    symbolName = string.IsNullOrEmpty(elementName) ? propertyRef.Name : $"{elementName}.{propertyRef.Name}";

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
                                GenerateTextForType(symbolName, prop, value, propertyInfo, textGenerator);
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
                                var name = ProcessResourceDictionary(ref spc, entityType, className, container, objectNode, textGenerator, ref id);

                                textGenerator.WriteLine($"{symbolName} = {name};");
                            }
                        }
                    }
                }
            }

            foreach (var obj in objects)
            {
                var name = ProcessResourceDictionary(ref spc, entityType, className, container, obj, textGenerator, ref id);
                
                if (entityType == EntityType.StyleRepository && element.Type.Name == "Style")
                {
                    if (typeInfo.FindPropertyAttributeByName("ContentAttribute", out var property))
                    {
                        textGenerator.WriteLine($"{elementName}.{property.Name}.Add({name});");
                    }
                    else
                    {
                        textGenerator.WriteLine($"{elementName}.Triggers.Add({name});");
                    }
                }
            }

            if (!string.IsNullOrEmpty(key))
            {
                textGenerator.WriteLine($@"Add(""{key}"", {elementName});");
            }

            return elementName;
        }
        
        private void GenerateTextForType(string symbolName, AumlAstPropertyNode property, IAumlAstValueNode value, IPropertySymbol propertyInfo, TextGenerator textGenerator)
        {
            switch (propertyInfo.Type.SpecialType)
            {
                case SpecialType.None:
                    if (propertyInfo.Type.TypeKind == TypeKind.Enum)
                    {
                        textGenerator.WriteLine($@"{symbolName} = {propertyInfo.Type.Name}.{property.GetTextValue()};");
                    }
                    else
                    {
                        if (propertyInfo.Type.Name == "ImageSource")
                        {
                            textGenerator.WriteLine($"{symbolName} = new BitmapImage(new Uri(@\"file://{property.GetTextValue()}\"));");
                        }
                        else if (propertyInfo.Type.Name is "ColumnDefinitions" or "RowDefinitions")
                        {
                            var definitions = value.ToString().Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                            foreach (var def in definitions)
                            {
                                if (propertyInfo.Type.Name == "ColumnDefinitions")
                                {
                                    textGenerator.WriteLine($@"{symbolName}.Add(new ColumnDefinition(GridLength.Parse(""{def}"")));");
                                }
                                else if (propertyInfo.Type.Name == "RowDefinitions")
                                {
                                    textGenerator.WriteLine($@"{symbolName}.Add(new RowDefinition(GridLength.Parse(""{def}"")));");
                                }
                            }
                        }
                        else if (propertyInfo.Type.Name == "Template")
                        {
                            var templateNode = value as AumlAstTemplateNode;
                            textGenerator.WriteLine($"{symbolName} = ControlTemplate.Load({templateNode.TemplateContent})");
                        }
                        else if (propertyInfo.Type.Name == "Color")
                        {
                            textGenerator.WriteLine($@"{symbolName} = Colors.Get(""{property.GetTextValue()}"");");
                        }
                        else
                        {
                            textGenerator.WriteLine($@"{symbolName} = {propertyInfo.Type.Name}.Parse(""{property.GetTextValue()}"");");
                        }
                    }
                    break;
                case SpecialType.System_Enum:
                    textGenerator.WriteLine($@"{symbolName} = {propertyInfo.Type.Name}.{property.GetTextValue()};");
                    break;
                case SpecialType.System_Object:
                case SpecialType.System_String:
                    textGenerator.WriteLine($@"{symbolName} = ""{property.GetTextValue()}"";");
                    break;
                default:
                    textGenerator.WriteLine($@"{symbolName} = {property.GetTextValue().ToLower()};");
                    break;
            }
        }
        private void CreateDiagnostic(ref SourceProductionContext spc, string className, string diagnosticText, DiagnosticSeverity severity)
        {
            var descriptor = new DiagnosticDescriptor(
                id: "AumlCodeBehindGenerator",
                title: "Codebehind generation error",
                messageFormat: "{0}: {1}",
                category: "AumlCodeBehindGenerator",
                severity,
                isEnabledByDefault: true);
            var diagnostic = Diagnostic.Create(descriptor, Location.None, $"{className}.cs", diagnosticText);
            spc.ReportDiagnostic(diagnostic);

        }
    }
}