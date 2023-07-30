﻿using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Controls;

namespace Adamantium.Game.Core
{
   /// <summary>
   /// Contains width, height, hwnd and Control itself, to which D3D will render its content
   /// </summary>
   public class GameContext:IEquatable<GameContext>
   {
       /// <summary>
       /// Constructs GameContext
       /// </summary>
       /// <param name="context">Object that represents surface on which Graphics content will be drawn</param>
       /// <param name="graphicsDevice">Graphics device on which current context was created</param>
       /// <exception cref="NotSupportedException"></exception>
       public GameContext(Object context, GraphicsDevice graphicsDevice = null)
      {
         var type = context.GetType();
         if (Utilities.IsTypeInheritFrom(type, typeof(IWindow)))
         {
            ContextType = GameContextType.Window;
         }
         else if (Utilities.IsTypeInheritFrom(type, typeof(RenderTargetPanel)))
         {
            ContextType = GameContextType.RenderTargetPanel;
         }
         else
         {
            throw new NotSupportedException($"context of type {type} is not supported");
         }
         Context = context;
         GraphicsDevice = graphicsDevice;
      }

      /// <summary>
      /// Object that represents surface on which Graphics content will be drawn
      /// </summary>
      public object Context { get; }

      /// <summary>
      /// Type of Game context
      /// </summary>
      public GameContextType ContextType { get; }
      
      /// <summary>
      /// Device on which context was created 
      /// </summary>
      public GraphicsDevice GraphicsDevice { get; }

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

         var context = (GameContext) obj;
         return Equals(context);
      }

      /// <summary>
      /// Indicates whether the current object is equal to another object of the same type.
      /// </summary>
      /// <returns>
      /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
      /// </returns>
      /// <param name="other">An object to compare with this object.</param>
      public bool Equals(GameContext other)
      {
         return this == other;
      }


       /// <summary>
       /// Deep comparing of two <see cref="GameContext"/>s
       /// </summary>
       /// <param name="context1">First <see cref="GameContext"/></param>
       /// <param name="context2">Second <see cref="GameContext"/></param>
       /// <returns>true if items are the same, otherwise - false</returns>
       public static bool operator ==(GameContext context1, GameContext context2)
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
       /// Deep comparing of two <see cref="GameContext"/>s
       /// </summary>
       /// <param name="context1">First <see cref="GameContext"/></param>
       /// <param name="context2">Second <see cref="GameContext"/></param>
       /// <returns>true if items are the same, otherwise - false</returns>
       public static bool operator !=(GameContext context1, GameContext context2)
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
           int hashCode = 1;
           if (Context != null)
           {
               return HashCode.Combine(Context.GetHashCode(), GraphicsDevice.GetHashCode());
           }

           return hashCode;
       }
   }
}