using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Adamantium.Mathematics;
using Adamantium.Mathematics.Triangulation;
using NUnit.Framework;

namespace Adamantium.MathTests
{
    /// <summary>
    /// Re-runnable triangulator benchmark (Phase 0.5). Category "Benchmark" so it's excluded from normal runs.
    /// Run: dotnet test -c Release --filter "TestCategory=Benchmark"
    /// </summary>
    [Category("Benchmark")]
    public class TriangulatorBenchmark
    {
        [Test]
        public void Bench()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Build: {(IsDebug() ? "DEBUG (ignore!)" : "RELEASE")}");
            sb.AppendLine();

            sb.AppendLine("== Circle (convex, single contour) ==");
            double prev = 0;
            foreach (var n in new[] { 64, 128, 256, 512, 1024, 2048 })
            {
                var pts = Circle(n);
                var (ms, min, tris) = Measure(() => Single(pts), 2, n <= 512 ? 15 : 5);
                var ratio = prev > 0 ? $"  x{ms / prev:F2}" : "";
                sb.AppendLine($"  n={n,5}  tris={tris,6}  median={ms,9:F3} ms  min={min,9:F3} ms{ratio}");
                prev = ms;
            }

            sb.AppendLine();
            sb.AppendLine("== Gear (concave, non-self-int, single contour) ==");
            foreach (var n in new[] { 128, 256, 512, 1024 })
            {
                var pts = Gear(n);
                var (ms, min, tris) = Measure(() => Single(pts), 2, n <= 512 ? 10 : 5);
                sb.AppendLine($"  n={n,5}  tris={tris,6}  median={ms,9:F3} ms  min={min,9:F3} ms");
            }

            sb.AppendLine();
            sb.AppendLine("== Annulus (outer ring + inner hole, multi-contour merge -> earcut-with-holes) ==");
            foreach (var n in new[] { 64, 256, 1024 })
            {
                var (ms, min, tris) = Measure(() => Annulus(n), 2, n <= 256 ? 10 : 5);
                sb.AppendLine($"  n/ring={n,5}  tris={tris,6}  median={ms,9:F3} ms  min={min,9:F3} ms");
            }

            sb.AppendLine();
            sb.AppendLine("== Many separate squares (containers / merge path) ==");
            foreach (var k in new[] { 4, 16, 64, 256 })
            {
                var (ms, min, tris) = Measure(() => Squares(k), 2, 5);
                sb.AppendLine($"  squares={k,4}  tris={tris,6}  median={ms,9:F3} ms  min={min,9:F3} ms");
            }

            var text = sb.ToString();
            try { System.IO.File.WriteAllText(@"C:\Temp\tri_bench.txt", text); } catch { }
            TestContext.Progress.WriteLine(text);
            Assert.Pass("\n" + text);
        }

        static Vector2[] Circle(int n, double r = 100)
        {
            var p = new Vector2[n];
            for (int i = 0; i < n; i++) { double a = 2 * Math.PI * i / n; p[i] = new Vector2(r * Math.Cos(a), r * Math.Sin(a)); }
            return p;
        }

        static Polygon Annulus(int n)
        {
            var p = new Polygon { FillRule = FillRule.EvenOdd };
            p.AddContour(new MeshContour(Circle(n, 100)));
            p.AddContour(new MeshContour(Circle(n, 50)));
            return p;
        }

        static Vector2[] Gear(int n, double r0 = 60, double r1 = 100)
        {
            var p = new Vector2[n];
            for (int i = 0; i < n; i++) { double a = 2 * Math.PI * i / n; double r = (i % 2 == 0) ? r0 : r1; p[i] = new Vector2(r * Math.Cos(a), r * Math.Sin(a)); }
            return p;
        }

        static Polygon Single(Vector2[] pts)
        {
            var p = new Polygon { FillRule = FillRule.EvenOdd };
            p.AddContour(new MeshContour(pts));
            return p;
        }

        static Polygon Squares(int k)
        {
            var p = new Polygon { FillRule = FillRule.EvenOdd };
            int side = (int)Math.Ceiling(Math.Sqrt(k));
            for (int i = 0; i < k; i++)
            {
                double x = (i % side) * 30, y = (i / side) * 30;
                p.AddContour(new MeshContour(new[] {
                    new Vector2(x, y), new Vector2(x + 20, y), new Vector2(x + 20, y + 20), new Vector2(x, y + 20) }));
            }
            return p;
        }

        static (double median, double min, int tris) Measure(Func<Polygon> build, int warmup, int runs)
        {
            for (int i = 0; i < warmup; i++) build().FillIndirect();
            var times = new List<double>();
            int tris = 0;
            for (int i = 0; i < runs; i++)
            {
                var poly = build();
                GC.Collect(); GC.WaitForPendingFinalizers();
                var sw = Stopwatch.StartNew();
                var v = poly.FillIndirect();
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
                tris = v.Count / 3;
            }
            times.Sort();
            return (times[times.Count / 2], times[0], tris);
        }

        static bool IsDebug()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }
}
