using Adamantium.Core.Collections;

namespace Adamantium.UI.Core.Media;

public class Pen : IEquatable<Pen>
{
   // A Pen is immutable-per-change (every field set once; a NEW Pen is built on any change) EXCEPT its brush, which is a
   // live, animatable object. So the pen keeps the LIVE brush and exposes its immutable SNAPSHOT - the same contract a
   // payload has, and for the same reason: an animated stroke brush must stay visible through a pen recorded long ago.
   private readonly Brush _brush;

   /// <summary>The immutable snapshot of the stroke brush's current appearance (see <see cref="Media.Brush.Snapshot"/>).</summary>
   public Brush Brush => _brush?.Snapshot;

   public Double Thickness { get; }

   public Double DashOffset { get; }
      
   public AdamantiumCollection<Double> DashStrokeArray { get; }

   public PenLineCap StartLineCap { get; }

   public PenLineCap EndLineCap { get; }

   // Cap on the STARTING end of each DASH/dot piece (the contour's true ends use Start/EndLineCap). Round makes round
   // dots; a zero-width dot + a round dash cap renders as a circle.
   public PenLineCap DashStartCap { get; }

   // Cap on the ENDING end of each dash. Separate from the start on purpose: with the two set differently a dash is no
   // longer symmetric - a concave bite behind a convex tip is an arrowhead, and a run of them is a direction.
   public PenLineCap DashEndCap { get; }

   public PenLineJoin PenLineJoin { get; }

   // Stroke trim: render only the arc-length fraction [TrimStart, TrimEnd] of the geometry (0..1) - e.g. a progress
   // bar / loading ring. Default 0..1 = the whole stroke. Applied on the GPU.
   public Double TrimStart { get; }

   public Double TrimEnd { get; }

   /// <summary>Whether the dash pattern is stretched so a WHOLE number of periods fits a closed contour. The array is
   /// given in pixels, and a closed contour's length almost never divides by the pattern - the remainder is left over
   /// where the contour closes and shows up as one long dash. Fitting scales the pattern by at most half a period so
   /// the ring closes on itself at ANY size, which is what a marching-ants ring needs and what a ruler-like dashed
   /// border must NOT have (its dashes are supposed to measure a fixed length).</summary>
   public Boolean FitDashesToContour { get; }

   /// <summary>Dash offset expressed in PERIODS instead of pixels, added to <see cref="DashOffset"/>. One period is the
   /// whole pattern, so animating this 0 -> 1 marches the dashes exactly one step and lands back on itself - seamlessly,
   /// whatever the array says and whatever the contour fit scaled it to. The pixel offset cannot do that: it makes the
   /// author work out the period by hand and then re-derive it every time the pattern changes.</summary>
   public Double DashPhase { get; }

   public Pen(
      Brush brush,
      Double thickness = 1.0,
      Double dashOffset = 0,
      IEnumerable<Double> dashStrokeArray = null,
      PenLineCap startLineCap = PenLineCap.Flat,
      PenLineCap endLineCap = PenLineCap.Flat,
      PenLineJoin penLineJoin = PenLineJoin.Miter,
      Double trimStart = 0.0,
      Double trimEnd = 1.0,
      PenLineCap dashStartCap = PenLineCap.ConvexRound,
      PenLineCap dashEndCap = PenLineCap.ConvexRound,
      Boolean fitDashesToContour = false,
      Double dashPhase = 0)
   {
      FitDashesToContour = fitDashesToContour;
      DashPhase = dashPhase;
      _brush = brush?.ForRendering();
      DashStrokeArray = new AdamantiumCollection<double>(dashStrokeArray);
      DashOffset = dashOffset;
      Thickness = thickness;
      StartLineCap = startLineCap;
      EndLineCap = endLineCap;
      PenLineJoin = penLineJoin;
      TrimStart = trimStart;
      TrimEnd = trimEnd;
      DashStartCap = dashStartCap;
      DashEndCap = dashEndCap;
   }

   /// <summary>Compare dash patterns BY VALUE. Deliberately here and not on <c>AdamantiumCollection</c>: that type is a
   /// general, MUTABLE list used across the engine, so giving it value equality would turn O(1) reference compares into
   /// O(n) ones for every other user, and a value hash on a mutable object silently loses it as a dictionary key. A dash
   /// pattern is 0-4 numbers, so comparing it costs nothing - and the count check exits first in the common empty case.</summary>
   private static bool SameDashes(AdamantiumCollection<Double> a, AdamantiumCollection<Double> b)
   {
      if (ReferenceEquals(a, b)) return true;
      if (a == null || b == null || a.Count != b.Count) return false;
      for (var i = 0; i < a.Count; i++)
         if (!a[i].Equals(b[i]))
            return false;
      return true;
   }

   /// <summary>A copy for the RENDER path: same values, same LIVE brush (so an animated stroke brush keeps animating
   /// through its snapshot), but a dash array nobody else holds. A payload storing this instead of the caller's pen no
   /// longer exposes the render thread to a pen the loop thread can still edit. Safe only because the comparison above is
   /// by value - a clone with a fresh collection would otherwise read as "changed" on every single frame.</summary>
   public Pen CloneForRendering() => new(
      _brush, 
      Thickness, 
      DashOffset, 
      DashStrokeArray, 
      StartLineCap, 
      EndLineCap, 
      PenLineJoin,
      TrimStart, 
      TrimEnd, 
      DashStartCap, 
      DashEndCap, 
      FitDashesToContour, 
      DashPhase);

   public bool Equals(Pen other)
   {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return Equals(Brush, other.Brush) && Thickness.Equals(other.Thickness) && DashOffset.Equals(other.DashOffset) &&
             SameDashes(DashStrokeArray, other.DashStrokeArray) && StartLineCap == other.StartLineCap &&
             EndLineCap == other.EndLineCap && DashStartCap == other.DashStartCap &&
             DashEndCap == other.DashEndCap && PenLineJoin == other.PenLineJoin &&
             TrimStart.Equals(other.TrimStart) && TrimEnd.Equals(other.TrimEnd) &&
             // Load-bearing: a pen that compares EQUAL is treated as unchanged and the stroke is never re-pointed, so a
             // property missing from here is a property that silently cannot be animated (the marching ants stopped
             // moving the moment DashPhase was added and left out).
             DashPhase.Equals(other.DashPhase) && FitDashesToContour == other.FitDashesToContour;
   }

   public override bool Equals(object obj)
   {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      if (obj.GetType() != this.GetType()) return false;
      return Equals((Pen)obj);
   }

   public override int GetHashCode()
   {
      return HashCode.Combine(
         // The dash array hashes by its COUNT, not its identity: Equals now compares the pattern by value, so two equal
         // pens with different collection instances must not land in different buckets.
         HashCode.Combine(Brush, Thickness, DashOffset, DashStrokeArray?.Count ?? 0, (int)StartLineCap, (int)EndLineCap, (int)PenLineJoin),
         HashCode.Combine(TrimStart, TrimEnd, (int)DashStartCap, (int)DashEndCap, DashPhase, FitDashesToContour));
   }
}