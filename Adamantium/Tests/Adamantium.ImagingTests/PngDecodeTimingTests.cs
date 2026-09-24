using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Adamantium.Imaging;
using NUnit.Framework;

namespace Adamantium.ImagingTests;

/// <summary>
/// Real textures decode to the same pixels GDI+ gives (the hashes were taken from it), and how long that takes.
/// </summary>
[TestFixture]
public class PngDecodeTimingTests
{
    private static readonly Dictionary<string, string> Expected = new()
    {
        ["Body 1.png"] = "7FC5FE1D81D8CF7F078B8369260F76D5FD323360B562D7E13E5D34EF6E7EB248",
        ["Body 2.png"] = "ED40CEF91C67BDE3B1A3097CAC082FFFFDEA3615619E944F38352126D65BA90F",
        ["Body 3.png"] = "36CE5CCA00ADF59C200FD8AF31EDB029C62D3051CFF3E86F09877CD22D451959",
        ["Body 4.png"] = "8DA9F095D6B2D7CB1C834D4042ADDC1C7C259CC086BD261A003734CB7B09EEEA",
        ["Body 5.png"] = "C446E71F7196BBC363ECABC7F5CFA9DC2EE29591878928D410AC9390C4EE9F07",
        ["Body 6.png"] = "ABED0846B8A1D3B4C2A6159AFB088B8828AD242B734E5DD235F38F5478C12345",
        ["Glass.png"] = "E6E766C54FE4337CCFCCAB4F28F2010DA0EB259E268A71904DEA7FA90A75F6F5",
        ["intpanel.png"] = "37C2BFBB0ABC28DAB9FA899A977F39309247680DE2FCB961B0E71420E5B648EE",
        ["panel.png"] = "F543E7BE77FFE4CEBA52993DD9D0AD431D0C7647B1A0528079342E4E5CBB7272",
        ["panel1.png"] = "5CDDAEE3999942A390015B023BBAE5ABB82DE8DC397F116A9D0C97864BDCA368",
        ["panel2.png"] = "5F932F2F15B60716D158D0506832DC8DB164E9D7D28D641E65D6E1901EC125A1",
        ["Parts 1.png"] = "E857168AE804D718597EA720DBE3363BEBFCED291C683FD07141DE0F0CC739DF",
        ["pilot.png"] = "645EDA1992CB740AC5AEC41267A4DD1E6F99CDE0D02022CE191D59DF642ECABB",
        ["weapons.png"] = "B11F635FA7F77F84361E20FDAE5300362F64E246AFD8899B72FC7369A906D97A",
    };

    private static string TexturesFolder()
    {
        for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Adamantium.UI.Sandbox", "Models", "F15C");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Adamantium.UI.Sandbox/Models/F15C");
    }

    [Test]
    public void RealTextures_DecodeToTheSamePixels()
    {
        var folder = TexturesFolder();
        var names = Expected.Keys.OrderBy(n => n).ToArray();
        var data = names.Select(n => File.ReadAllBytes(Path.Combine(folder, n))).ToArray();

        // Warm-up, so the JIT is not in the numbers.
        BitmapLoader.Load(new MemoryStream(data[0])).GetRawPixels(0);

        const int rounds = 3;
        double loadMs = 0, decodeMs = 0;
        for (var i = 0; i < names.Length; i++)
        {
            byte[] pixels = null;
            for (var r = 0; r < rounds; r++)
            {
                var watch = Stopwatch.StartNew();
                var bitmap = BitmapLoader.Load(new MemoryStream(data[i]));
                loadMs += watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                pixels = bitmap.GetRawPixels(0);
                decodeMs += watch.Elapsed.TotalMilliseconds;
            }

            Assert.That(Convert.ToHexString(SHA256.HashData(pixels)), Is.EqualTo(Expected[names[i]]), names[i]);
        }

        TestContext.Out.WriteLine($"{(loadMs + decodeMs) / rounds:F1} ms per pass: load {loadMs / rounds:F1}, decode {decodeMs / rounds:F1}");
    }
}
