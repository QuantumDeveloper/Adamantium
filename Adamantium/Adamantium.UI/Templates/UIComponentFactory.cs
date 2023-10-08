using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Adamantium.UI.Markup;

namespace Adamantium.UI.Templates;

public class UIComponentFactory
{
    public UIComponentFactory(AumlTemplateContainer container)
    {
        AumlContainer = container; 
    }

    public AumlTemplateContainer AumlContainer { get; }
    
    public TemplateResult Build()
    {
        var templateResult = new TemplateResult();
        var stack = new Stack<IAumlAstNode>();
        stack.Push(AumlContainer.RootNode);

        IMeasurableComponent root = null;

        var templateNode = AumlContainer.RootNode as AumlAstObjectNode;
        foreach (var child in templateNode.Children)
        {
            if (child is AumlAstObjectNode objectNode)
            {
                root = (IMeasurableComponent)ProcessElement(root, objectNode);
            }
            else if (child is AumlAstPropertyNode property)
            {
                if (property.Property is AumlAstPropertyReference propertyReference)
                {
                    var prop = root?.GetType().GetProperties()
                        .FirstOrDefault(x => x.Name == propertyReference.Name);

                    foreach (var value in property.Values)
                    {
                        //prop.SetValue(current, value);
                    }

                    foreach (var value in property.Values)
                    {
                        if (value is AumlAstObjectNode node)
                        {
                            var result = ProcessElement(root, node);
                            
                        }
                    }
                }
            }
            else if (child is AumlAstDirective directive)
            {

            }
        }

        templateResult.RootComponent = root;
        return templateResult;
    }

    private bool IsAdamantiumComponent(object component)
    {
        return component is IAdamantiumComponent;
    }

    private object ProcessElement(IAdamantiumComponent parent, AumlAstObjectNode node)
    {
        var stack = new Stack<IAumlAstNode>();
        stack.Push(node);
        object current = null;
        while (stack.Count > 0)
        {
            var element = stack.Pop() as AumlAstObjectNode;
            if (element != AumlContainer.RootNode)
            {
                var type = AumlContainer.TypesMap[element.Type.GetFullTypeName()];
                current = GenerateComponent(type);
                if (parent is IContainer container && IsAdamantiumComponent(current))
                {
                    container.AddOrSetChildComponent(current);
                }
            }

            foreach (var child in element.Children)
            {
                if (child is AumlAstObjectNode astNode)
                {
                    ProcessElement((IAdamantiumComponent)current, astNode);
                }
                else if (child is AumlAstPropertyNode property)
                {
                    if (property.Property is AumlAstPropertyReference propertyReference)
                    {
                        var prop = current?.GetType().GetProperties()
                            .FirstOrDefault(x => x.Name == propertyReference.Name);

                        foreach (var value in property.Values)
                        {
                            if (value is AumlAstTextNode textNode)
                            {

                                SetProperty(current, prop, textNode);
                            }
                            
                            if (value is AumlAstObjectNode objectNode)
                            {
                                var result = ProcessElement((IAdamantiumComponent)current, objectNode);
                                prop.SetValue(current, result);
                            }
                        }
                    }
                }
                else if (child is AumlAstDirective directive)
                {
                    
                }
            }
        }

        return current;
    }

    private void SetProperty(object obj, PropertyInfo property, AumlAstTextNode textValue)
    {
        var propertyType = property.PropertyType;
        if (propertyType == typeof(string))
        {
            property.SetValue(obj, textValue.Text);
        }
        else if (propertyType.IsEnum)
        {
            var enumValue = Enum.Parse(propertyType, textValue.Text);
            property.SetValue(obj, enumValue);
        }
        else if (propertyType == typeof(IList<>))
        {
            //propertyType.GetMethod("Add").Invoke(property.GetValue(obj));
        }
        else
        {
            var parseMethod = propertyType.GetMethods().FirstOrDefault(x => x.Name == "Parse");
            if (parseMethod != null)
            {
                var parseResult = parseMethod.Invoke(obj, new object[] { textValue.Text });
                property.SetValue(obj, parseResult);
            }
        }
    }

    private object GenerateComponent(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        
        var component = Activator.CreateInstance(type);
        
        ArgumentNullException.ThrowIfNull(component);

        return component;
    }
}