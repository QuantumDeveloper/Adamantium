using Adamantium.Graphics.Core;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Panels;

/// <summary>
/// Displays frames produced by an external engine/process (possibly another graphics API). The producer renders
/// into a shared surface and hands its <see cref="SharedSurfaceDescriptor"/> to this panel via
/// <see cref="SetSource"/>; the panel imports it zero-copy and samples it during compositing. With no source the
/// panel allocates nothing and draws its <see cref="Panel.Background"/> as a placeholder. Interop resources are
/// freed when the source changes/clears and when the panel leaves the visual tree — the control stays
/// non-disposable (its lifetime is the visual tree's, not the caller's).
/// </summary>
public class RenderTargetPanel : Grid
{
   private SharedSurfaceImage _image;
   // The previous source, kept alive for one extra frame after a swap (see RetireCurrentSource). Disposed by the
   // producer via DisposeRetiredSource once the GPU is idle and the compositor's submit referencing it is done.
   private SharedSurfaceImage _retiredImage;

   static RenderTargetPanel()
   {
      UseLayoutRoundingProperty.OverrideMetadata(typeof(RenderTargetPanel),
         new PropertyMetadata(true, PropertyMetadataOptions.AffectsMeasure));
      // Unlike a plain panel, this one IS an interactive surface: the hosted game is activated and fed input ONLY while
      // the panel holds focus (RenderTargetGameOutput keys IsActive/Got|LostFocus off it). So opt back IN to focus,
      // since Panel's default is now false. A click on the panel then focuses it and the game starts receiving input.
      FocusableProperty.OverrideMetadata(typeof(RenderTargetPanel), new PropertyMetadata(true));
   }

   /// <summary>The descriptor of the currently bound source, or <c>null</c> when nothing is bound.</summary>
   public SharedSurfaceDescriptor Source { get; private set; }

   /// <summary>
   /// Binds an externally produced surface and triggers a redraw. Any previously bound surface is released first.
   /// Pass <c>null</c> to unbind (equivalent to <see cref="ClearSource"/>).
   /// </summary>
   public void SetSource(SharedSurfaceDescriptor descriptor)
   {
      if (ReferenceEquals(Source, descriptor)) return;

      RetireCurrentSource();
      if (descriptor != null)
      {
         Source = descriptor;
         _image = new SharedSurfaceImage(descriptor);
      }
      InvalidateRender(false);
   }

   /// <summary>Unbinds the current source, if any (deferred release — see <see cref="RetireCurrentSource"/>).</summary>
   public void ClearSource()
   {
      if (Source == null) return;
      RetireCurrentSource();
      InvalidateRender(false);
   }

   /// <summary>
   /// Defers disposal of the imported surface by one frame instead of freeing it now. The compositor samples this
   /// surface and may have already queued its produce/consume semaphores into a submit that hasn't run yet
   /// (PreRender queues them in BeginDraw; the submit happens at the end of the draw phase). Destroying the import
   /// here would invalidate those handles (vkQueueSubmit Invalid VkSemaphore). The producer drains the retired image
   /// next frame via <see cref="DisposeRetiredSource"/>, after a wait-idle that proves the submit is complete.
   /// </summary>
   private void RetireCurrentSource()
   {
      _retiredImage?.Dispose(); // a prior retired image (not yet drained) is two frames old - safe to drop now
      _retiredImage = _image;
      _image = null;
      Source = null;
   }

   /// <summary>Disposes the surface retired by the previous source swap. The producer calls this one frame later,
   /// with the GPU idle, so the compositor's submit that referenced it has finished.</summary>
   public void DisposeRetiredSource()
   {
      _retiredImage?.Dispose();
      _retiredImage = null;
   }

   /// <summary>Frees both the current and the retired imported surface immediately. Detach only (the visual tree is
   /// being torn down, so nothing samples them anymore).</summary>
   private void ReleaseSource()
   {
      _image?.Dispose();
      _image = null;
      _retiredImage?.Dispose();
      _retiredImage = null;
      Source = null;
   }

   protected override void OnRender(IDrawingContext context)
   {
      var rect = new Rect(new Size(ActualWidth, ActualHeight));
      var session = context.ForControl(this);
      if (_image != null)
      {
         // TODO(Phase 4): wait(produce) before sampling and signal(consume) after, wired into the compositor's
         // queue submit (OnRender only records draw commands, so the semaphores can't be driven from here).
         session.DrawImage(_image, Background, rect, CornerRadius.Empty);
      }
      else
      {
         session.DrawRectangle(Background, rect, CornerRadius.Empty);
      }
   }

   protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
   {
      ReleaseSource();
      base.OnDetachedFromVisualTree(e);
   }
}
