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
            Children = new List<object>();
        }
        
        public object Parent { get; set; }
        
        public object Component { get; set; } 
        
        public bool IsProperty { get; set; }
        
        public bool IsUIComponent { get; set; }
        
        public bool IsAdamantiumComponent { get; set; }
        
        public List<object> Children { get; set; }
    }
    
    private static string DefaultNamespace = "Adamantium.UI";
    
    private static Dictionary<XElement, XamlObject> objectsDict = new Dictionary<XElement, XamlObject>();
    private static Assembly assembly = Assembly.Load("Adamantium.UI");
    
    public static IUIComponent Parse(string xamlString)
    {
        ArgumentNullException.ThrowIfNull(xamlString);
        
        var doc = XDocument.Parse(xamlString, LoadOptions.SetLineInfo);
        var objectStructure = new XamlObject();
        
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
                rootComponent.Component = currentObject;
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
                    currentObject = property;
                }
                else
                {
                    currentObject = ParseObject(element);
                    if (currentObject != null)
                    {
                        var xamlObject = new XamlObject();
                        xamlObject.Component = currentObject;
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

        return rootComponent.Component as IUIComponent;
    }

    private static void ParseAttributes(XElement element, XamlObject xamlObject)
    {
        if (!xamlObject.IsAdamantiumComponent) return;
        
        var component = xamlObject.Component as IAdamantiumComponent;
        var attributes = element.Attributes().ToList();
        var properties = component.GetType().GetProperties();
                    
        foreach (var attribute in attributes)
        {
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

                        if (type == typeof(Brush))
                        {
                            var brush = Brush.Parse(value);
                            prop.SetValue(component, brush);
                        }
                    }
                    else
                    {
                        prop.SetValue(component, Convert.ChangeType(value, prop.PropertyType));
                    }
                    
                }
            }
        }
    }

    private static object ParseObject(XElement element)
    {
        var controlName = element.Name.LocalName;
        var type = assembly.DefinedTypes.FirstOrDefault(x => x.Name == controlName);

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