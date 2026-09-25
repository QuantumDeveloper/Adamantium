using System.Runtime.CompilerServices;
using Object = System.Object;

namespace Adamantium.Game
{
   /// <summary>
   /// The host's surface an output draws into - a window, a panel. What kinds there are is the host's business: its
   /// <see cref="IOutputFactory"/> decides.
   /// </summary>
   public class OutputContext:IEquatable<OutputContext>
   {
       public OutputContext(Object context)
      {
         Context = context;
      }

      /// <summary>
      /// The host's surface the content is drawn on.
      /// </summary>
      public object Context { get; }

      public override bool Equals(object obj)
      {
         if (obj == null || obj.GetType() != GetType())
            return false;

         var context = (OutputContext) obj;
         return Equals(context);
      }

      public bool Equals(OutputContext other)
      {
         return this == other;
      }


       /// <summary>
       /// Equal when both wrap the same surface.
       /// </summary>
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

       public static bool operator !=(OutputContext context1, OutputContext context2)
       {
           return !(context1 == context2);
       }

       public override int GetHashCode()
       {
           return RuntimeHelpers.GetHashCode(Context);
       }
   }
}
