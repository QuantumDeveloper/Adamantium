using Adamantium.UI.Core.Controls;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Core;

public interface IControl : ITemplatedUIComponent
{
    Brush Background { get; set; }

    Brush Foreground { get; set; }
}
