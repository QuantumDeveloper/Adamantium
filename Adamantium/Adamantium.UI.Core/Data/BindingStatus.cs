namespace Adamantium.UI.Core.Data;

public enum BindingStatus
{
   NotAttached = 0,
   Inactive = 1,
   Active = 2,
   Detached = 3,
   AsyncRequestPending = 4,
   PathError = 5,
   UpdateTargetError = 6,
   UpdateSourceError = 7
}