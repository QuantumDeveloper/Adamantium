using System;

namespace Adamantium.UI;

internal class EventSubscription
{
   public Delegate Handler { get; set; }
   public bool HandledEeventToo { get; set; }
}