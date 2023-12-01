using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.UI.Controls;
using Adamantium.UI.Resources;

namespace Adamantium.UI.Extensions;

public static class WindowExtension
{
    public static void Update(this IWindow window, IThemeManager themeManager, AppTime appTime)
    {
        ProcessVisualTree(window, themeManager, UpdateComponent);
        //ProcessVisualTree(window, themeManager, UpdateComponentLocation);
    }
    
    private static void ProcessVisualTree(IUIComponent component, IThemeManager themeManager, Action<IUIComponent> processAction)
    {
        var stack = new Stack<IUIComponent>();
        stack.Push(component);
        while (stack.Count > 0)
        {
            var control = stack.Pop();
            themeManager.ApplyStyles(component);
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
        var parent = control.LogicalParent as IMeasurableComponent;
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
        
        UpdateComponentLocation(visualComponent);
    }

    private static void UpdateComponentLocation(IUIComponent visualComponent)
    {
        if (visualComponent.LogicalParent != null)
        {
            visualComponent.Location = visualComponent.Bounds.Location + ((IUIComponent)visualComponent.LogicalParent).Location;
            visualComponent.ClipPosition = visualComponent.ClipRectangle.Location + ((IUIComponent)visualComponent.LogicalParent).Location;
        }
        else
        {
            visualComponent.Location = visualComponent.Bounds.Location;
            visualComponent.ClipPosition = visualComponent.ClipRectangle.Location;
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