using System;
using Adamantium.Multiverse;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;

namespace Adamantium.UI.Universes;

public interface IVirtualWindow : IWindow
{
    UniverseOutput RootWindow { get; set; }
}