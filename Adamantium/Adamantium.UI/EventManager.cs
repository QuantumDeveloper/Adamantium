using System;
using System.Collections.Generic;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI;

public static class EventManager
{
   private static Dictionary<Type, List<RoutedEvent>> routedEvents = new Dictionary<Type, List<RoutedEvent>>();

   private static Dictionary<RoutedEvent, List<ClassEventSubsription>> classEventSubscriptions =
      new Dictionary<RoutedEvent, List<ClassEventSubsription>>();

   public static RoutedEvent[] GetRoutedEvents()
   {
      lock (routedEvents)
      {
         List<RoutedEvent> eventsList = new List<RoutedEvent>();
         foreach (var routedEvent in eventsList)
         {
            eventsList.AddRange(new[] {routedEvent});
         }
         return eventsList.ToArray();
      }
   }

   public static RoutedEvent[] GetRoutedEvents(Type ownerType)
   {
      lock (routedEvents)
      {
         if (routedEvents.ContainsKey(ownerType))
         {
            return routedEvents[ownerType].ToArray();
         }
      }
      return null;
   }

   public static RoutedEvent RegisterRoutedEvent(String name, RoutingStrategy routingStrategy, Type eventHandlerType, Type eventOwnerType)
   {
      lock (routedEvents)
      {
         if (String.IsNullOrEmpty(name))
         {
            throw new ArgumentNullException(nameof(name));
         }

         if (eventHandlerType == null)
         {
            throw new ArgumentNullException(nameof(eventHandlerType));
         }

         if (eventOwnerType == null)
         {
            throw new ArgumentNullException(nameof(eventOwnerType));
         }

         RoutedEvent routedEvent = null;

         if (routedEvents.ContainsKey(eventOwnerType))
         {
            List<RoutedEvent> eventsList = routedEvents[eventOwnerType];
            foreach (var @event in eventsList)
            {
               if (@event.Name == name)
               {
                  throw new ArgumentException(eventOwnerType.Name + " class already contains routed event with name "+ name );
               }
            }
            routedEvent = new RoutedEvent(name, routingStrategy, eventHandlerType, eventOwnerType);
            eventsList.Add(routedEvent);
         }
         else
         {
            routedEvent = new RoutedEvent(name, routingStrategy, eventHandlerType, eventOwnerType);
            routedEvents.Add(eventOwnerType, new List<RoutedEvent>() {routedEvent});
         }
            

         return routedEvent;
      }
   }

   public static void RegisterClassHandler(Type classType, RoutedEvent routedEvent, Delegate handler, Boolean handledEventsToo = false)
   {
      routedEvent.RegisterClassHandler(classType, handler, handledEventsToo);
   }

   public static void RegisterClassHandler<T>(RoutedEvent routedEvent, Delegate handler, Boolean handledEventsToo = false)
   {
      routedEvent.RegisterClassHandler<T>(handler, handledEventsToo);
   }
}