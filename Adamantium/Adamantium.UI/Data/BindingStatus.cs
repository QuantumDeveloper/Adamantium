namespace Adamantium.UI.Data;

public enum BindingStatus
{
   NotAttached = 0,
   Inactive = 1,
   Active = 2,
   Detached = 3,
   AsyncRequsetPending = 4,
   PathError = 5,
   UpdateTargetError = 6,
   UpdateSourceError = 7
}