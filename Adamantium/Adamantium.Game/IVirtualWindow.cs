using System;
using Adamantium.Game.Core;
using Adamantium.UI.Controls;

namespace Adamantium.Game;

public interface IVirtualWindow : IWindow
{
    GameOutput RootWindow { get; set; }
}