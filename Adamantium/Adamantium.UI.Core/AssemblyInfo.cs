using Adamantium.UI.Core.Markup;

// Core presentation types (Media/geometry/brushes/transforms, Resources/styles, Imaging) live under the single
// default namespace alongside the controls — WPF-style, no prefix. The resolver maps by assembly, so this one
// entry brings the whole Adamantium.UI.Core assembly under "http://adamantium/ui" (merged with Adamantium.UI.Controls).
[assembly: XmlnsDefinition("http://adamantium/ui", "clr-namespace:Adamantium.UI.Core.Media;assembly=Adamantium.UI.Core")]
// Markup language features stay on the x: prefix (x:Name, x:Key, …).
[assembly: XmlnsDefinition("http://adamantium/ui/xaml/extensions", "clr-namespace:Adamantium.UI.Markup.MarkupExtensions;assembly=Adamantium.UI.Core", "x")]