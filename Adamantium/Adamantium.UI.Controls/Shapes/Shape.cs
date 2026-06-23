using System.Globalization;
using Adamantium.Core.Collections;
using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Shapes;

public abstract class Shape : InputUIComponent
{
   protected Rect Rect;

   // The actual painted rectangle of the stroke in the control's local space (the geometry's authored coordinates grown
   // by the pen, including miter spikes). Drives the framework adorner/selection frame so it hugs the real stroked
   // bounds, not the bare geometry box. Distinct from the layout Bounds, which spans from the origin (WPF Stretch=None).
   public Rect RenderBounds { get; protected set; }

   protected Shape()
   {
   }

   // Measures a shape defined by intrinsic geometry (Line/Polygon/Path). The painted bounds are the GPU stroke's exact
   // extent (TryComputeStrokeBounds mirrors StrokeEffect.fx, including the clamped miter spikes at sharp corners such as
   // star tips). The geometry keeps its authored coordinates (WPF Stretch=None); the element grows to include the stroke
   // so it no longer pokes past the control. With no visible stroke the bounds collapse to the bare geometry.
   protected Size MeasureStrokedGeometry(Rect geometryBounds, IReadOnlyList<(Vector2[] Points, bool IsClosed)> contours = null)
   {
      var thickness = IsVisibleBrush(Stroke) ? StrokeThickness : 0.0;
      var half = thickness / 2.0;

      var min = new Vector2(double.MaxValue);
      var max = new Vector2(double.MinValue);

      // Floor: the geometry bbox grown by half the stroke. Always valid (a non-empty geometry never collapses to 0) and
      // exact for round/bevel joins and smooth outlines. Combined/group geometries whose contours aren't exposed at
      // measure time rely on this floor instead of collapsing.
      if (geometryBounds.Width > 0 || geometryBounds.Height > 0)
      {
         min = new Vector2(geometryBounds.X - half, geometryBounds.Y - half);
         max = new Vector2(geometryBounds.X + geometryBounds.Width + half, geometryBounds.Y + geometryBounds.Height + half);
      }

      // Refinement: union the exact stroke extent, which reaches past the bbox floor at sharp MITER corners (star tips).
      if (TryComputeStrokeBounds(contours, thickness, StrokeLineJoin, StartLineCap, EndLineCap, out var smin, out var smax))
      {
         min = Vector2.Min(min, smin);
         max = Vector2.Max(max, smax);
      }

      if (max.X < min.X || max.Y < min.Y)
      {
         RenderBounds = default;
         Rect = default;
         return new Size(0, 0);
      }

      // WPF Stretch=None: the geometry keeps its authored coordinates (a Line's X1/Y1, a Path's points), so the element
      // spans from its origin to the painted stroke's bottom-right - the size includes BOTH the geometry's own offset
      // and the stroke overhang (which fixes the stroke poking past the control without moving the shape). RenderBounds
      // is the stroke's actual bbox in that same local space, used by the adorner to frame it tightly.
      RenderBounds = new Rect(min, max);
      var size = new Size(Math.Max(max.X, 0), Math.Max(max.Y, 0));
      Rect = new Rect(size);
      return size;
   }

   // Extracts the outline contours (raw polyline points + closed flag) from a processed geometry mesh, mirroring the
   // stroke render path (RenderUnit.TryGetSolidContours) so the measured bounds match what is actually stroked.
   protected static List<(Vector2[] Points, bool IsClosed)> GetContours(Mesh mesh)
   {
      List<(Vector2[], bool)> list = [];
      if (mesh?.Contours != null)
      {
         foreach (var contour in mesh.Contours)
            if (contour.Points is { Length: >= 2 })
               list.Add((contour.Points, contour.IsGeometryClosed));
      }
      return list;
   }

   // CPU mirror of StrokeEffect.fx (OffsetPair + join/cap extents): the exact bounding box of the GPU stroke, so
   // RenderBounds/size hug it - including the clamped miter spike (capped at 4*half via the 0.25 clamp) that a flat
   // half-thickness inflate misses at sharp corners. A closed contour has no caps (every vertex is a join); an open one
   // caps its two ends. MUST stay in sync with StrokeEffect.fx / GpuStrokeRenderComponent.
   protected static bool TryComputeStrokeBounds(
      IReadOnlyList<(Vector2[] Points, bool IsClosed)> contours,
      double thickness, PenLineJoin join, PenLineCap startCap, PenLineCap endCap,
      out Vector2 min, out Vector2 max)
   {
      min = new Vector2(double.MaxValue);
      max = new Vector2(double.MinValue);
      var any = false;
      var half = thickness / 2.0;

      foreach (var (points, isClosed) in contours ?? [])
      {
         var n = points?.Length ?? 0;
         if (n < 2) continue;

         for (var i = 0; i < n; i++)
         {
            var p = points[i];
            var isStart = !isClosed && i == 0;
            var isEnd = !isClosed && i == n - 1;

            if (isStart || isEnd)
            {
               var dir = SafeNormalize(isStart ? points[1] - points[0] : points[n - 1] - points[n - 2]);
               var cap = isStart ? startCap : endCap;
               var p0 = isStart ? p - dir * (CapShift(cap) * half) : p + dir * (CapShift(cap) * half);
               var perp = new Vector2(-dir.Y, dir.X);
               Include(ref min, ref max, ref any, p0 + perp * half);
               Include(ref min, ref max, ref any, p0 - perp * half);
               if (cap != PenLineCap.Flat)   // round/triangle cap fans reach up to half beyond the endpoint
               {
                  Include(ref min, ref max, ref any, p + new Vector2(half, half));
                  Include(ref min, ref max, ref any, p - new Vector2(half, half));
               }
            }
            else
            {
               var d0 = SafeNormalize(p - points[(i - 1 + n) % n]);
               var d1 = SafeNormalize(points[(i + 1) % n] - p);
               if ((d0.X == 0 && d0.Y == 0) || (d1.X == 0 && d1.Y == 0))   // degenerate segment: basic half disc
               {
                  Include(ref min, ref max, ref any, p + new Vector2(half, half));
                  Include(ref min, ref max, ref any, p - new Vector2(half, half));
                  continue;
               }

               var n0 = new Vector2(-d0.Y, d0.X);
               var n1 = new Vector2(-d1.Y, d1.X);
               if (join == PenLineJoin.Miter)
               {
                  var miter = SafeNormalize(n0 + n1);
                  var denom = Math.Max(Vector2.Dot(miter, n0), 0.25);   // matches the shader: spike capped at 4*half
                  var len = half / denom;
                  Include(ref min, ref max, ref any, p + miter * len);
                  Include(ref min, ref max, ref any, p - miter * len);
               }
               else
               {
                  Include(ref min, ref max, ref any, p + n0 * half);
                  Include(ref min, ref max, ref any, p - n0 * half);
                  Include(ref min, ref max, ref any, p + n1 * half);
                  Include(ref min, ref max, ref any, p - n1 * half);
                  if (join == PenLineJoin.Round)
                  {
                     Include(ref min, ref max, ref any, p + new Vector2(half, half));
                     Include(ref min, ref max, ref any, p - new Vector2(half, half));
                  }
               }
            }
         }
      }

      return any;
   }

   private static void Include(ref Vector2 min, ref Vector2 max, ref bool any, Vector2 p)
   {
      min = Vector2.Min(min, p);
      max = Vector2.Max(max, p);
      any = true;
   }

   private static Vector2 SafeNormalize(Vector2 v)
   {
      var len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
      return len < 1e-9 ? default : new Vector2(v.X / len, v.Y / len);
   }

   // Cap shift along the segment direction in units of half-thickness (matches StrokeEffect.fx CapShift): square
   // extends out by half, concave insets by half, others none.
   private static double CapShift(PenLineCap cap) =>
      cap == PenLineCap.Square ? 1.0 : cap is PenLineCap.ConcaveTriangle or PenLineCap.ConcaveRound ? -1.0 : 0.0;

   public static readonly AdamantiumProperty FillProperty = AdamantiumProperty.Register(nameof(Fill),
      typeof (Brush), typeof (Shape),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.BindsTwoWayByDefault|PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty StrokeProperty = AdamantiumProperty.Register(nameof(Stroke),
      typeof (Brush), typeof (Shape),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.BindsTwoWayByDefault));

   public static readonly AdamantiumProperty StrokeThicknessProperty =
      AdamantiumProperty.Register(nameof(StrokeThickness),
         typeof (Double), typeof (Shape),
         new PropertyMetadata((Double) 0,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender, CoerceStrokeThickness ));

   public static readonly AdamantiumProperty StrokeDashArrayProperty =
      AdamantiumProperty.Register(nameof(StrokeDashArray),
         typeof (TrackingCollection<Double>), typeof(Shape), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty StretchProperty = AdamantiumProperty.Register(nameof(Stretch),
      typeof (Stretch), typeof (Shape),
      new PropertyMetadata(Stretch.None,
         PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty StartLineCapProperty =
      AdamantiumProperty.Register(nameof(StartLineCap), typeof(PenLineCap), typeof(Shape),
         new PropertyMetadata(PenLineCap.Flat, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty EndLineCapProperty =
      AdamantiumProperty.Register(nameof(EndLineCap), typeof(PenLineCap), typeof(Shape),
         new PropertyMetadata(PenLineCap.Flat, PropertyMetadataOptions.AffectsRender));
      
   public static readonly AdamantiumProperty StrokeLineJoinProperty =
      AdamantiumProperty.Register(nameof(StrokeLineJoin), typeof(PenLineJoin), typeof(Shape),
         new PropertyMetadata(PenLineJoin.Miter, PropertyMetadataOptions.AffectsRender));
      
   public static readonly AdamantiumProperty StrokeDashOffsetProperty =
      AdamantiumProperty.Register(nameof(StrokeDashOffset), typeof(Double), typeof(Shape),
         new PropertyMetadata(0d, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty StrokeTrimStartProperty =
      AdamantiumProperty.Register(nameof(StrokeTrimStart), typeof(Double), typeof(Shape),
         new PropertyMetadata(0d, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty StrokeTrimEndProperty =
      AdamantiumProperty.Register(nameof(StrokeTrimEnd), typeof(Double), typeof(Shape),
         new PropertyMetadata(1d, PropertyMetadataOptions.AffectsRender));

   // Symbolic dash pattern: a named sequence (StrokeDashSymbols, e.g. "Dash Dot Dot Dash") whose glyph widths + gap
   // come from StrokeDashGlyphs (e.g. "Dash=18, Dot=3, Gap=6", in pixels). When set it expands to the regular
   // StrokeDashArray, so you compose the pattern by name (Morse-codeable) instead of raw numbers.
   public static readonly AdamantiumProperty StrokeDashSymbolsProperty =
      AdamantiumProperty.Register(nameof(StrokeDashSymbols), typeof(String), typeof(Shape),
         new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty StrokeDashGlyphsProperty =
      AdamantiumProperty.Register(nameof(StrokeDashGlyphs), typeof(String), typeof(Shape),
         new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

   // Cap on each dash/dot piece's ends (the contour's real ends use Start/EndLineCap). Round -> round dots.
   public static readonly AdamantiumProperty StrokeDashCapProperty =
      AdamantiumProperty.Register(nameof(StrokeDashCap), typeof(PenLineCap), typeof(Shape),
         new PropertyMetadata(PenLineCap.ConvexRound, PropertyMetadataOptions.AffectsRender));

   public Brush Fill
   {
      get => GetValue<Brush>(FillProperty);
      set => SetValue(FillProperty, value);
   }

   public Brush Stroke
   {
      get => GetValue<Brush>(StrokeProperty);
      set => SetValue(StrokeProperty, value);
   }

   public Double StrokeThickness
   {
      get => GetValue<Double>(StrokeThicknessProperty);
      set => SetValue(StrokeThicknessProperty, value);
   }

   public TrackingCollection<Double> StrokeDashArray
   {
      get => GetValue<TrackingCollection<Double>>(StrokeDashArrayProperty);
      set => SetValue(StrokeDashArrayProperty, value);
   }

   public Stretch Stretch
   {
      get => GetValue<Stretch>(StretchProperty);
      set => SetValue(StretchProperty, value);
   }

   public PenLineCap StartLineCap
   {
      get => GetValue<PenLineCap>(StartLineCapProperty);
      set => SetValue(StartLineCapProperty, value);
   }

   public PenLineCap EndLineCap
   {
      get => GetValue<PenLineCap>(EndLineCapProperty);
      set => SetValue(EndLineCapProperty, value);
   }

   public PenLineJoin StrokeLineJoin
   {
      get => GetValue<PenLineJoin>(StrokeLineJoinProperty);
      set => SetValue(StrokeLineJoinProperty, value);
   }

   public Double StrokeDashOffset
   {
      get => GetValue<Double>(StrokeDashOffsetProperty);
      set => SetValue(StrokeDashOffsetProperty, value);
   }

   public Double StrokeTrimStart
   {
      get => GetValue<Double>(StrokeTrimStartProperty);
      set => SetValue(StrokeTrimStartProperty, value);
   }

   public Double StrokeTrimEnd
   {
      get => GetValue<Double>(StrokeTrimEndProperty);
      set => SetValue(StrokeTrimEndProperty, value);
   }

   public String StrokeDashSymbols
   {
      get => GetValue<String>(StrokeDashSymbolsProperty);
      set => SetValue(StrokeDashSymbolsProperty, value);
   }

   public String StrokeDashGlyphs
   {
      get => GetValue<String>(StrokeDashGlyphsProperty);
      set => SetValue(StrokeDashGlyphsProperty, value);
   }

   public PenLineCap StrokeDashCap
   {
      get => GetValue<PenLineCap>(StrokeDashCapProperty);
      set => SetValue(StrokeDashCapProperty, value);
   }
      
   private static object CoerceStrokeThickness(AdamantiumComponent adamantiumAdamantiumComponent, object baseValue)
   {
      Double value = (Double) baseValue;
      if (value < 0)
      {
         return (Double) 0;
      }
      return baseValue;
   }

   public Pen GetPen()
   {
      // A symbolic pattern (StrokeDashSymbols) expands into the dash array; otherwise the raw StrokeDashArray is used.
      return new Pen(
         Stroke,
         StrokeThickness,
         StrokeDashOffset,
         ExpandDashSymbols() ?? (IEnumerable<double>)StrokeDashArray,
         StartLineCap,
         EndLineCap,
         StrokeLineJoin,
         StrokeTrimStart,
         StrokeTrimEnd,
         StrokeDashCap);
   }

   // Expands StrokeDashSymbols ("Dash Dot Dot Dash") into a dash array using the named widths + gap from
   // StrokeDashGlyphs ("Dash=18, Dot=3, Gap=6", in pixels). Each symbol contributes [width, gap]; an unknown or
   // zero-width symbol becomes a hair-width "dot" that the round dash cap renders as a circle. Returns null when no
   // symbolic pattern is set, so GetPen falls back to the raw StrokeDashArray.
   private List<double> ExpandDashSymbols()
   {
      var symbols = StrokeDashSymbols;
      if (string.IsNullOrWhiteSpace(symbols)) return null;

      var glyphs = ParseDashGlyphs(StrokeDashGlyphs);
      var gap = glyphs.TryGetValue("Gap", out var g) ? g : Math.Max(StrokeThickness, 1.0);

      var result = new List<double>();
      foreach (var token in symbols.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
      {
         var width = glyphs.TryGetValue(token, out var w) ? w : 0.0;
         result.Add(Math.Max(width, 0.01));   // zero width -> epsilon; the round dash cap draws it as a dot
         result.Add(gap);
      }
      return result.Count > 0 ? result : null;
   }

   // Parses "Name=px, Name=px, Gap=px" into a name->pixels map (case-insensitive, invariant numbers).
   private static Dictionary<string, double> ParseDashGlyphs(string spec)
   {
      var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
      if (string.IsNullOrWhiteSpace(spec)) return map;
      foreach (var pair in spec.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries))
      {
         var kv = pair.Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
         if (kv.Length == 2 && double.TryParse(kv[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            map[kv[0]] = v;
      }
      return map;
   }

   // --- Tight hit-testing helpers (narrow phase) -----------------------------------------------------------------
   // Shapes override IUIComponent.HitTestCore with their real geometry (see Line/Ellipse/Path) so a click inside a
   // shape's bounding box but off the shape itself doesn't select it. These helpers are the shared 2D math.

   /// <summary>Shortest distance from <paramref name="p"/> to the segment a-b (both in the shape's local space).</summary>
   protected static double DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
   {
      var abx = b.X - a.X;
      var aby = b.Y - a.Y;
      var lenSq = abx * abx + aby * aby;
      double t = lenSq < 1e-9 ? 0.0 : Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / lenSq, 0.0, 1.0);
      var dx = p.X - (a.X + t * abx);
      var dy = p.Y - (a.Y + t * aby);
      return Math.Sqrt(dx * dx + dy * dy);
   }

   /// <summary>A brush that paints something (non-null, not the transparent brush, non-zero alpha) - so an unfilled
   /// shape doesn't hit-test on its empty interior, only on its stroke.</summary>
   protected static bool IsVisibleBrush(Brush brush) =>
      brush != null && brush != Brushes.Transparent && !(brush is SolidColorBrush { Color.A: 0 });

   protected override Size MeasureOverride(Size availableSize)
   {
      Size shapeSize = Rect.Size;

      // The shape's natural (intrinsic) size, captured before Rect is recomputed below. This is what the shape reports
      // to layout: a Stretch-aligned shape with no explicit size desires its intrinsic size (0 for a box shape like
      // Rectangle/Ellipse), NOT the available space - stretching fills the box via Rect (render), at arrange/render time,
      // not by inflating the desired size. (WPF behaviour; previously a Stretch shape desired the whole available area.)
      double naturalWidth = Rect.Width + Rect.X;
      double naturalHeight = Rect.Height + Rect.Y;

      Size desiredSize = new Size(availableSize.Width, availableSize.Height).Deflate(new Thickness(StrokeThickness/2));

      if (double.IsInfinity(availableSize.Width))
      {
         desiredSize.Width = shapeSize.Width;
      }

      if (double.IsInfinity(availableSize.Height))
      {
         desiredSize.Height = shapeSize.Height;
      }
         
      if (double.IsNaN(Width) && HorizontalAlignment != HorizontalAlignment.Stretch)
      {
         desiredSize.Width = shapeSize.Width + Rect.X;
      }

      if (double.IsNaN(Height) && VerticalAlignment != VerticalAlignment.Stretch)
      {
         desiredSize.Height = shapeSize.Height + Rect.Y;
      }

      if (!double.IsNaN(Width))
      {
         desiredSize.Width = Width;
      }

      if (!double.IsNaN(Height))
      {
         desiredSize.Height = Height;
      }

      switch (Stretch)
      {
         case Stretch.Uniform:
            shapeSize.Width = Math.Min(desiredSize.Width, desiredSize.Height);
            shapeSize.Height = Math.Min(desiredSize.Width, desiredSize.Height);
            break;
         case Stretch.UniformToFill:
            double maxValue = Math.Max(desiredSize.Width, desiredSize.Height);
            shapeSize.Width = maxValue;
            shapeSize.Height = maxValue;
            break;
         case Stretch.Fill:
            shapeSize.Width = Math.Max(desiredSize.Width, desiredSize.Height);
            shapeSize.Height = Math.Max(desiredSize.Width, desiredSize.Height);
            break;
         default:
            shapeSize = desiredSize;
            break;
      }
      Rect = new Rect(shapeSize);

      // Box shapes (Ellipse/Rectangle) inset their geometry by half the stroke via Deflate, so the painted bounds are
      // the box itself; geometry shapes use MeasureStrokedGeometry instead and never reach here.
      RenderBounds = new Rect(shapeSize);

      // Report the intrinsic size to layout (explicit Width/Height win); Rect above still fills for rendering.
      return new Size(
         double.IsNaN(Width) ? naturalWidth : Width,
         double.IsNaN(Height) ? naturalHeight : Height);
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      return Rect.Size;
   }
}