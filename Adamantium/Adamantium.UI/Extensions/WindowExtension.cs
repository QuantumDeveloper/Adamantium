using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Extensions;

public static class WindowExtension
{
    public static void Update(this IWindow window, IThemeManager themeManager, AppTime appTime)
    {
        ProcessVisualTree(window, themeManager, UpdateComponent);
    }
    
    private static void ProcessVisualTree(IUIComponent component, IThemeManager themeManager, Action<IUIComponent> processAction)
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
        var parent = control.LogicalParent as IMeasurableComponent;
        if (!control.IsStyleApplied)
        {
            control.ApplyCurrentTheme();
        }
        
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
            control.Arrange(parent != null ? new Rect(parent.DesiredSize) : new Rect(control.DesiredSize));
        }
    }

    private static void MeasureControl(IMeasurableComponent control, Double width, Double height)
    {
        if (!Double.IsNaN(width) && !Double.IsNaN(height))
        {
            var s = new Size(width, height);
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