using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Extensions;

public static class WindowExtension
{
    public static void Update(this IWindow window, IThemeManager themeManager, AppTime appTime)
    {
        ProcessVisualTree(window, themeManager, UpdateComponent);
    }

    /// <summary>Runs the same per-component measure/arrange update over an arbitrary subtree (no IWindow/theme
    /// required). Used by layout tests that drive two frames to reproduce a dynamic-relayout bug.</summary>
    public static void UpdateTree(IUIComponent root)
    {
        ProcessVisualTree(root, null, UpdateComponent);
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
        if (LayoutTrace.Enabled)
        {
            var name = string.IsNullOrEmpty(control.Name) ? control.GetType().Name : control.Name;
            LayoutTrace.Log($"UPDATE {name}: measureValid={control.IsMeasureValid} arrangeValid={control.IsArrangeValid} parent={(control.LogicalParent as IName)?.Name} parentDesired={(parent?.DesiredSize)}");
        }

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
            else if (control.VisualParent is not IMeasurableComponent visualParent || visualParent.IsMeasureValid)
            {
                // This is the TOP-most invalid node in its branch, so measure it directly. When an ANCESTOR is also
                // invalid we must NOT measure here: that ancestor re-measures this node hierarchically with the real
                // parent-given constraint. Measuring standalone uses control.Width/Height, which for an auto-sized
                // control is NaN -> Size.Infinity - and that bogus unbounded measure is what corrupted a virtualizing
                // panel re-invalidated mid-pass (its RaiseMetrics -> scrollbar -> InvalidateMeasure fires AFTER the
                // window was already visited this DFS pass, re-invalidating the panel below it): the panel realized a
                // huge window while being arranged to the small viewport -> the list jumped sideways / spilled past its
                // clip. Deferring lets the next pass measure it correctly from the (still-invalid) window down.
                MeasureControl(control, control.Width, control.Height);
            }
        }

        if (!control.IsArrangeValid)
        {
            var rect = parent != null ? new Rect(parent.DesiredSize) : new Rect(control.DesiredSize);
            if (LayoutTrace.Enabled) LayoutTrace.Log($"UPDATE {(string.IsNullOrEmpty(control.Name) ? control.GetType().Name : control.Name)}: -> Arrange(new Rect(parent.DesiredSize)) = {rect}");
            control.Arrange(rect);
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