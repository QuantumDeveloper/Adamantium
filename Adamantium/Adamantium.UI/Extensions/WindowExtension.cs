using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Extensions;

public static class WindowExtension
{
    public static void Update(this IWindow window, AppTime appTime)
    {
        ProcessVisualTree(window, UpdateComponent);
        ProcessVisualTree(window, UpdateComponentLocation);
    }
    
    private static void ProcessVisualTree(IUIComponent component, Action<IUIComponent> processAction)
    {
        var stack = new Stack<IUIComponent>();
        stack.Push(component);
        while (stack.Count > 0)
        {
            var control = stack.Pop();
            
            processAction(control);

            foreach (var visual in control.GetVisualDescendants())
            {
                stack.Push(visual);
            }
        }
    }
        
    private static void UpdateComponent(IUIComponent visualComponent)
    {
        var control = (MeasurableUIComponent)visualComponent;
        var parent = control.VisualParent as IMeasurableComponent;
        if (!control.IsMeasureValid)
        {
            if (control is IWindow wnd)
            {
                MeasureControl(control, wnd.ClientWidth, wnd.ClientHeight);
            }
            else
            {
                MeasureControl(control, control.Width, control.Height);
            }
        }
            
        if (!control.IsArrangeValid)
        {
            if (parent != null)
            {
                control.Arrange(new Rect(parent.DesiredSize));
            }
            else
            {
                control.Arrange(new Rect(control.DesiredSize));
            }
        }
            
        // if (control.Parent != null)
        // {
        //     control.Location = control.Bounds.Location + control.Parent.Location;
        //     control.ClipPosition = control.ClipRectangle.Location + control.Parent.Location;
        // }
    }

    private static void UpdateComponentLocation(IUIComponent visualComponent)
    {
        if (visualComponent.LogicalParent != null)
        {
            visualComponent.Location = visualComponent.Bounds.Location + ((IUIComponent)(visualComponent.LogicalParent)).Location;
            visualComponent.ClipPosition = visualComponent.ClipRectangle.Location + ((IUIComponent)(visualComponent.LogicalParent)).Location;
        }
    }

    private static void MeasureControl(IMeasurableComponent control, Double width, Double height)
    {
        if (!Double.IsNaN(width) && !Double.IsNaN(height))
        {
            Size s = new Size(width, height);
            control.Measure(s);
        }
        else if (Double.IsNaN(width) && !Double.IsNaN(height))
        {
            control.Measure(new Size(Double.PositiveInfinity, height));
        }
        else if (!Double.IsNaN(width) && Double.IsNaN(height))
        {
            control.Measure(new Size(width, Double.PositiveInfinity));
        }
        else
        {
            control.Measure(Size.Infinity);
        }
    }
}