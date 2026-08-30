using Adamantium.Mathematics;

namespace Adamantium.UI.Core;

public interface IRootVisualComponent : IUIComponent
{
   /// <summary>This surface is drawn ONCE - a bitmap bake, a designer preview, an off-screen test - so nothing inside it
   /// may leave work for "the next frame": there is none. A window says false, and its content is free to arrive over the
   /// frames that follow (see ContentPresenter.DeferContent).</summary>
   bool RendersOnce => false;

   /// <summary>A desktop point (physical pixels) in this surface's own LOGICAL coordinates. The one place the two
   /// units meet, and it needs this surface's scale to do it - see <see cref="PixelPoint"/>.</summary>
   Vector2 PointToClient(PixelPoint point);

   /// <summary>A point of this surface's own LOGICAL coordinates as a desktop point (physical pixels).</summary>
   PixelPoint PointToScreen(Vector2 point);

   void AttachContextAndInitialize(IUIContext context);

   /// <summary>Where this surface sits on the DESKTOP, in physical pixels. <see cref="Left"/>/<see cref="Top"/> are the
   /// same value as bindable numbers - a window's position is authored and serialized as two numbers - so anything that
   /// COMPUTES a position works with this, and only the property boundary is loose.</summary>
   PixelPoint Position { get; set; }

   /// <summary>Where the surface is RIGHT NOW, as the OS last reported it - readable from any thread.
   ///
   /// <para><see cref="Position"/> is a bindable property, so the platform can only assign it on the loop thread, which
   /// means posting to that thread's queue and waiting for it to be drained. That is fine for a layout that gets saved
   /// and for bindings, and far too slow for anything DRAWN from the window's place on the desktop: while a window is
   /// dragged, the frame would keep being built from where the window was one or two frames ago. A mica backdrop maps
   /// the wallpaper through this, and any lag turns into the backdrop sliding about instead of standing still.</para>
   ///
   /// <para>Defaults to <see cref="Position"/> for surfaces the OS does not move.</para></summary>
   PixelPoint LivePosition => Position;

   double Left { get; set; }

   double Top { get; set; }
        
   string Title { get; set; }
   
   Double ClientWidth { get; set; }

   Double ClientHeight { get; set; }
   
   IUIContext UIContext { get; }
}