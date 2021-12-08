using System.Collections.Generic;

namespace Adamantium.UI.Rendering
{
    internal class UIPresentationContainer
    {
        public List<UIPresentationItem> Items { get; }

        public UIPresentationContainer()
        {
            Items = new List<UIPresentationItem>();
        }

        public void AddItem(UIPresentationItem item)
        {
            Items.Add(item);
        }
        
        public void DisposeAndClearItems()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].Dispose();
            }
            Items.Clear();
        }
    }
}