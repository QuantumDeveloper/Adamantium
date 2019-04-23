using System;
using System.Collections.Generic;
using System.Reflection;

namespace Adamantium.UI
{
   public sealed class RoutedEvent
   {
      private readonly List<ClassEventSubsription> classEventSubscriptions = new List<ClassEventSubsription>(); 

      public Int32 GlobalIndex { get;}
      public String Name { get; private set; }
      public RoutingStrategy RoutingStrategy { get; private set; }
      public Type EventHandlerType { get; private set; }
      public Type EventOwnerType { get; private set; }

      private static Int32 globalIndex = 1;

      internal RoutedEvent(String name, RoutingStrategy routingRoutingStrategy, Type eventHandlerType, Type eventOwnerType)
      {
         GlobalIndex = globalIndex++;
         Name = name;
         RoutingStrategy = routingRoutingStrategy;
         EventHandlerType = eventHandlerType;
         EventOwnerType = eventOwnerType;
      }

      internal void RegisterClassHandler(Type classType, Delegate handler, Boolean handledEventsToo = false)
      {
         lock (classEventSubscriptions)
         {
            ClassEventSubsription subsription = new ClassEventSubsription
            {
               HandledEeventToo = handledEventsToo,
               Handler = handler,
               TargetType = classType
            };

            classEventSubscriptions.Add(subsription);
         }
      }

      internal void RegisterClassHandler<T>(Delegate handler, Boolean handledEventsToo = false)
      {
         lock (classEventSubscriptions)
         {
            ClassEventSubsription subsription = new ClassEventSubsription
            {
               HandledEeventToo = handledEventsToo,
               Handler = handler,
               TargetType = typeof(T)
            };

            classEventSubscriptions.Add(subsription);
         }
      }

      internal void InvokeClassHandlers(object sender, RoutedEventArgs e)
      {
         lock (classEventSubscriptions)
         {
            foreach (var subsription in classEventSubscriptions)
            {
               if (subsription.TargetType.GetTypeInfo().IsAssignableFrom(sender.GetType().GetTypeInfo()) &&
                   (!e.Handled || subsription.HandledEeventToo))
               {
                  subsription.Handler.DynamicInvoke(sender, e);
               }
            }
         }
      }

      public override int GetHashCode()
      {
         return GlobalIndex;
      }

      public override string ToString()
      {
         return GetType().Name +"."+Name;
      }
   }
}
