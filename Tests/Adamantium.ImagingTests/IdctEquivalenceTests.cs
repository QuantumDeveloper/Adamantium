using System;
using System.Reflection;
using NUnit.Framework;

namespace Adamantium.ImagingTests;

/// <summary>
/// The fast inverse DCT against the direct one, block for block.
///
/// <para>The round-trip test next door proves a picture survives the codec; it cannot prove the transform is RIGHT,
/// because JPEG is lossy and a slightly wrong transform still produces a picture that looks fine. This one compares the
/// factorised transform against the definition it replaced, on the same coefficients - the only way to tell "faster"
/// apart from "quietly different".</para>
///
/// <para>Both are private to the codec, so they are reached by reflection rather than by widening the API for a test.</para>
/// </summary>
[TestFixture]
public class IdctEquivalenceTests
{
    private static readonly Type Dct = typeof(Adamantium.Imaging.ImageDescription).Assembly
        .GetType("Adamantium.Imaging.Jpeg.DCT", throwOnError: true);

    // The fast transform writes 64 bytes into a caller's buffer at an offset - blocks are stored end to end rather than
    // as an object each. A non-zero offset is passed on purpose: it is how the decoder calls it.
    private static byte[] Fast(float[] coefficients)
    {
        var instance = Activator.CreateInstance(Dct, nonPublic: true);
        var method = Dct.GetMethod("FastIDCT", BindingFlags.Instance | BindingFlags.NonPublic);

        var buffer = new byte[192];
        method!.Invoke(instance, new object[] { coefficients, buffer, 64 });

        var block = new byte[64];
        Array.Copy(buffer, 64, block, 0, 64);
        return block;
    }

    private static byte[,] Reference(float[] coefficients)
    {
        var method = Dct.GetMethod("ReferenceIDCT", BindingFlags.Static | BindingFlags.NonPublic);
        var output = new byte[8, 8];
        method!.Invoke(null, new object[] { coefficients, new float[64], output });
        return output;
    }

    private static void AssertSame(float[] coefficients, string what)
    {
        var fast = Fast((float[])coefficients.Clone());
        var reference = Reference((float[])coefficients.Clone());

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                // One level of tolerance: the two accumulate their sums in a different order, so a value landing exactly
                // on .5 can round either way. Anything beyond that is a different transform, not a different rounding.
                Assert.That(Math.Abs(fast[y * 8 + x] - reference[y, x]), Is.LessThanOrEqualTo(1),
                    $"{what}: at [{y},{x}] fast={fast[y * 8 + x]} reference={reference[y, x]}");
            }
        }
    }

    [Test]
    public void FlatBlock_IsTheSame()
    {
        // Only a DC term - the case the fast path short-circuits, and the one most blocks in a photograph actually are.
        var coefficients = new float[64];
        coefficients[0] = 512f;
        AssertSame(coefficients, "DC only");
    }

    [Test]
    public void SingleFrequencies_AreTheSame()
    {
        // Every basis function on its own: if the two disagree about one frequency, this says which.
        for (var i = 0; i < 64; i++)
        {
            var coefficients = new float[64];
            coefficients[i] = 300f;
            AssertSame(coefficients, $"basis function {i}");
        }
    }

    [Test]
    public void RandomBlocks_AreTheSame()
    {
        var random = new Random(20260830);
        for (var block = 0; block < 200; block++)
        {
            var coefficients = new float[64];
            coefficients[0] = (float)(random.NextDouble() * 2000 - 1000);
            // Falling magnitude with frequency, as a real photograph's blocks are - a uniformly random block is a
            // picture nobody ever decodes, and would let a mistake in the high frequencies hide behind clamping.
            for (var i = 1; i < 64; i++)
            {
                coefficients[i] = (float)((random.NextDouble() * 2 - 1) * 400 / (1 + i));
            }

            AssertSame(coefficients, $"random block {block}");
        }
    }

    [Test]
    public void ExtremeBlock_ClampsTheSameWay()
    {
        // Values far past what eight bits can hold, so both have to clamp - and clamp identically.
        var coefficients = new float[64];
        for (var i = 0; i < 64; i++) coefficients[i] = i % 2 == 0 ? 4000f : -4000f;
        AssertSame(coefficients, "saturating block");
    }
}
