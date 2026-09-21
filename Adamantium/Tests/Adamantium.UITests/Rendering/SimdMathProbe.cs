using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// Is there anything in SIMD for this engine's math? The engine detects AVX2/SSE4.2 (AcceleratedMathConfig) and then
/// uses it nowhere; Matrix4x4F.Multiply is 64 scalar multiplies and 48 adds.
///
/// Benchmarked HONESTLY, which the first attempt was not: that one called each operation through a lambda with captured
/// locals, so a 64-byte struct travelled through a display class and System.Numerics came out FIVE TIMES SLOWER than the
/// hand-rolled scalar version - a number that says more about the harness than about SIMD. Here the timed loop is a
/// plain loop over an array, the result is accumulated into a sink the JIT cannot discard, and every contender is fed
/// the same data in the same shape.
/// </summary>
[TestFixture]
[Explicit("Measurement probe - run it deliberately and read the numbers")]
public class SimdMathProbe
{
    private const int N = 4096;        // fits L1 comfortably, so this measures the math and not the memory
    private const int Reps = 200;

    // Row-major, 4 floats per row, sequential layout - the same shape Matrix4x4F declares.
    private static Matrix4x4F MulSimd(in Matrix4x4F l, in Matrix4x4F r)
    {
        ref var lf = ref Unsafe.As<Matrix4x4F, float>(ref Unsafe.AsRef(in l));
        ref var rf = ref Unsafe.As<Matrix4x4F, float>(ref Unsafe.AsRef(in r));

        var r0 = Vector128.LoadUnsafe(ref rf, 0);
        var r1 = Vector128.LoadUnsafe(ref rf, 4);
        var r2 = Vector128.LoadUnsafe(ref rf, 8);
        var r3 = Vector128.LoadUnsafe(ref rf, 12);

        Matrix4x4F result = default;
        ref var of = ref Unsafe.As<Matrix4x4F, float>(ref result);

        for (nuint row = 0; row < 4; row++)
        {
            var b = row * 4;
            var acc = Vector128.Create(Unsafe.Add(ref lf, b + 0)) * r0
                    + Vector128.Create(Unsafe.Add(ref lf, b + 1)) * r1
                    + Vector128.Create(Unsafe.Add(ref lf, b + 2)) * r2
                    + Vector128.Create(Unsafe.Add(ref lf, b + 3)) * r3;
            acc.StoreUnsafe(ref of, b);
        }

        return result;
    }

    /// <summary>Second attempt, with the three things the first one got wrong: the left matrix is loaded as VECTORS and
    /// its lanes broadcast by shuffle (the first version did sixteen scalar loads from memory, one per broadcast), the
    /// rows are unrolled instead of looped over an index, and the whole thing is forced inline - which is what lets the
    /// JIT keep all eight vectors in registers instead of spilling them.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Matrix4x4F MulSimd2(in Matrix4x4F l, in Matrix4x4F r)
    {
        ref var lf = ref Unsafe.As<Matrix4x4F, float>(ref Unsafe.AsRef(in l));
        ref var rf = ref Unsafe.As<Matrix4x4F, float>(ref Unsafe.AsRef(in r));

        var r0 = Vector128.LoadUnsafe(ref rf, 0);
        var r1 = Vector128.LoadUnsafe(ref rf, 4);
        var r2 = Vector128.LoadUnsafe(ref rf, 8);
        var r3 = Vector128.LoadUnsafe(ref rf, 12);

        Unsafe.SkipInit(out Matrix4x4F result);
        ref var of = ref Unsafe.As<Matrix4x4F, float>(ref result);

        Row(Vector128.LoadUnsafe(ref lf, 0), r0, r1, r2, r3).StoreUnsafe(ref of, 0);
        Row(Vector128.LoadUnsafe(ref lf, 4), r0, r1, r2, r3).StoreUnsafe(ref of, 4);
        Row(Vector128.LoadUnsafe(ref lf, 8), r0, r1, r2, r3).StoreUnsafe(ref of, 8);
        Row(Vector128.LoadUnsafe(ref lf, 12), r0, r1, r2, r3).StoreUnsafe(ref of, 12);
        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<float> Row(Vector128<float> a, Vector128<float> r0, Vector128<float> r1,
            Vector128<float> r2, Vector128<float> r3)
            => Vector128.Shuffle(a, Vector128.Create(0, 0, 0, 0)) * r0
             + Vector128.Shuffle(a, Vector128.Create(1, 1, 1, 1)) * r1
             + Vector128.Shuffle(a, Vector128.Create(2, 2, 2, 2)) * r2
             + Vector128.Shuffle(a, Vector128.Create(3, 3, 3, 3)) * r3;
    }

    /// <summary>A Matrix4x4F-shaped struct backed by System.Numerics.Matrix4x4. The question this answers is NOT whether
    /// the runtime's type is fast - that is already measured - but whether WRAPPING it keeps the win: the engine's API is
    /// M11..M44 fields, and turning those into properties over an inner struct could cost exactly what SIMD gains.
    /// Field ORDER is the same (row-major M11..M44), so a wrapper can be reinterpreted from the old layout.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MatrixWrapped
    {
        private System.Numerics.Matrix4x4 _m;

        public MatrixWrapped(System.Numerics.Matrix4x4 m) => _m = m;

        // The engine reads and writes these by name everywhere; as properties over the inner struct they are a field
        // access after inlining, which is the thing to prove rather than assume.
        public float M11 { get => _m.M11; set => _m.M11 = value; }
        public float M12 { get => _m.M12; set => _m.M12 = value; }
        public float M41 { get => _m.M41; set => _m.M41 = value; }
        public float M42 { get => _m.M42; set => _m.M42 = value; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MatrixWrapped operator *(MatrixWrapped a, MatrixWrapped b) => new(a._m * b._m);

        public static MatrixWrapped Translation(float x, float y, float z)
            => new(System.Numerics.Matrix4x4.CreateTranslation(x, y, z));

        public static MatrixWrapped Scaling(float x, float y, float z)
            => new(System.Numerics.Matrix4x4.CreateScale(x, y, z));
    }

    /// <summary>Our scalar multiply WITHOUT the temporary. The shipped one builds a local Matrix4x4F, fills sixteen
    /// fields and then copies all 64 bytes into the out parameter - so it is fair to ask how much of its cost is
    /// arithmetic and how much is that copy, before concluding anything about SIMD.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MulScalarNoTemp(ref Matrix4x4F l, ref Matrix4x4F r, out Matrix4x4F o)
    {
        o.M11 = l.M11 * r.M11 + l.M12 * r.M21 + l.M13 * r.M31 + l.M14 * r.M41;
        o.M12 = l.M11 * r.M12 + l.M12 * r.M22 + l.M13 * r.M32 + l.M14 * r.M42;
        o.M13 = l.M11 * r.M13 + l.M12 * r.M23 + l.M13 * r.M33 + l.M14 * r.M43;
        o.M14 = l.M11 * r.M14 + l.M12 * r.M24 + l.M13 * r.M34 + l.M14 * r.M44;
        o.M21 = l.M21 * r.M11 + l.M22 * r.M21 + l.M23 * r.M31 + l.M24 * r.M41;
        o.M22 = l.M21 * r.M12 + l.M22 * r.M22 + l.M23 * r.M32 + l.M24 * r.M42;
        o.M23 = l.M21 * r.M13 + l.M22 * r.M23 + l.M23 * r.M33 + l.M24 * r.M43;
        o.M24 = l.M21 * r.M14 + l.M22 * r.M24 + l.M23 * r.M34 + l.M24 * r.M44;
        o.M31 = l.M31 * r.M11 + l.M32 * r.M21 + l.M33 * r.M31 + l.M34 * r.M41;
        o.M32 = l.M31 * r.M12 + l.M32 * r.M22 + l.M33 * r.M32 + l.M34 * r.M42;
        o.M33 = l.M31 * r.M13 + l.M32 * r.M23 + l.M33 * r.M33 + l.M34 * r.M43;
        o.M34 = l.M31 * r.M14 + l.M32 * r.M24 + l.M33 * r.M34 + l.M34 * r.M44;
        o.M41 = l.M41 * r.M11 + l.M42 * r.M21 + l.M43 * r.M31 + l.M44 * r.M41;
        o.M42 = l.M41 * r.M12 + l.M42 * r.M22 + l.M43 * r.M32 + l.M44 * r.M42;
        o.M43 = l.M41 * r.M13 + l.M42 * r.M23 + l.M43 * r.M33 + l.M44 * r.M43;
        o.M44 = l.M41 * r.M14 + l.M42 * r.M24 + l.M43 * r.M34 + l.M44 * r.M44;
    }

    /// <summary>AVX: TWO rows per instruction. The right-hand rows are duplicated into both 128-bit halves of a 256-bit
    /// register, the left's rows come in pairs, and Permute broadcasts a lane WITHIN each half - so one pass computes
    /// rows 0+1 and the second rows 2+3. Half the instruction count of the Vector128 version.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Matrix4x4F MulAvx(in Matrix4x4F l, in Matrix4x4F r)
    {
        ref var lf = ref Unsafe.As<Matrix4x4F, float>(ref Unsafe.AsRef(in l));
        ref var rf = ref Unsafe.As<Matrix4x4F, float>(ref Unsafe.AsRef(in r));

        var r0 = Vector128.LoadUnsafe(ref rf, 0);
        var r1 = Vector128.LoadUnsafe(ref rf, 4);
        var r2 = Vector128.LoadUnsafe(ref rf, 8);
        var r3 = Vector128.LoadUnsafe(ref rf, 12);

        var R0 = Vector256.Create(r0, r0);
        var R1 = Vector256.Create(r1, r1);
        var R2 = Vector256.Create(r2, r2);
        var R3 = Vector256.Create(r3, r3);

        Unsafe.SkipInit(out Matrix4x4F result);
        ref var of = ref Unsafe.As<Matrix4x4F, float>(ref result);

        Pair(Vector256.LoadUnsafe(ref lf, 0), R0, R1, R2, R3).StoreUnsafe(ref of, 0);
        Pair(Vector256.LoadUnsafe(ref lf, 8), R0, R1, R2, R3).StoreUnsafe(ref of, 8);
        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<float> Pair(Vector256<float> a, Vector256<float> R0, Vector256<float> R1,
            Vector256<float> R2, Vector256<float> R3)
            => System.Runtime.Intrinsics.X86.Avx.Permute(a, 0x00) * R0
             + System.Runtime.Intrinsics.X86.Avx.Permute(a, 0x55) * R1
             + System.Runtime.Intrinsics.X86.Avx.Permute(a, 0xAA) * R2
             + System.Runtime.Intrinsics.X86.Avx.Permute(a, 0xFF) * R3;
    }

    /// <summary>FMA: multiply and add as ONE instruction, so each row costs four fused ops instead of four multiplies
    /// plus three adds - and with one rounding step instead of two, which is also slightly more accurate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Matrix4x4F MulFma(in Matrix4x4F l, in Matrix4x4F r)
    {
        ref var lf = ref Unsafe.As<Matrix4x4F, float>(ref Unsafe.AsRef(in l));
        ref var rf = ref Unsafe.As<Matrix4x4F, float>(ref Unsafe.AsRef(in r));

        var r0 = Vector128.LoadUnsafe(ref rf, 0);
        var r1 = Vector128.LoadUnsafe(ref rf, 4);
        var r2 = Vector128.LoadUnsafe(ref rf, 8);
        var r3 = Vector128.LoadUnsafe(ref rf, 12);

        Unsafe.SkipInit(out Matrix4x4F result);
        ref var of = ref Unsafe.As<Matrix4x4F, float>(ref result);

        Row(Vector128.LoadUnsafe(ref lf, 0), r0, r1, r2, r3).StoreUnsafe(ref of, 0);
        Row(Vector128.LoadUnsafe(ref lf, 4), r0, r1, r2, r3).StoreUnsafe(ref of, 4);
        Row(Vector128.LoadUnsafe(ref lf, 8), r0, r1, r2, r3).StoreUnsafe(ref of, 8);
        Row(Vector128.LoadUnsafe(ref lf, 12), r0, r1, r2, r3).StoreUnsafe(ref of, 12);
        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<float> Row(Vector128<float> a, Vector128<float> r0, Vector128<float> r1,
            Vector128<float> r2, Vector128<float> r3)
        {
            var acc = System.Runtime.Intrinsics.X86.Sse.Shuffle(a, a, 0x00) * r0;
            acc = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(System.Runtime.Intrinsics.X86.Sse.Shuffle(a, a, 0x55), r1, acc);
            acc = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(System.Runtime.Intrinsics.X86.Sse.Shuffle(a, a, 0xAA), r2, acc);
            acc = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(System.Runtime.Intrinsics.X86.Sse.Shuffle(a, a, 0xFF), r3, acc);
            return acc;
        }
    }

    private static int UnmanagedProof<T>() where T : unmanaged => Unsafe.SizeOf<T>();

    /// <summary>The pointer question. Every SIMD variant above indexes arrays, and each index carries a bounds check
    /// plus a write barrier check the JIT may or may not hoist. Pinning the three arrays once and walking raw float*
    /// removes all of that - so this measures what pointers are actually worth here, rather than assuming they are
    /// free money. The arithmetic is byte-for-byte the same AVX kernel as MulAvx.</summary>
    private static unsafe void MulAvxBulk(Matrix4x4F[] a, Matrix4x4F[] b, Matrix4x4F[] o)
    {
        fixed (Matrix4x4F* pa = a)
        fixed (Matrix4x4F* pb = b)
        fixed (Matrix4x4F* po = o)
        {
            var la = (float*)pa;
            var lb = (float*)pb;
            var lo = (float*)po;
            for (var i = 0; i < N; i++, la += 16, lb += 16, lo += 16)
            {
                var r0 = Vector128.Load(lb);
                var r1 = Vector128.Load(lb + 4);
                var r2 = Vector128.Load(lb + 8);
                var r3 = Vector128.Load(lb + 12);
                var R0 = Vector256.Create(r0, r0);
                var R1 = Vector256.Create(r1, r1);
                var R2 = Vector256.Create(r2, r2);
                var R3 = Vector256.Create(r3, r3);

                PairP(Vector256.Load(la), R0, R1, R2, R3).Store(lo);
                PairP(Vector256.Load(la + 8), R0, R1, R2, R3).Store(lo + 8);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<float> PairP(Vector256<float> two, Vector256<float> r0, Vector256<float> r1,
            Vector256<float> r2, Vector256<float> r3)
            => System.Runtime.Intrinsics.X86.Avx.Permute(two, 0x00) * r0
             + System.Runtime.Intrinsics.X86.Avx.Permute(two, 0x55) * r1
             + System.Runtime.Intrinsics.X86.Avx.Permute(two, 0xAA) * r2
             + System.Runtime.Intrinsics.X86.Avx.Permute(two, 0xFF) * r3;
    }

    /// <summary>The same pinning applied to the SCALAR kernel - the control that says whether a pointer win, if any,
    /// comes from the pinning or from the vector code.</summary>
    private static unsafe void MulScalarBulk(Matrix4x4F[] a, Matrix4x4F[] b, Matrix4x4F[] o)
    {
        fixed (Matrix4x4F* pa = a)
        fixed (Matrix4x4F* pb = b)
        fixed (Matrix4x4F* po = o)
        {
            for (var i = 0; i < N; i++) Matrix4x4F.Multiply(ref pa[i], ref pb[i], out po[i]);
        }
    }

    private static void Report(string what, long ticks, int ops)
        => TestContext.Out.WriteLine($"  {what,-42} {System.Diagnostics.Stopwatch.GetElapsedTime(0, ticks).TotalMilliseconds * 1000000 / ops,8:F2} ns/op");

    /// <summary>Every contender writes its FULL result into an output array. The first version of this test consumed only
    /// result.M11 - which let the JIT delete the other fifteen fields from the inlined scalar version (7 flops instead of
    /// 112) while the big System.Numerics operator kept all of them. That is not a comparison, and it produced two
    /// contradictory sets of numbers before the impossible one gave it away: 3.4ns for 112 scalar flops would be eight
    /// operations per cycle.</summary>
    [Test]
    public void MatrixMultiply()
    {
        TestContext.Out.WriteLine("  CPU: " + string.Join(" ", new[]
        {
            $"Sse41={System.Runtime.Intrinsics.X86.Sse41.IsSupported}",
            $"Avx={System.Runtime.Intrinsics.X86.Avx.IsSupported}",
            $"Avx2={System.Runtime.Intrinsics.X86.Avx2.IsSupported}",
            $"Fma={System.Runtime.Intrinsics.X86.Fma.IsSupported}",
            $"Avx512F={System.Runtime.Intrinsics.X86.Avx512F.IsSupported}",
            $"V256hw={Vector256.IsHardwareAccelerated}",
            $"V512hw={Vector512.IsHardwareAccelerated}",
        }));

        var a = new Matrix4x4F[N];
        var b = new Matrix4x4F[N];
        var outOurs = new Matrix4x4F[N];
        var na = new System.Numerics.Matrix4x4[N];
        var nb = new System.Numerics.Matrix4x4[N];
        var outNum = new System.Numerics.Matrix4x4[N];
        for (var i = 0; i < N; i++)
        {
            a[i] = Matrix4x4F.Translation(i * 0.5f, i * 0.25f, 1) * Matrix4x4F.Scaling(1.001f, 0.999f, 1f);
            b[i] = Matrix4x4F.Scaling(1.002f, 1.003f, 1f) * Matrix4x4F.Translation(i * 0.1f, 2, 3);
            na[i] = System.Numerics.Matrix4x4.CreateTranslation(i * 0.5f, i * 0.25f, 1);
            nb[i] = System.Numerics.Matrix4x4.CreateScale(1.002f, 1.003f, 1f);
        }

        void Bench(string what, Action run)
        {
            for (var w = 0; w < 5; w++) run();
            var t = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var r = 0; r < Reps; r++) run();
            Report(what, System.Diagnostics.Stopwatch.GetTimestamp() - t, N * Reps);
        }

        Bench("Matrix4x4F.Multiply (ours, shipped)", () => {
            for (var i = 0; i < N; i++) Matrix4x4F.Multiply(ref a[i], ref b[i], out outOurs[i]); });

        Bench("ours scalar, no temp copy", () => {
            for (var i = 0; i < N; i++) MulScalarNoTemp(ref a[i], ref b[i], out outOurs[i]); });

        Bench("SSE (Vector128, shuffle+unroll)", () => {
            for (var i = 0; i < N; i++) outOurs[i] = MulSimd2(in a[i], in b[i]); });

        Bench("FMA (fused multiply-add)", () => {
            for (var i = 0; i < N; i++) outOurs[i] = MulFma(in a[i], in b[i]); });

        Bench("AVX (Vector256, two rows/op)", () => {
            for (var i = 0; i < N; i++) outOurs[i] = MulAvx(in a[i], in b[i]); });

        Bench("System.Numerics.Matrix4x4 *", () => {
            for (var i = 0; i < N; i++) outNum[i] = na[i] * nb[i]; });

        // Composition, which is what transform code actually does: local * parent * projection.
        Bench("ours scalar, chain of three", () => {
            for (var i = 0; i < N; i++) outOurs[i] = a[i] * b[i] * a[i]; });

        Bench("AVX, chain of three", () => {
            for (var i = 0; i < N; i++) outOurs[i] = MulAvx(MulAvx(in a[i], in b[i]), in a[i]); });

        Bench("System.Numerics, chain of three", () => {
            for (var i = 0; i < N; i++) outNum[i] = na[i] * nb[i] * na[i]; });

        TestContext.Out.WriteLine($"  (checksum {outOurs[7].M11 + outNum[7].M11:E3})");
    }

    /// <summary>Same correction as MatrixMultiply: the first version consumed only .X, which let the JIT delete three
    /// quarters of the scalar work while System.Numerics kept all four lanes. Here every result goes into an output
    /// array in full.</summary>
    [Test]
    public void VectorMultiply()
    {
        var a = new Vector4F[N];
        var b = new Vector4F[N];
        var outOurs = new Vector4F[N];
        var na = new System.Numerics.Vector4[N];
        var nb = new System.Numerics.Vector4[N];
        var outNum = new System.Numerics.Vector4[N];
        for (var i = 0; i < N; i++)
        {
            a[i] = new Vector4F(i * 0.5f, i * 0.25f, 1, 2);
            b[i] = new Vector4F(1.001f, 0.999f, 2, 0.5f);
            na[i] = new System.Numerics.Vector4(i * 0.5f, i * 0.25f, 1, 2);
            nb[i] = new System.Numerics.Vector4(1.001f, 0.999f, 2, 0.5f);
        }

        void Bench(string what, Action run)
        {
            for (var w = 0; w < 5; w++) run();
            var t = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var r = 0; r < Reps; r++) run();
            Report(what, System.Diagnostics.Stopwatch.GetTimestamp() - t, N * Reps);
        }

        Bench("Vector4F * (ours, scalar)", () => {
            for (var i = 0; i < N; i++) outOurs[i] = a[i] * b[i]; });

        Bench("Vector4F * (SSE, Vector128)", () => {
            for (var i = 0; i < N; i++) outOurs[i] = MulVec128(in a[i], in b[i]); });

        Bench("System.Numerics.Vector4 *", () => {
            for (var i = 0; i < N; i++) outNum[i] = na[i] * nb[i]; });

        // What vector code actually does: transform a point by a matrix. Four dot products, or four broadcasts and a
        // sum of scaled rows - the shape where SIMD normally pays.
        var m = Matrix4x4F.Translation(1, 2, 3) * Matrix4x4F.Scaling(1.5f, 2f, 0.5f);
        var nm = System.Numerics.Matrix4x4.CreateTranslation(1, 2, 3);

        Bench("Transform point (ours, scalar)", () => {
            for (var i = 0; i < N; i++) outOurs[i] = Vector4F.Transform(a[i], m); });

        Bench("Transform point (SSE, Vector128)", () => {
            for (var i = 0; i < N; i++) outOurs[i] = TransformVec128(in a[i], in m); });

        Bench("Transform point (FMA)", () => {
            for (var i = 0; i < N; i++) outOurs[i] = TransformFma(in a[i], in m); });

        Bench("Transform point (System.Numerics)", () => {
            for (var i = 0; i < N; i++) outNum[i] = System.Numerics.Vector4.Transform(na[i], nm); });

        TestContext.Out.WriteLine($"  (checksum {outOurs[7].X + outNum[7].X:E3})");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4F MulVec128(in Vector4F l, in Vector4F r)
    {
        var v = Vector128.LoadUnsafe(ref Unsafe.As<Vector4F, float>(ref Unsafe.AsRef(in l)))
              * Vector128.LoadUnsafe(ref Unsafe.As<Vector4F, float>(ref Unsafe.AsRef(in r)));
        Unsafe.SkipInit(out Vector4F o);
        v.StoreUnsafe(ref Unsafe.As<Vector4F, float>(ref o));
        return o;
    }

    /// <summary>Broadcast each lane of the vector and scale the matrix rows - the standard SIMD transform, four
    /// multiplies and three adds instead of sixteen and twelve.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4F TransformVec128(in Vector4F v, in Matrix4x4F m)
    {
        ref var mf = ref Unsafe.As<Matrix4x4F, float>(ref Unsafe.AsRef(in m));
        var s = Vector128.LoadUnsafe(ref Unsafe.As<Vector4F, float>(ref Unsafe.AsRef(in v)));
        var acc = Vector128.Shuffle(s, Vector128.Create(0, 0, 0, 0)) * Vector128.LoadUnsafe(ref mf, 0)
                + Vector128.Shuffle(s, Vector128.Create(1, 1, 1, 1)) * Vector128.LoadUnsafe(ref mf, 4)
                + Vector128.Shuffle(s, Vector128.Create(2, 2, 2, 2)) * Vector128.LoadUnsafe(ref mf, 8)
                + Vector128.Shuffle(s, Vector128.Create(3, 3, 3, 3)) * Vector128.LoadUnsafe(ref mf, 12);
        Unsafe.SkipInit(out Vector4F o);
        acc.StoreUnsafe(ref Unsafe.As<Vector4F, float>(ref o));
        return o;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4F TransformFma(in Vector4F v, in Matrix4x4F m)
    {
        if (!System.Runtime.Intrinsics.X86.Fma.IsSupported) return TransformVec128(in v, in m);
        ref var mf = ref Unsafe.As<Matrix4x4F, float>(ref Unsafe.AsRef(in m));
        var s = Vector128.LoadUnsafe(ref Unsafe.As<Vector4F, float>(ref Unsafe.AsRef(in v)));
        var acc = Vector128.Shuffle(s, Vector128.Create(0, 0, 0, 0)) * Vector128.LoadUnsafe(ref mf, 0);
        acc = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(
            Vector128.Shuffle(s, Vector128.Create(1, 1, 1, 1)), Vector128.LoadUnsafe(ref mf, 4), acc);
        acc = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(
            Vector128.Shuffle(s, Vector128.Create(2, 2, 2, 2)), Vector128.LoadUnsafe(ref mf, 8), acc);
        acc = System.Runtime.Intrinsics.X86.Fma.MultiplyAdd(
            Vector128.Shuffle(s, Vector128.Create(3, 3, 3, 3)), Vector128.LoadUnsafe(ref mf, 12), acc);
        Unsafe.SkipInit(out Vector4F o);
        acc.StoreUnsafe(ref Unsafe.As<Vector4F, float>(ref o));
        return o;
    }

    /// <summary>The same contenders at real volume, with the WALL TIME of the whole loop reported.
    ///
    /// This test is also the one that caught the last measurement bug, and it is the subtlest of the lot. At one million
    /// operations System.Numerics.Matrix4x4 measured 99 ns/op; at ten million, 11; at a hundred million, 9.2 - while our
    /// own types held steady. The operator lives in CoreLib and ships PRECOMPILED (ReadyToRun) in a conservative form
    /// with no AVX, because the AOT image cannot assume the CPU it will run on. It is replaced by the fully vectorised
    /// version only after the call-count threshold trips and the background rejit lands. A short warmup therefore times
    /// the SLOW image and calls it the truth. Hence: a warmup measured in MILLIONS, a pause to let the rejit finish, and
    /// a second warmup afterwards - for every contender, so nobody is timed in the wrong tier.</summary>
    [Test]
    public void MatrixMillions()
    {
        var a = new Matrix4x4F[N];
        var b = new Matrix4x4F[N];
        var outOurs = new Matrix4x4F[N];
        var na = new System.Numerics.Matrix4x4[N];
        var nb = new System.Numerics.Matrix4x4[N];
        var outNum = new System.Numerics.Matrix4x4[N];
        var wa = new MatrixWrapped[N];
        var wb = new MatrixWrapped[N];
        var outWrap = new MatrixWrapped[N];
        for (var i = 0; i < N; i++)
        {
            a[i] = Matrix4x4F.Translation(i * 0.5f, i * 0.25f, 1) * Matrix4x4F.Scaling(1.001f, 0.999f, 1f);
            b[i] = Matrix4x4F.Scaling(1.002f, 1.003f, 1f) * Matrix4x4F.Translation(i * 0.1f, 2, 3);
            na[i] = System.Numerics.Matrix4x4.CreateTranslation(i * 0.5f, i * 0.25f, 1);
            nb[i] = System.Numerics.Matrix4x4.CreateScale(1.002f, 1.003f, 1f);
            wa[i] = MatrixWrapped.Translation(i * 0.5f, i * 0.25f, 1);
            wb[i] = MatrixWrapped.Scaling(1.002f, 1.003f, 1f);
        }

        var contenders = new (string Who, string What, Action Run)[]
        {
            ("Matrix4x4F.Multiply (наш, скаляр)", "C = A * B",
                () => { for (var i = 0; i < N; i++) Matrix4x4F.Multiply(ref a[i], ref b[i], out outOurs[i]); }),
            ("наш тип + SSE (Vector128)", "C = A * B",
                () => { for (var i = 0; i < N; i++) outOurs[i] = MulSimd2(in a[i], in b[i]); }),
            ("наш тип + AVX (Vector256)", "C = A * B",
                () => { for (var i = 0; i < N; i++) outOurs[i] = MulAvx(in a[i], in b[i]); }),
            ("наш тип + FMA", "C = A * B",
                () => { for (var i = 0; i < N; i++) outOurs[i] = MulFma(in a[i], in b[i]); }),
            ("System.Numerics.Matrix4x4", "C = A * B",
                () => { for (var i = 0; i < N; i++) outNum[i] = na[i] * nb[i]; }),
            ("обёртка над System.Numerics", "C = A * B",
                () => { for (var i = 0; i < N; i++) outWrap[i] = wa[i] * wb[i]; }),
            ("наш тип + AVX, УКАЗАТЕЛИ", "C = A * B", () => MulAvxBulk(a, b, outOurs)),
            ("наш тип + скаляр, УКАЗАТЕЛИ", "C = A * B", () => MulScalarBulk(a, b, outOurs)),
            ("Matrix4x4F.Multiply (наш, скаляр)", "D = A * B * A",
                () => { for (var i = 0; i < N; i++) outOurs[i] = a[i] * b[i] * a[i]; }),
            ("наш тип + SSE (Vector128)", "D = A * B * A",
                () => { for (var i = 0; i < N; i++) outOurs[i] = MulSimd2(MulSimd2(in a[i], in b[i]), in a[i]); }),
            ("System.Numerics.Matrix4x4", "D = A * B * A",
                () => { for (var i = 0; i < N; i++) outNum[i] = na[i] * nb[i] * na[i]; }),
            ("обёртка над System.Numerics", "D = A * B * A",
                () => { for (var i = 0; i < N; i++) outWrap[i] = wa[i] * wb[i] * wa[i]; }),
        };

        // Every contender is dragged to tier1 BEFORE anyone is timed: millions of calls, a pause for the background
        // rejit to land, then millions more.
        void WarmAll()
        {
            foreach (var c in contenders) for (var r = 0; r < 2_000_000 / N; r++) c.Run();
        }
        WarmAll();
        System.Threading.Thread.Sleep(500);
        WarmAll();

        const int Target = 100_000_000;
        var reps = Target / N;
        var ops = (long)reps * N;

        TestContext.Out.WriteLine($"  {"участник",-34} {"операция",-14} {"млн оп",7} {"весь цикл, мс",14} {"нс/оп",8}");
        foreach (var c in contenders)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var t = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var r = 0; r < reps; r++) c.Run();
            var ms = System.Diagnostics.Stopwatch.GetElapsedTime(t, System.Diagnostics.Stopwatch.GetTimestamp()).TotalMilliseconds;
            TestContext.Out.WriteLine(
                $"  {c.Who,-34} {c.What,-14} {ops / 1_000_000.0,7:F1} {ms,14:F2} {ms * 1_000_000 / ops,8:F2}");
        }

        TestContext.Out.WriteLine($"  (checksum {outOurs[7].M11 + outNum[7].M11 + outWrap[7].M11:E3})");
    }

    /// <summary>The part of the wrapper question that is NOT about speed: does a Matrix4x4F backed by
    /// System.Numerics.Matrix4x4 still travel to the GPU unchanged? Utilities.Write takes a blittable fast path keyed on
    /// Unsafe.SizeOf == Marshal.SizeOf, constant buffers are filled by reinterpreting the struct, and the runtime type is
    /// built internally out of Vector4 rows - which is exactly the kind of thing that can raise alignment and quietly
    /// change a layout. Asserted, because a wrong answer here corrupts every transform on screen rather than slowing it.</summary>
    [Test]
    public void WrapperStaysBlittable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Unsafe.SizeOf<MatrixWrapped>(), Is.EqualTo(64), "wrapper size");
            Assert.That(System.Runtime.InteropServices.Marshal.SizeOf<MatrixWrapped>(), Is.EqualTo(64), "wrapper Marshal size");
            Assert.That(Unsafe.SizeOf<Matrix4x4F>(), Is.EqualTo(64), "ours size");
            Assert.That(System.Runtime.InteropServices.Marshal.SizeOf<Matrix4x4F>(), Is.EqualTo(64), "ours Marshal size");
            Assert.That(RuntimeHelpers.IsReferenceOrContainsReferences<MatrixWrapped>(), Is.False, "wrapper has no refs");
            // EffectParameter does `fixed (Matrix4x4F* p = values)` to upload constants, which the compiler allows only
            // for an UNMANAGED type. The constraint below is that same compile-time check: if a System.Numerics-backed
            // matrix were not unmanaged, this line would not build.
            Assert.That(UnmanagedProof<MatrixWrapped>(), Is.EqualTo(64), "wrapper is unmanaged (compile-time)");
        });

        // Same sixteen floats in the same order, reinterpreted both ways.
        var ours = Matrix4x4F.Translation(7, 8, 9) * Matrix4x4F.Scaling(1.5f, 2.5f, 3.5f);
        var wrapped = Unsafe.As<Matrix4x4F, MatrixWrapped>(ref ours);
        var back = Unsafe.As<MatrixWrapped, Matrix4x4F>(ref wrapped);

        Span<byte> lhs = stackalloc byte[64];
        Span<byte> rhs = stackalloc byte[64];
        Unsafe.WriteUnaligned(ref lhs[0], ours);
        Unsafe.WriteUnaligned(ref rhs[0], wrapped);
        Assert.That(rhs.SequenceEqual(lhs), Is.True, "byte image differs");
        Assert.That((back.M11, back.M41, back.M42, back.M43),
            Is.EqualTo((ours.M11, ours.M41, ours.M42, ours.M43)), "round trip");

        // The alignment question, which size alone does not answer: an array of wrappers must be tightly packed, or a
        // buffer upload of N matrices lands N different rows in the wrong place.
        var arr = new MatrixWrapped[4];
        ref var e0 = ref arr[0];
        ref var e1 = ref arr[1];
        var stride = (long)Unsafe.ByteOffset(ref Unsafe.As<MatrixWrapped, byte>(ref e0),
                                             ref Unsafe.As<MatrixWrapped, byte>(ref e1));
        Assert.That(stride, Is.EqualTo(64), "array stride");
        TestContext.Out.WriteLine($"  wrapper: size 64, Marshal 64, stride {stride}, byte image identical");
    }

    /// <summary>AoS vs SoA, the layout question underneath every "should we use SIMD" discussion.
    ///
    /// Array of Structures is what the engine has: Vector3F[] laid out X0Y0Z0 X1Y1Z1. One 128-bit load brings three
    /// components of ONE vector - three lanes used, one wasted - and combining them (a length, a dot) needs horizontal
    /// shuffles, which is the slow direction.
    ///
    /// Structure of Arrays is three float[] laid out X0X1X2.. Y0Y1Y2.. Z0Z1Z2.. One load brings X of FOUR vectors, the
    /// same instruction serves four bodies, and nothing is shuffled.
    ///
    /// The operation is normalisation - three multiplies, two adds, a square root, three divides - because that is the
    /// shape a solver actually runs, and the sqrt keeps it honest rather than a pure multiply-add showcase. The SoA
    /// SCALAR row is the control that separates the layout from the vectorisation: if it matches AoS scalar, then the
    /// win that follows is the vector code and not merely the rearrangement.</summary>
    [Test]
    public void AosVsSoa()
    {
        var aos = new Vector3F[N];
        var aosOut = new Vector3F[N];
        var sx = new float[N]; var sy = new float[N]; var sz = new float[N];
        var ox = new float[N]; var oy = new float[N]; var oz = new float[N];
        for (var i = 0; i < N; i++)
        {
            float x = 1 + i % 17, y = 2 + i % 13, z = 3 + i % 11;
            aos[i] = new Vector3F(x, y, z);
            sx[i] = x; sy[i] = y; sz[i] = z;
        }

        var contenders = new (string Who, string What, Action Run)[]
        {
            ("AoS, скаляр (как сейчас)", "normalize", () =>
            {
                for (var i = 0; i < N; i++)
                {
                    var v = aos[i];
                    var inv = 1f / MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
                    aosOut[i] = new Vector3F(v.X * inv, v.Y * inv, v.Z * inv);
                }
            }),
            ("SoA, скаляр (контроль раскладки)", "normalize", () =>
            {
                for (var i = 0; i < N; i++)
                {
                    float x = sx[i], y = sy[i], z = sz[i];
                    var inv = 1f / MathF.Sqrt(x * x + y * y + z * z);
                    ox[i] = x * inv; oy[i] = y * inv; oz[i] = z * inv;
                }
            }),
            ("SoA + SSE (4 тела за раз)", "normalize", () =>
            {
                for (var i = 0; i < N; i += 4)
                {
                    var x = Vector128.LoadUnsafe(ref sx[i]);
                    var y = Vector128.LoadUnsafe(ref sy[i]);
                    var z = Vector128.LoadUnsafe(ref sz[i]);
                    var inv = Vector128<float>.One / Vector128.Sqrt(x * x + y * y + z * z);
                    (x * inv).StoreUnsafe(ref ox[i]);
                    (y * inv).StoreUnsafe(ref oy[i]);
                    (z * inv).StoreUnsafe(ref oz[i]);
                }
            }),
            ("SoA + AVX (8 тел за раз)", "normalize", () =>
            {
                for (var i = 0; i < N; i += 8)
                {
                    var x = Vector256.LoadUnsafe(ref sx[i]);
                    var y = Vector256.LoadUnsafe(ref sy[i]);
                    var z = Vector256.LoadUnsafe(ref sz[i]);
                    var inv = Vector256<float>.One / Vector256.Sqrt(x * x + y * y + z * z);
                    (x * inv).StoreUnsafe(ref ox[i]);
                    (y * inv).StoreUnsafe(ref oy[i]);
                    (z * inv).StoreUnsafe(ref oz[i]);
                }
            }),
        };

        void WarmAll()
        {
            foreach (var c in contenders) for (var r = 0; r < 2_000_000 / N; r++) c.Run();
        }
        WarmAll();
        System.Threading.Thread.Sleep(500);
        WarmAll();

        const int Target = 100_000_000;
        var reps = Target / N;
        var ops = (long)reps * N;

        TestContext.Out.WriteLine($"  {"участник",-34} {"операция",-12} {"млн оп",7} {"весь цикл, мс",14} {"нс/оп",8}");
        foreach (var c in contenders)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var t = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var r = 0; r < reps; r++) c.Run();
            var ms = System.Diagnostics.Stopwatch.GetElapsedTime(t, System.Diagnostics.Stopwatch.GetTimestamp()).TotalMilliseconds;
            TestContext.Out.WriteLine(
                $"  {c.Who,-34} {c.What,-12} {ops / 1_000_000.0,7:F1} {ms,14:F2} {ms * 1_000_000 / ops,8:F2}");
        }

        // Both layouts must agree, or the fast one is fast because it is wrong.
        for (var i = 0; i < N; i++)
            Assert.That(ox[i], Is.EqualTo(aosOut[i].X).Within(1e-4f), $"SoA differs from AoS at {i}");
        TestContext.Out.WriteLine("  (обе раскладки дают одинаковый результат)");
    }

    /// <summary>The objection to SoA that has to be answered with memory, not arithmetic: if reading one object costs
    /// three cache misses instead of one, does the gather not eat the whole win?
    ///
    /// AosVsSoa above CANNOT answer it - 4096 vectors is 48 KB, it lives in L1, and memory never enters. So this one uses
    /// a MILLION bodies (64 MB as AoS) and a body struct of realistic width, and it separates the two access patterns
    /// that the objection conflates:
    ///
    ///   SWEEP - touch every body in order, using only position and velocity. AoS drags the whole 64-byte body through
    ///   the bus to use 24 bytes of it; SoA reads only the six arrays it needs, every byte of every line used.
    ///
    ///   GATHER - touch bodies in random order, one at a time. Here the objection is right: AoS has the body on one line,
    ///   SoA has to visit three.
    ///
    /// Which one dominates decides the layout, and that is a property of the SUBSYSTEM, not of SIMD.</summary>
    [Test]
    public void AosVsSoaWhenMemoryMatters()
    {
        const int Bodies = 1_000_000;

        // A body wide enough to be honest: position, velocity, orientation, force, masses. 64 bytes.
        var aos = new Body[Bodies];
        var px = new float[Bodies]; var py = new float[Bodies]; var pz = new float[Bodies];
        var vx = new float[Bodies]; var vy = new float[Bodies]; var vz = new float[Bodies];
        var order = new int[Bodies];

        var seed = 123456789u;
        for (var i = 0; i < Bodies; i++)
        {
            float x = 1 + i % 17, y = 2 + i % 13, z = 3 + i % 11;
            aos[i] = new Body { Px = x, Py = y, Pz = z, Vx = 0.5f, Vy = 0.25f, Vz = 0.125f, Mass = 1, InvMass = 1 };
            px[i] = x; py[i] = y; pz[i] = z; vx[i] = 0.5f; vy[i] = 0.25f; vz[i] = 0.125f;
            order[i] = i;
        }
        // One fixed shuffle, computed here so the timed loops contain no randomness.
        for (var i = Bodies - 1; i > 0; i--)
        {
            seed = seed * 1664525u + 1013904223u;
            var j = (int)(seed % (uint)(i + 1));
            (order[i], order[j]) = (order[j], order[i]);
        }

        const float Dt = 1f / 60f;
        float sink = 0;

        var contenders = new (string Who, string What, Action Run)[]
        {
            ("AoS (структура 64 Б)", "проход подряд", () =>
            {
                var a = aos;
                for (var i = 0; i < a.Length; i++)
                {
                    a[i].Px += a[i].Vx * Dt; a[i].Py += a[i].Vy * Dt; a[i].Pz += a[i].Vz * Dt;
                }
            }),
            ("SoA, скаляр", "проход подряд", () =>
            {
                for (var i = 0; i < Bodies; i++)
                {
                    px[i] += vx[i] * Dt; py[i] += vy[i] * Dt; pz[i] += vz[i] * Dt;
                }
            }),
            ("SoA + AVX (8 тел за раз)", "проход подряд", () =>
            {
                var dt = Vector256.Create(Dt);
                for (var i = 0; i < Bodies; i += 8)
                {
                    (Vector256.LoadUnsafe(ref px[i]) + Vector256.LoadUnsafe(ref vx[i]) * dt).StoreUnsafe(ref px[i]);
                    (Vector256.LoadUnsafe(ref py[i]) + Vector256.LoadUnsafe(ref vy[i]) * dt).StoreUnsafe(ref py[i]);
                    (Vector256.LoadUnsafe(ref pz[i]) + Vector256.LoadUnsafe(ref vz[i]) * dt).StoreUnsafe(ref pz[i]);
                }
            }),
            ("AoS", "случайный доступ", () =>
            {
                float s = 0;
                var a = aos;
                for (var i = 0; i < Bodies; i++)
                {
                    ref var b = ref a[order[i]];
                    s += b.Px + b.Py + b.Pz;
                }
                sink += s;
            }),
            ("SoA", "случайный доступ", () =>
            {
                float s = 0;
                for (var i = 0; i < Bodies; i++)
                {
                    var k = order[i];
                    s += px[k] + py[k] + pz[k];
                }
                sink += s;
            }),
        };

        foreach (var c in contenders) { c.Run(); c.Run(); }
        System.Threading.Thread.Sleep(500);
        foreach (var c in contenders) { c.Run(); c.Run(); }

        const int Reps = 40;
        var ops = (long)Reps * Bodies;

        TestContext.Out.WriteLine($"  {"участник",-28} {"схема доступа",-18} {"млн тел",8} {"весь цикл, мс",14} {"нс/тело",9}");
        foreach (var c in contenders)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var t = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var r = 0; r < Reps; r++) c.Run();
            var ms = System.Diagnostics.Stopwatch.GetElapsedTime(t, System.Diagnostics.Stopwatch.GetTimestamp()).TotalMilliseconds;
            TestContext.Out.WriteLine(
                $"  {c.Who,-28} {c.What,-18} {ops / 1_000_000.0,8:F1} {ms,14:F2} {ms * 1_000_000 / ops,9:F2}");
        }

        TestContext.Out.WriteLine($"  AoS занимает {Unsafe.SizeOf<Body>() * (long)Bodies / (1024 * 1024)} МБ, " +
            $"SoA читает за проход {6L * sizeof(float) * Bodies / (1024 * 1024)} МБ (sink {sink:E2})");
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Body
    {
        public float Px, Py, Pz;
        public float Vx, Vy, Vz;
        public float Qx, Qy, Qz, Qw;
        public float Fx, Fy, Fz;
        public float Mass, InvMass, Radius;
    }
}
