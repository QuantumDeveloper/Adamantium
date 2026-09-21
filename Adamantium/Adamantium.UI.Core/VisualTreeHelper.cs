namespace Adamantium.UI.Core;

public static class VisualTreeHelper
{
    public static object FindElementByName(IUIComponent uiComponent, string name)
    {
        var stack = new Stack<IUIComponent>();
        while (stack.Count > 0)
        {
            var element = stack.Pop();

            if (element.Name.Equals(name)) return element;

            foreach (var visualChild in element.VisualChildren)
            {
                stack.Push(visualChild);
            }
        }

        return null;
    }
}