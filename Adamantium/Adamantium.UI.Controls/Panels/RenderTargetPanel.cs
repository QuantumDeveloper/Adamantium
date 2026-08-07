using Adamantium.Graphics.Core;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
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

   public static readonly AdamantiumProperty MouseLookModeProperty = AdamantiumProperty.Register(nameof(MouseLookMode),
      typeof(MouseLookMode), typeof(RenderTargetPanel), new PropertyMetadata(MouseLookMode.None));

   /// <summary>How the panel feeds the hosted game a relative mouse delta for mouse-look (see <see cref="MouseLookMode"/>).
   /// Default <see cref="MouseLookMode.None"/> - the cursor stays a normal pointer.</summary>
   public MouseLookMode MouseLookMode
   {
      get => GetValue<MouseLookMode>(MouseLookModeProperty);
      set => SetValue(MouseLookModeProperty, value);
   }

   public static readonly AdamantiumProperty IsMouseLookEnabledProperty = AdamantiumProperty.Register(
      nameof(IsMouseLookEnabled), typeof(bool), typeof(RenderTargetPanel),
      new PropertyMetadata(true, OnIsMouseLookEnabledChanged));

   /// <summary>Gates mouse-look ON/OFF without changing the <see cref="MouseLookMode"/>. Bind it to app state (e.g. an
   /// open in-game menu) so a click on the panel does NOT hide the cursor while the menu is up; setting it false while
   /// looking immediately restores the cursor. Default <c>true</c>.</summary>
   public bool IsMouseLookEnabled
   {
      get => GetValue<bool>(IsMouseLookEnabledProperty);
      set => SetValue(IsMouseLookEnabledProperty, value);
   }

   private static void OnIsMouseLookEnabledChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      var panel = (RenderTargetPanel)a;
      if (!panel.IsMouseLookEnabled) panel.Disengage();   // disabled mid-look (a menu opened) -> give the cursor back
   }

   private bool _looking;    // relative mode currently engaged by THIS panel
   private bool _leftDown;   // Drag mode: left button held on the panel
   private bool _rightDown;  // Drag mode: right button held on the panel
   private Vector2 _engagePoint;   // panel-relative cursor position at engage; restored (as screen) on release

   // Esc is the always-available escape hatch out of mouse-look (frees the hidden cursor), the safety net for
   // Continuous mode and any missed button-up.
   protected override void OnKeyDown(KeyEventArgs e)
   {
      base.OnKeyDown(e);
      if (e.Key == Key.Escape) Disengage();
   }

   /// <summary>
   /// Binds an externally produced surface and triggers a redraw. Any previously bound surface is released first.
   /// Pass <c>null</c> to unbind (equivalent to <see cref="ClearSource"/>).
   /// </summary>
   public void SetSource(SharedSurfaceDescriptor descriptor)
   {
      if (ReferenceEquals(Source, descriptor)) return;

      DropCurrentSource();
      if (descriptor != null)
      {
         Source = descriptor;
         _image = new SharedSurfaceImage(descriptor);
      }
      InvalidateAfterSourceChange();
   }

   /// <summary>Unbinds the current source, if any (see <see cref="DropCurrentSource"/>).</summary>
   public void ClearSource()
   {
      if (Source == null) return;
      DropCurrentSource();
      InvalidateAfterSourceChange();
   }

   /// <summary>
   /// Marks the panel for re-record after a source swap, DEFERRED to the START of the next loop frame. The producer (the
   /// game loop) binds the surface during the loop's DRAW phase - AFTER this frame's UI record - so an INLINE
   /// <see cref="UIComponent.InvalidateRender"/> marks geometry too late: the frame's <c>RenderDirty.Clear</c> wipes the
   /// mark before any record snapshots it, and the surface is bound ONCE (no per-frame retry), so the panel would stay on
   /// its placeholder and never draw the game. Neither dispatcher facade fits: <c>Dispatcher.Post</c> runs INLINE when
   /// already on the loop thread (same bad timing), and <c>Dispatcher.Invoke</c> runs on the message-pump thread, which
   /// races the loop's record→clear and loses the mark. <see cref="LoopSignal.Post"/> ALWAYS queues onto the loop thread's
   /// pre-record drain (<c>DrainPending</c>, start of Update), so the mark is set before that frame's record and cannot be
   /// cleared first.
   /// </summary>
   private void InvalidateAfterSourceChange()
   {
      if (UIAppContext.Current != null) LoopSignal.Post(() => InvalidateRender(false));
      else InvalidateRender(false);
   }

   /// <summary>
   /// Drops the current source WITHOUT freeing its imported surface. The GPU import's lifetime belongs to the
   /// ImageRenderComponent that samples it: it frees the import through the render device's fence-gated deferred-dispose
   /// once the compositor switches off it (the producer resize hands over a NEW surface, the pipeline rebuilds the
   /// panel's op onto it and releases the old component then). Freeing it here would race the render thread, which may
   /// still be replaying an op that samples the old import - the resize-crash (vkQueueSubmit against a freed surface).
   /// </summary>
   private void DropCurrentSource()
   {
      _image = null;
      Source = null;
   }

   /// <summary>Drops the source on detach. The imported surface is freed by its render component when the pipeline
   /// removes this panel's units (ReconcileDetachedControls, on the render thread) - never disposed here, off the
   /// render thread, where it could race a compositor submit still in flight.</summary>
   private void ReleaseSource()
   {
      _image = null;
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
      Disengage();   // a tab switch / tree teardown while looking must restore the cursor
      ReleaseSource();
      base.OnDetachedFromVisualTree(e);
   }

   // --- Mouse-look: engage relative mouse mode per MouseLookMode ---------------------------------------------------
   // Engage = focus + capture the mouse + ask the window (its platform worker) to hide/centre the cursor and start
   // feeding synthesized RawMouseMove deltas. Disengage restores everything. Drag mode brackets it with the button
   // hold; Continuous mode brackets it with focus (click to engage, blur to release).

   private void Engage()
   {
      if (_looking || !IsMouseLookEnabled) return;
      _looking = true;
      // Remember where the cursor sat ON THIS PANEL (panel-relative), so it reappears there on release instead of at the
      // re-centre point. The panel owns this position; the worker just warps to the screen point we hand back. Computed as
      // the EXACT inverse of PointToScreen (root-client of the screen cursor, minus the accumulated ClipRectangle offsets)
      // so PointToScreen(_engagePoint) round-trips back to the same screen pixel - MouseDevice.GetPosition uses a different
      // base (Bounds), so its error would accumulate a drift across engage/release cycles.
      _engagePoint = ScreenToPanel(MouseDevice.CurrentDevice.GetScreenPosition());
      Focus();
      CaptureMouse();
      (RootVisual as WindowBase)?.SetRelativeMouseMode(true, default);
   }

   private void Disengage()
   {
      if (!_looking) return;
      _looking = false;
      (RootVisual as WindowBase)?.SetRelativeMouseMode(false, this.PointToScreen(_engagePoint));
      ReleaseMouseCapture();
   }

   // Exact inverse of this.PointToScreen (screen -> panel-relative): the screen point in root-client coords minus the
   // ClipRectangle offsets PointToScreen accumulates, so the two compose to identity (no per-cycle drift).
   private Vector2 ScreenToPanel(PixelPoint screen)
   {
      var rootLocal = RootVisual.PointToClient(screen);
      var offset = default(Mathematics.Vector2);
      for (IUIComponent c = this; c != null && c is not IRootVisualComponent; c = c.VisualParent)
         offset += c.ClipRectangle.Location;
      return rootLocal - offset;
   }

   protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
   {
      base.OnMouseLeftButtonDown(sender, e);
      _leftDown = true;
      if (MouseLookMode == MouseLookMode.Drag) Engage();
      else if (MouseLookMode == MouseLookMode.Continuous) Focus();
   }

   protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
   {
      base.OnMouseLeftButtonUp(sender, e);
      _leftDown = false;
      if (MouseLookMode == MouseLookMode.Drag && !_rightDown) Disengage();
   }

   protected override void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
   {
      base.OnMouseRightButtonDown(sender, e);
      _rightDown = true;
      if (MouseLookMode == MouseLookMode.Drag) Engage();
      else if (MouseLookMode == MouseLookMode.Continuous) Focus();
   }

   protected override void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
   {
      base.OnMouseRightButtonUp(sender, e);
      _rightDown = false;
      if (MouseLookMode == MouseLookMode.Drag && !_leftDown) Disengage();
   }

   protected override void OnGotFocus(RoutedEventArgs e)
   {
      base.OnGotFocus(e);
      if (MouseLookMode == MouseLookMode.Continuous) Engage();
   }

   protected override void OnLostFocus(RoutedEventArgs e)
   {
      base.OnLostFocus(e);
      if (MouseLookMode == MouseLookMode.Continuous) Disengage();
   }
}
