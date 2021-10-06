using System;

namespace Adamantium.UI.Media
{
    public class VisualParentChangedEventArgs : EventArgs
    {
        public VisualParentChangedEventArgs(VisualComponent oldParent, VisualComponent newParent)
        {
            OldParent = oldParent;
            NewParent = newParent;
        }

        public VisualComponent OldParent { get; }

        public VisualComponent NewParent { get; }
    }
}