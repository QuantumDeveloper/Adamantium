using System;
using System.Collections.Generic;
using Adamantium.UI.Data;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls;

public interface IContentControl : IControl, IContainer
{
    object Content { get; set; }
}