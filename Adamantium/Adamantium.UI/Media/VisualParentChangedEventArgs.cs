using System;

namespace Adamantium.UI.Media
{
    public class VisualParentChangedEventArgs : EventArgs
    {
        public VisualParentChangedEventArgs(IUIComponent oldParent, IUIComponent newParent)
        {
            OldParent = oldParent;
            NewParent = newParent;
        }

        public IUIComponent OldParent { get; }

        public IUIComponent NewParent { get; }
    }
}