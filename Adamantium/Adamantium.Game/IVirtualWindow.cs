using System;
using Adamantium.Game.Core;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;

namespace Adamantium.Game;

public interface IVirtualWindow : IWindow
{
    UniverseOutput RootWindow { get; set; }
}