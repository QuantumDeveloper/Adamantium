using System;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Core.Media;

/// <summary>A picture named by URI is decoded OFF the UI thread, so a brush handed one has nothing to sample on the
/// frame it was set - and, unlike the <see cref="Controls"/> Image control, a brush has nobody waiting for the load to
/// finish. Without this a textured fill draws nothing, for ever: the first frame finds no texture and no later frame is
/// ever asked for.</summary>
internal static class TexturedBrushSource
{
    /// <summary>Repaint whoever paints with this brush once <paramref name="source"/> has finished loading. Does nothing
    /// for a source that was ready all along (raw pixels, an already-loaded bitmap).</summary>
    public static void RepaintWhenLoaded(ImageSource source, Action repaint)
    {
        if (source is not BitmapImage bitmap) return;

        var load = bitmap.LoadTask;
        if (load == null || load.IsCompleted) return;

        load.ContinueWith(_ =>
        {
            // The continuation lands on a thread-pool thread; the repaint belongs to the thread that owns the brush.
            var dispatcher = UIAppContext.Current?.Dispatcher;
            if (dispatcher != null)
            {
                dispatcher.Post(repaint);
            }
            else
            {
                repaint();
            }
        });
    }
}
