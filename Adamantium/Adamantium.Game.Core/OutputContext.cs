using Object = System.Object;

namespace Adamantium.Game.Core
{
   /// <summary>
   /// The host's surface an output draws into - a window, a panel. What kinds there are is the host's business: its
   /// <see cref="IOutputFactory"/> decides.
   /// </summary>
   public class OutputContext:IEquatable<OutputContext>
   {
       /// <summary>
       /// Constructs OutputContext
       /// </summary>
       /// <param name="context">Object that represents surface on which Graphics content will be drawn</param>
       public OutputContext(Object context)
      {
         Context = context;
      }

      /// <summary>
      /// Object that represents surface on which Graphics content will be drawn
      /// </summary>
      public object Context { get; }

      /// <summary>
      /// Determines whether the specified object is equal to the current object.
      /// </summary>
      /// <returns>
      /// true if the specified object  is equal to the current object; otherwise, false.
      /// </returns>
      /// <param name="obj">The object to compare with the current object. </param><filterpriority>2</filterpriority>
      public override bool Equals(object obj)
      {
         if (obj == null || obj.GetType() != GetType())
            return false;

         var context = (OutputContext) obj;
         return Equals(context);
      }

      /// <summary>
      /// Indicates whether the current object is equal to another object of the same type.
      /// </summary>
      /// <returns>
      /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
      /// </returns>
      /// <param name="other">An object to compare with this object.</param>
      public bool Equals(OutputContext other)
      {
         return this == other;
      }


       /// <summary>
       /// Deep comparing of two <see cref="OutputContext"/>s
       /// </summary>
       /// <param name="context1">First <see cref="OutputContext"/></param>
       /// <param name="context2">Second <see cref="OutputContext"/></param>
       /// <returns>true if items are the same, otherwise - false</returns>
       public static bool operator ==(OutputContext context1, OutputContext context2)
       {
           if ((object) context1 == null && (object) context2 == null)
           {
               return true;
           }

           if ((object) context1 == null || (object) context2 == null)
           {
               return false;
           }

           return context1.Context == context2.Context;
       }

       /// <summary>
       /// Deep comparing of two <see cref="OutputContext"/>s
       /// </summary>
       /// <param name="context1">First <see cref="OutputContext"/></param>
       /// <param name="context2">Second <see cref="OutputContext"/></param>
       /// <returns>true if items are the same, otherwise - false</returns>
       public static bool operator !=(OutputContext context1, OutputContext context2)
       {
           return !(context1 == context2);
       }

       /// <summary>
       /// Serves as the default hash function. 
       /// </summary>
       /// <returns>
       /// A hash code for the current object.
       /// </returns>
       /// <filterpriority>2</filterpriority>
       public override int GetHashCode()
       {
           return Context.GetHashCode();
       }
   }
}
