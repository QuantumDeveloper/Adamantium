using System.Reflection;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core.RoutedEvents;

// [TypeParser] so a markup attribute string (EventTrigger Event="Loaded") resolves to the registered event, like Brush
// resolves via BrushParser.
[TypeParser(typeof(RoutedEventParser))]
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

   public void RegisterClassHandler(Type classType, Delegate handler, Boolean handledEventsToo = false)
   {
      lock (classEventSubscriptions)
      {
         var subscription = new ClassEventSubsription
         {
            HandledEventsToo = handledEventsToo,
            Handler = handler,
            TargetType = classType
         };

         classEventSubscriptions.Add(subscription);
      }
   }

   public void RegisterClassHandler<T>(Delegate handler, Boolean handledEventsToo = false)
   {
      lock (classEventSubscriptions)
      {
         var subscription = new ClassEventSubsription
         {
            HandledEventsToo = handledEventsToo,
            Handler = handler,
            TargetType = typeof(T)
         };

         classEventSubscriptions.Add(subscription);
      }
   }

   public void InvokeClassHandlers(object sender, RoutedEventArgs e)
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