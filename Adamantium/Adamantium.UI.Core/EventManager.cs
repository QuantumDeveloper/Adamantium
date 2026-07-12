using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core;

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

   /// <summary>Resolves a routed event by its markup name, so an AUML attribute can name one (e.g. an
   /// <c>EventTrigger Event="Loaded"</c>). Accepts a bare event name (first owner that declares it wins - names like
   /// Loaded/MouseDown are effectively unique) or an <c>Owner.Event</c> qualified form to disambiguate. Returns null if
   /// no registered event matches (the owner type's static ctor may not have run yet - but by template-build time every
   /// base control that declares the common events is initialised).</summary>
   public static RoutedEvent FindRoutedEvent(String name)
   {
      if (String.IsNullOrEmpty(name)) return null;

      String ownerName = null, eventName = name;
      var dot = name.LastIndexOf('.');
      if (dot >= 0)
      {
         ownerName = name.Substring(0, dot);
         eventName = name.Substring(dot + 1);
      }

      lock (routedEvents)
      {
         foreach (var pair in routedEvents)
         {
            if (ownerName != null && pair.Key.Name != ownerName) continue;
            foreach (var routedEvent in pair.Value)
               if (routedEvent.Name == eventName) return routedEvent;
         }
      }

      return null;
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