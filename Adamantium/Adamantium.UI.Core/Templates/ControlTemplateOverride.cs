using Adamantium.UI.Core.Collections;
using Adamantium.UI.Core.Controls;

namespace Adamantium.UI.Core.Templates;

public static class ControlTemplateOverride
{
    public static readonly AdamantiumProperty OverridesProperty =
        AdamantiumProperty.RegisterAttached(
            "Overrides",
            typeof(TemplateOverridesCollection),
            typeof(ITemplatedUIComponent),
            new PropertyMetadata(null));

    public static void SetOverrides(IUIComponent element, TemplateOverridesCollection value)
    {
        element.SetValue(OverridesProperty, value);
    }

    public static TemplateOverridesCollection GetOverrides(IUIComponent element)
    {
        return element.GetValue<TemplateOverridesCollection>(OverridesProperty);
    }
}