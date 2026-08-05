using System;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.Engine.GraphicsTests
{
    // Regression tests for the two dash/trim stroke-expander bugs, dispatching the REAL production kernel
    // (StrokeEffect.fx -> StrokeDashCutCS + EmitJoin), linked into EffectsData so there is no prototype drift.
    // Single-thread, sequential emission -> a CPU readback of the vertex buffer + VkDrawIndirectCommand is deterministic.
    [TestFixture]
    public class StrokeDashCutTests
    {
        // BUG 1: a dashed stroke crossing a SHARP corner used to emit BOTH corner triangles in EmitJoin - the inner one
        // has no adjacent dash quad to hide it, so it showed as an inward chevron. The wedge is now filled on BOTH sides
        // deliberately (the inner triangle lands inside the two quads' overlap, harmless for an opaque stroke) - which
        // beats having to pick, and sometimes mis-pick, the outer side on a curve.
        // Geometry: open L (0,0)->(100,0)->(100,100), one dash covering the whole contour (pattern [1000,1000] so the
        // first "on" run spans both 100-long segments and crosses the corner), bevel join, flat caps, no round fan,
        // Fringe 0. Layout: quad seg0 (6) + wedge (6) + quad seg1 (6) = 18 verts, and NO cap geometry at either end -
        // caps are a per-fragment mask now, so they can never add a triangle here.
        [Test]
        public void DashJoin_SharpCorner_EmitsWedgeCarryingThePiecesEndDistances()
        {
            float[] pts = { 0f, 0f, 100f, 0f, 100f, 100f };
            float[] pattern = { 1000f, 1000f };   // one "on" run (1000) >> total length (200) -> covers both segments
            const float half = 5f;

            var main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(), 3, "TestApp", true);
            var device = main.CreateRenderDevice();
            var gd = (GraphicsDevice)device;

            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "StrokeEffect.fx"), device);
            var pass = effect.Techniques[0].Passes[1];   // Stroke technique: 0 Expand, 1 DashCut, 2 Draw

            var pointsBuf = MakeBuffer(gd, pts);
            var patternBuf = MakeBuffer(gd, pattern);
            var indirect = MakeIndirect(gd);
            var output = MakeOutput(gd, 4096);

            SetCommon(effect, pointsBuf, output, patternBuf, indirect);
            effect.Parameters["PointCount"].SetValue(3u);
            effect.Parameters["IsClosed"].SetValue(0u);
            effect.Parameters["PatternCount"].SetValue((uint)pattern.Length);
            effect.Parameters["DashOffset"].SetValue(0f);
            effect.Parameters["HalfThickness"].SetValue(half);
            effect.Parameters["Fringe"].SetValue(0f);
            effect.Parameters["JoinType"].SetValue(1u);       // bevel -> single outer corner triangle
            effect.Parameters["RoundSegments"].SetValue(0u);
            effect.Parameters["StartCap"].SetValue(0u);
            effect.Parameters["EndCap"].SetValue(0u);
            effect.Parameters["DashStartCap"].SetValue(0u);
            effect.Parameters["DashEndCap"].SetValue(0u);
            effect.Parameters["MaxVertices"].SetValue(4096u);
            effect.Parameters["TrimStart"].SetValue(0f);
            effect.Parameters["TrimEnd"].SetValue(1f);

            var (cmd4, verts) = RunCut(main, device, gd, pass, indirect, output, 18 * 10);

            Assert.That(cmd4[0], Is.EqualTo(18), "vertexCount: quad(6) + both-sided bevel wedge(6) + quad(6), no cap geometry");

            // The join is the 6 verts right after seg0's quad, 10 floats each:
            // (x, y | perp, uA, vA, arcA | caps, uB, vB, arcB).
            // Corner (100,0), h = 5: plus side (100,5) & (95,0), centre (100,0), then the mirrored minus side.
            float[] expectedWedge =
            {
                100f, 5f, 5f,   95f, 0f, 5f,   100f, 0f, 0f,
                100f, -5f, -5f, 105f, 0f, -5f, 100f, 0f, 0f,
            };
            for (int v = 0; v < 6; v++)
            {
                int o = 60 + v * 10;
                float x = verts[o + 0], y = verts[o + 1];
                Assert.That(x, Is.EqualTo(expectedWedge[v * 3 + 0]).Within(0.01f), $"wedge vert {v} x");
                Assert.That(y, Is.EqualTo(expectedWedge[v * 3 + 1]).Within(0.01f), $"wedge vert {v} y");
                Assert.That(verts[o + 2], Is.EqualTo(expectedWedge[v * 3 + 2]).Within(0.01f), $"wedge vert {v} perp");

                // The load-bearing part: the wedge belongs to the dash PIECE crossing the corner, so it carries that
                // piece's two END FRAMES (the cap's shape) AND its arc distance to both ends (the cap's reach). Without
                // the frames a concave cap carves a join as a CIRCLE around the corner - a hole punched through the
                // dash; without the arc, a cap shaves ribbon that is far away along the path but happens to lie behind
                // its axis. The piece spans the whole L: it starts at (0,0) heading +x and ends at (100,100) heading
                // +y, and the corner sits at arc 100 of 200 - so uA = x, vA = y, uB = 100 - y, vB = 100 - x, arcs 100.
                Assert.That(verts[o + 3], Is.EqualTo(x).Within(0.01f), $"wedge vert {v} uA");
                Assert.That(verts[o + 4], Is.EqualTo(y).Within(0.01f), $"wedge vert {v} vA");
                Assert.That(verts[o + 5], Is.EqualTo(100f).Within(0.01f), $"wedge vert {v} arcA");
                Assert.That(verts[o + 6], Is.EqualTo(0f).Within(0.01f), $"wedge vert {v} caps (flat/flat)");
                Assert.That(verts[o + 7], Is.EqualTo(100f - y).Within(0.01f), $"wedge vert {v} uB");
                Assert.That(verts[o + 8], Is.EqualTo(100f - x).Within(0.01f), $"wedge vert {v} vB");
                Assert.That(verts[o + 9], Is.EqualTo(100f).Within(0.01f), $"wedge vert {v} arcB");
            }

            main.Dispose();
        }

        // BUG 1 follow-up: a dash toggle landing EXACTLY on a corner must NOT draw the join wedge. Open L
        // (0,0)->(100,0)->(100,100), each segment 100 long, pattern [50 on, 50 off] offset 0: the gap on segment 0 ends
        // right at the corner (arc 100) where the pattern flips back on, so the incoming side is empty - a lone wedge
        // there juts out as a triangular tab. With the fix (lastSpanOn && on) no wedge is emitted: quad(6, seg0 [0,50])
        // + quad(6, seg1 [100,150]) = 12 verts, NOT 15.
        [Test]
        public void DashJoin_ToggleExactlyOnCorner_EmitsNoWedge()
        {
            float[] pts = { 0f, 0f, 100f, 0f, 100f, 100f };
            float[] pattern = { 50f, 50f };
            const float half = 5f;

            var main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(), 3, "TestApp", true);
            var device = main.CreateRenderDevice();
            var gd = (GraphicsDevice)device;

            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "StrokeEffect.fx"), device);
            var pass = effect.Techniques[0].Passes[1];

            var pointsBuf = MakeBuffer(gd, pts);
            var patternBuf = MakeBuffer(gd, pattern);
            var indirect = MakeIndirect(gd);
            var output = MakeOutput(gd, 4096);

            SetCommon(effect, pointsBuf, output, patternBuf, indirect);
            effect.Parameters["PointCount"].SetValue(3u);
            effect.Parameters["IsClosed"].SetValue(0u);
            effect.Parameters["PatternCount"].SetValue((uint)pattern.Length);
            effect.Parameters["DashOffset"].SetValue(0f);
            effect.Parameters["HalfThickness"].SetValue(half);
            effect.Parameters["Fringe"].SetValue(0f);
            effect.Parameters["JoinType"].SetValue(1u);
            effect.Parameters["RoundSegments"].SetValue(0u);
            effect.Parameters["StartCap"].SetValue(0u);
            effect.Parameters["EndCap"].SetValue(0u);
            effect.Parameters["DashStartCap"].SetValue(0u);
            effect.Parameters["DashEndCap"].SetValue(0u);
            effect.Parameters["MaxVertices"].SetValue(4096u);
            effect.Parameters["TrimStart"].SetValue(0f);
            effect.Parameters["TrimEnd"].SetValue(1f);

            var (cmd4, _) = RunCut(main, device, gd, pass, indirect, output, 0);

            Assert.That(cmd4[0], Is.EqualTo(12), "dash toggles on exactly at the corner -> incoming side empty -> no wedge (quad+quad, no join)");

            main.Dispose();
        }

        // BUG 2 (shader half): an EMPTY trim window (TrimStart == TrimEnd == 0) must expand to nothing. Confirms the
        // StrokeDashCutCS emission is innocent (vertexCount == 0) - so any stray pixel seen live is in the C# draw path
        // (a stale vertex/indirect slot being drawn), NOT the shader. Closed square, trim-only (no dashes).
        [Test]
        public void Trim_EmptyWindow_EmitsNoVertices()
        {
            float[] pts = { 0f, 0f, 100f, 0f, 100f, 100f, 0f, 100f };   // closed square, contour starts at (0,0)
            float[] dummyPattern = { 1f };

            var main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(), 3, "TestApp", true);
            var device = main.CreateRenderDevice();
            var gd = (GraphicsDevice)device;

            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "StrokeEffect.fx"), device);
            var pass = effect.Techniques[0].Passes[1];

            var pointsBuf = MakeBuffer(gd, pts);
            var patternBuf = MakeBuffer(gd, dummyPattern);
            var indirect = MakeIndirect(gd);
            var output = MakeOutput(gd, 4096);

            SetCommon(effect, pointsBuf, output, patternBuf, indirect);
            effect.Parameters["PointCount"].SetValue(4u);
            effect.Parameters["IsClosed"].SetValue(1u);
            effect.Parameters["PatternCount"].SetValue(0u);   // trim only
            effect.Parameters["DashOffset"].SetValue(0f);
            effect.Parameters["HalfThickness"].SetValue(5f);
            effect.Parameters["Fringe"].SetValue(0f);
            effect.Parameters["JoinType"].SetValue(0u);
            effect.Parameters["RoundSegments"].SetValue(0u);
            effect.Parameters["StartCap"].SetValue(0u);
            effect.Parameters["EndCap"].SetValue(0u);
            effect.Parameters["DashStartCap"].SetValue(0u);
            effect.Parameters["DashEndCap"].SetValue(0u);
            effect.Parameters["MaxVertices"].SetValue(4096u);
            effect.Parameters["TrimStart"].SetValue(0f);
            effect.Parameters["TrimEnd"].SetValue(0f);

            var (cmd4, _) = RunCut(main, device, gd, pass, indirect, output, 0);

            Assert.That(cmd4[0], Is.EqualTo(0), "empty trim window must emit zero vertices");

            main.Dispose();
        }

        // BUG 2: the stray cap-dots at the trim EXTREMES ([0,0]/[1,1]/any start==end) are NOT a shader emit - the cut
        // kernel correctly writes vertexCount 0 for an empty window (asserted below). The real cause is C#-side: a
        // DrawIndirect with a 0 count still rasterizes the vertex buffer's STALE previous-frame geometry, so
        // GpuStrokeRenderComponent.Render now skips the draw when the trim window is empty. These tests lock the shader
        // half (empty window -> 0 vertices); a partial window still emits a real (capped) piece.
        [Test]
        public void Trim_ExactEqualWindow_RoundCaps_EmitsNothing()
        {
            var (cmd4, _) = RunEllipseTrim(0.5f, 0.5f, 0f, out _);
            Assert.That(cmd4[0], Is.EqualTo(0), "exact empty trim window emits nothing even with round caps");
        }

        [Test]
        public void Trim_PartialWindow_StillEmits()
        {
            // window = 0.01 * ~440 ~= 4.4 device px -> a real capped piece is emitted. Exactly one quad and no more: the
            // round caps that used to add two 16-triangle disc fans here are a per-fragment mask now.
            var (cmd4, _) = RunEllipseTrim(0.5f, 0.51f, 1f, out var main);
            Assert.That(cmd4[0], Is.GreaterThanOrEqualTo(6), "a non-empty trim window still renders");
            main.Dispose();
        }

        // Dispatches the cut on a 48-point closed ellipse with ConvexRound caps everywhere, no dashes, the given trim +
        // fringe. Returns the indirect command; leaves `main` for the caller to dispose (or discards it when unused).
        private static (int[] cmd4, float[] verts) RunEllipseTrim(float trimStart, float trimEnd, float fringe, out MainGraphicsDevice main)
        {
            const int n = 48;
            var pts = new float[n * 2];
            for (int i = 0; i < n; i++)
            {
                double a = 2.0 * Math.PI * i / n;
                pts[i * 2] = (float)(80.0 * Math.Cos(a));
                pts[i * 2 + 1] = (float)(60.0 * Math.Sin(a));
            }

            main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(), 3, "TestApp", true);
            var device = main.CreateRenderDevice();
            var gd = (GraphicsDevice)device;

            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "StrokeEffect.fx"), device);
            var pass = effect.Techniques[0].Passes[1];

            var pointsBuf = MakeBuffer(gd, pts);
            var patternBuf = MakeBuffer(gd, new float[] { 1f });
            var indirect = MakeIndirect(gd);
            var output = MakeOutput(gd, 8192);

            SetCommon(effect, pointsBuf, output, patternBuf, indirect);
            effect.Parameters["PointCount"].SetValue((uint)n);
            effect.Parameters["IsClosed"].SetValue(1u);
            effect.Parameters["PatternCount"].SetValue(0u);
            effect.Parameters["DashOffset"].SetValue(0f);
            effect.Parameters["HalfThickness"].SetValue(4f);
            effect.Parameters["Fringe"].SetValue(fringe);
            effect.Parameters["JoinType"].SetValue(2u);          // round join (demo default)
            effect.Parameters["RoundSegments"].SetValue(16u);
            effect.Parameters["StartCap"].SetValue(2u);          // ConvexRound - matches the demo binding
            effect.Parameters["EndCap"].SetValue(2u);
            effect.Parameters["DashStartCap"].SetValue(2u);
            effect.Parameters["DashEndCap"].SetValue(2u);
            effect.Parameters["MaxVertices"].SetValue(8192u);
            effect.Parameters["TrimStart"].SetValue(trimStart);
            effect.Parameters["TrimEnd"].SetValue(trimEnd);

            var (cmd4, verts) = RunCut(main, device, gd, pass, indirect, output, 0);
            return (cmd4, verts);
        }

        private static void SetCommon(Effect effect, Adamantium.Graphics.Buffer points, Adamantium.Graphics.Buffer output,
            Adamantium.Graphics.Buffer pattern, Adamantium.Graphics.Buffer indirect)
        {
            effect.Parameters["PointsAddress"].SetValue(points.GetDeviceAddress());
            effect.Parameters["OutputAddress"].SetValue(output.GetDeviceAddress());
            effect.Parameters["PatternAddress"].SetValue(pattern.GetDeviceAddress());
            effect.Parameters["IndirectAddress"].SetValue(indirect.GetDeviceAddress());
            effect.Parameters["DashMode"].SetValue(1u);
        }

        // Dispatches the single-thread cut, reads back the indirect command (4 uints) and the first `outFloats` output
        // floats. Mirrors the frame flow of the existing ComputeTests (compute in BeginDraw's beforeRenderPass hook).
        private static (int[] cmd4, float[] verts) RunCut(MainGraphicsDevice main, IGraphicsDevice device,
            GraphicsDevice gd, IEffectPass pass, Adamantium.Graphics.Buffer indirect, Adamantium.Graphics.Buffer output, int outFloats)
        {
            var prms = new PresentationParameters(PresenterType.RenderTarget, 16, 16, IntPtr.Zero);
            using var presenter = GraphicsPresenter.Create(device, prms, "stroke_dashcut");
            device.SetRenderTargets(presenter.RenderTarget);
            device.SetDepthBuffer(presenter.DepthBuffer);
            device.MSAALevel = presenter.MSAALevel;
            device.Presenter = presenter;

            Assert.That(device.BeginDraw(beforeRenderPass: cmd =>
            {
                pass.Apply();
                gd.Dispatch(1);
                gd.BufferBarrier(indirect,
                    PipelineStageFlagBits2.ComputeShaderBit, AccessFlagBits2.ShaderWriteBit,
                    PipelineStageFlagBits2.HostBit, AccessFlagBits2.HostReadBit);
                gd.BufferBarrier(output,
                    PipelineStageFlagBits2.ComputeShaderBit, AccessFlagBits2.ShaderWriteBit,
                    PipelineStageFlagBits2.HostBit, AccessFlagBits2.HostReadBit);
            }), Is.True);
            device.EndDraw();
            device.Submit();
            presenter.Present();
            device.FrameEnded();
            device.DeviceWaitIdle();

            var cmd4 = new int[4];
            var cp = indirect.MapMemory();
            Marshal.Copy((IntPtr)(nint)cp, cmd4, 0, 4);
            indirect.UnmapMemory();

            var verts = new float[Math.Max(outFloats, 1)];
            if (outFloats > 0)
            {
                var op = output.MapMemory();
                Marshal.Copy((IntPtr)(nint)op, verts, 0, outFloats);
                output.UnmapMemory();
            }
            return (cmd4, verts);
        }

        private static Adamantium.Graphics.Buffer MakeBuffer(GraphicsDevice gd, float[] data)
        {
            var buf = Adamantium.Graphics.Buffer.New(gd, (ulong)(data.Length * sizeof(float)),
                BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
                MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal);
            var p = buf.MapMemory();
            Marshal.Copy(data, 0, (IntPtr)(nint)p, data.Length);
            buf.UnmapMemory();
            return buf;
        }

        private static Adamantium.Graphics.Buffer MakeIndirect(GraphicsDevice gd) =>
            Adamantium.Graphics.Buffer.New(gd, (ulong)(4 * sizeof(uint)),
                BufferUsageFlags.IndirectBuffer | BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
                MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal);

        private static Adamantium.Graphics.Buffer MakeOutput(GraphicsDevice gd, int floats) =>
            Adamantium.Graphics.Buffer.New(gd, (ulong)(floats * sizeof(float)),
                BufferUsageFlags.VertexBuffer | BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
                MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal);
    }
}
