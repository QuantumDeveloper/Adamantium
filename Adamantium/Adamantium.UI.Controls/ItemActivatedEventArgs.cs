using System;

namespace Adamantium.UI.Controls;

/// <summary>WHICH ITEM WAS OPENED - what a double click on a row means, said with the item it landed on.</summary>
public class ItemActivatedEventArgs : EventArgs
{
    public ItemActivatedEventArgs(object item) => Item = item;

    /// <summary>The item the row stood for - the application's own object, not the container showing it.</summary>
    public object Item { get; }
}
