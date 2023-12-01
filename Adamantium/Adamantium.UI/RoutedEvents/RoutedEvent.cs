using System;
using System.Collections.Generic;
using System.Reflection;

namespace Adamantium.UI.RoutedEvents;

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
         ClassEventSubsription subscription = new ClassEventSubsription
         {
            HandledEventsToo = handledEventsToo,
            Handler = handler,
            TargetType = classType
         };

         classEventSubscriptions.Add(subscription);
      }
   }

   internal void RegisterClassHandler<T>(Delegate handler, Boolean handledEventsToo = false)
   {
      lock (classEventSubscriptions)
      {
         ClassEventSubsription subscription = new ClassEventSubsription
         {
            HandledEventsToo = handledEventsToo,
            Handler = handler,
            TargetType = typeof(T)
         };

         classEventSubscriptions.Add(subscription);
      }
   }

   internal void InvokeClassHandlers(object sender, RoutedEventArgs e)
   {
      lock (classEventSubscriptions)
      {
         foreach (var subscription in classEventSubscriptions)
         {
            if (subscription.TargetType.GetTypeInfo().IsAssignableFrom(sender.GetType().GetTypeInfo()) &&
                (!e.Handled || subscription.HandledEventsToo))
            {
               subscription.Handler.DynamicInvoke(sender, e);
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