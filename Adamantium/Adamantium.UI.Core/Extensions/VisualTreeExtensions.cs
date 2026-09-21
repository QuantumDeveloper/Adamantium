namespace Adamantium.UI.Core.Extensions;

public static class VisualTreeExtensions
{
    public static void TraverseVisualTree(this IUIComponent component, Action<IUIComponent> action)
    {
        var stack = new Stack<IUIComponent>();
        stack.Push(component);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            
            action(current);
            
            foreach (var uiComponent in current.VisualChildren.Reverse())
            {
                stack.Push(uiComponent);
            }
        }
    }
}