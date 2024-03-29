﻿using System;

namespace Adamantium.UI.RoutedEvents
{
   public class RoutedEventArgs:EventArgs
   {
      public bool Handled { get; set; }
      public object OriginalSource { get; set; }
      public object Source { get; set; }
      public RoutedEvent RoutedEvent { get; set; }

      public RoutedEventArgs()
      { }

      public RoutedEventArgs(RoutedEvent routedEvent)
      {
         RoutedEvent = routedEvent;
      }

      public RoutedEventArgs(RoutedEvent routedEvent, UIComponent originalSource)
      {
         RoutedEvent = routedEvent;
         OriginalSource = originalSource;
      }
   }
}
