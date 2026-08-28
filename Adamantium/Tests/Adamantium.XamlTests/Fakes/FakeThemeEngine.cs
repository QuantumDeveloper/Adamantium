using System;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;

namespace Adamantium.XamlTests;

internal sealed class FakeThemeEngine : IThemeEngine
{
    public void ApplyCurrentTheme(IFundamentalUIComponent control) { }
    public void ApplyStyles(IFundamentalUIComponent component) { }
    public void ApplyExternalStyles(IFundamentalUIComponent control, params ReadOnlySpan<Style> styles) { }
}
