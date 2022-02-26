using System.Collections;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Adamantium.Core;
using Adamantium.UI.Media;

namespace Adamantium.UI.Xaml;

public class XamlParser
{
    internal class XamlObject
    {
        public XamlObject()
        {
            Children = new List<XamlObject>();
        }
        
        public XamlObject Parent { get; set; }
        
        public object Element { get; set; } 
        
        public bool IsProperty { get; set; }
        
        public bool IsUIComponent { get; set; }
        
        public bool IsAdamantiumComponent { get; set; }
        
        public List<XamlObject> Children { get; }

        public override string ToString()
        {
            return Element.GetType().Name;
        }

        public override bool Equals(object? obj)
        {
            if (obj is XamlObject xamlObject)
            {
                return xamlObject.Element == Element;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Element.GetHashCode();
        }
    }

    static XamlParser()
    {
        assemblies["Adamantium.UI"] = Assembly.Load("Adamantium.UI");
    }
    
    private static string DefaultNamespace = "Adamantium.UI";
    
    private static Dictionary<XElement, XamlObject> objectsDict = new Dictionary<XElement, XamlObject>();
    private static Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>();
    
    public static IUIComponent Parse(string xamlString)
    {
        ArgumentNullException.ThrowIfNull(xamlString);
        var doc = XDocument.Parse(xamlString, LoadOptions.SetLineInfo);
        
        XamlObject rootComponent = null;
        object currentObject = null;
        

        foreach (var element in doc.Descendants())
        {
            var controlName = element.Name.LocalName;
            if (string.IsNullOrEmpty(controlName)) continue;

            if (rootComponent == null)
            {
                var ns = element.Name.Namespace.NamespaceName;
                if (string.IsNullOrEmpty(ns))
                {
                    ns = DefaultNamespace;
                }

                currentObject = Activator.CreateInstance(ns, $"{ns}.{controlName}")?.Unwrap();
                if (currentObject == null)
                {
                    int lineNumber = -1;
                    int linePosition = -1;
                    if (element is IXmlLineInfo info && info.HasLineInfo())
                    {
                        lineNumber = info.LineNumber;
                        linePosition = info.LinePosition;
                    }
                    throw new XamlParseException($"Not possible to create object {controlName} Line: {lineNumber} Position: {linePosition}");
                }
                
                rootComponent = new XamlObject();
                rootComponent.Element = currentObject;
                objectsDict[element] = rootComponent;

                rootComponent.IsUIComponent = currentObject is IUIComponent;
                rootComponent.IsAdamantiumComponent = currentObject is IAdamantiumComponent;
                
                ParseAttributes(element, rootComponent);
            }
            else
            {
                // If we have dot in an element name, this means we have deal not with control, but with a property
                if (element.Name.LocalName.Contains('.'))
                {
                    var propertyName = element.Name.LocalName.Split('.')[1];
                    var property = currentObject.GetType().GetProperty(propertyName);
                    var xamlObject = new XamlObject();
                    xamlObject.Element = property;
                    xamlObject.IsProperty = true;
                    xamlObject.Parent = objectsDict[element.Parent];
                    objectsDict[element] = xamlObject;
                    xamlObject.Parent.Children.Add(xamlObject);
                    
                    currentObject = property;
                }
                else
                {
                    currentObject = ParseObject(element);
                    if (currentObject != null)
                    {
                        var xamlObject = new XamlObject();
                        xamlObject.Element = currentObject;
                        objectsDict[element] = xamlObject;
                        var parent = objectsDict[element.Parent];
                        xamlObject.Parent = parent;
                        parent.Children.Add(xamlObject);
                        xamlObject.IsAdamantiumComponent = currentObject is IAdamantiumComponent;
                        xamlObject.IsUIComponent = currentObject is IUIComponent;
                        ParseAttributes(element, xamlObject);
                    }

                }
            }
        }

        var rootElement = FormHierarchy(rootComponent);

        return rootElement;
    }

    private static IUIComponent FormHierarchy(XamlObject rootObject)
    {
        var dict = new Dictionary<XamlObject, object>();
        IUIComponent root = null;
        object current = null;
        var stack = new Stack<XamlObject>();
        stack.Push(rootObject);
        while (stack.Count > 0)
        {
            var xamlObject = stack.Pop();

            if (root != null)
            {
                current = dict[xamlObject];
            }

            if (xamlObject.IsUIComponent && root == null)
            {
                root = xamlObject.Element as IUIComponent;
                current = root;
            }
            else if (xamlObject.IsUIComponent && root != null)
            {
                var properties = current.GetType().GetProperties();
                var content = properties.FirstOrDefault(x => x.GetCustomAttribute<ContentAttribute>() != null);
                if (content.PropertyType.GetInterface(nameof(IEnumerable)) != null)
                {
                    var collection = content.GetValue(current);
                    content.PropertyType.GetMethod("Add")?.Invoke(collection, new[] { xamlObject.Element });
                }
                else
                {
                    content.SetValue(current, xamlObject.Element);
                }
            }
            else if (xamlObject.IsAdamantiumComponent)
            {
                var parent = xamlObject.Parent;
                if (parent.IsProperty)
                {
                    var prop = parent.Element as PropertyInfo;
                    prop.SetValue(dict[parent], xamlObject.Element);
                }
                else if (parent.IsAdamantiumComponent)
                {
                    var properties = current.GetType().GetProperties();
                    var content = properties.FirstOrDefault(x => x.GetCustomAttribute<ContentAttribute>() != null);
                    if (content.PropertyType.GetInterface(nameof(IEnumerable)) != null)
                    {
                        var collection = content.GetValue(current);
                        content.PropertyType.GetMethod("Add")?.Invoke(collection, new[] { xamlObject.Element });
                    }
                    else
                    {
                        content.SetValue(current, xamlObject.Element);
                    }
                }

            }

            foreach (var obj in xamlObject.Children)
            {
                dict[obj] = xamlObject.Element;
                stack.Push(obj);
            }
        }
        return root;
    }

    private static void ParseAttributes(XElement element, XamlObject xamlObject)
    {
        if (!xamlObject.IsAdamantiumComponent) return;
        
        var component = xamlObject.Element as IAdamantiumComponent;
        var attributes = element.Attributes().ToList();
        var properties = component.GetType().GetProperties();
                    
        foreach (var attribute in attributes)
        {
            if (attribute.IsNamespaceDeclaration)
            {
                if (!assemblies.ContainsKey(attribute.Value))
                {
                    assemblies[attribute.Value] = Assembly.Load(attribute.Value);
                }
                continue;
            }
            
            var name = attribute.Name;
            var value = attribute.Value;
            var prop = properties.FirstOrDefault(x => x.Name == name);
            if (prop != null)
            {
                if (value.Contains("Binding"))
                {
                                
                }
                else
                {
                    if (Utilities.IsTypeInheritFrom(prop.PropertyType,  typeof(IAdamantiumComponent)))
                    {
                        var controlType = attribute.Name.LocalName;
                        var type = prop.PropertyType;

                        var ns = element.Name.Namespace.NamespaceName;
                        if (string.IsNullOrEmpty(ns))
                        {
                            ns = DefaultNamespace;
                        }
                        if (type == null) throw new XamlParseException($"Cannot find an object type {controlType} in namespace {ns}");

                        // TODO make generic processing for abstract types
                        var parseMethod = type.GetMethod("Parse");
                        if (parseMethod != null)
                        {
                            var result = parseMethod.Invoke(component, new[] { value });
                            prop.SetValue(component, result);
                        }
                    }
                    else if (prop.PropertyType.IsEnum)
                    {
                        if (!Enum.TryParse(prop.PropertyType, attribute.Value, true, out var result))
                        {
                            throw new XamlParseException($"");
                        }
                        prop.SetValue(component, result);
                    }
                    else if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string))
                    {
                        prop.SetValue(component, Convert.ChangeType(value, prop.PropertyType));
                    }
                    else
                    {
                        var val = prop.PropertyType.GetMethod("Parse").Invoke(component, new[] { value });
                        prop.SetValue(component, val);
                    }
                }
            }
        }
    }

    private static object ParseObject(XElement element)
    {
        var controlName = element.Name.LocalName;
        var type = assemblies[element.Name.NamespaceName].DefinedTypes.FirstOrDefault(x => x.Name == controlName);

        var ns = element.Name.Namespace.NamespaceName;
        if (string.IsNullOrEmpty(ns))
        {
            ns = DefaultNamespace;
        }
        if (type == null) throw new XamlParseException($"Cannot find an object type {controlName} in namespace {ns}");

        var currentObject = Activator.CreateInstance(type);
        return currentObject;

    }

    private static void ConvertType(object component, PropertyInfo info, string input)
    {
        
    }
    
}