using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Fonts;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.FontTests
{
    public class OtfTests
    {
        internal static class CFF1Fonts
        {
            public static string Glametrix;

            public static string Quicksand_Regular;

            public static string SourceSans3_Regular;
        }
        
        internal static class CFF2Fonts
        {
            public static string AdobeVFPrototype;

            public static string SourceHanSerifVFProtoJP;
        }
        
        internal static class FontCollections
        {
            public static string Ttf_Asana;

            public static string Cff1_NotoSansCJK_Regular;
        }
        
        
        static OtfTests()
        {
            CFF1Fonts.Glametrix = Path.Combine("OTFFonts", "CFF", "Glametrix-oj9A.otf");
            CFF1Fonts.Quicksand_Regular = Path.Combine("OTFFonts", "CFF", "Quicksand-Regular.otf");
            CFF1Fonts.SourceSans3_Regular = Path.Combine("OTFFonts", "SourceSans3-Regular.otf");
            
            CFF2Fonts.AdobeVFPrototype = Path.Combine("OTFFonts", "CFF2", "AdobeVFPrototype.otf");
            CFF2Fonts.SourceHanSerifVFProtoJP = Path.Combine("OTFFonts", "CFF2", "SourceHanSerifVFProtoJP.otf");
            
            FontCollections.Ttf_Asana = Path.Combine("OTFFonts", "FontCollections", "ASANA.TTC");
            FontCollections.Cff1_NotoSansCJK_Regular = Path.Combine("OTFFonts", "FontCollections", "NotoSansCJK-Regular.ttc");
        }
        
        [Test]
        public void LoadOtfCff1Font_Glametrix()
        {
            var typeFace = TypeFace.LoadFont(CFF1Fonts.Glametrix, 2);
        }
        
        [Test]
        public void LoadOtfCff1Font_SourceSans3()
        {
            var typeFace = TypeFace.LoadFont(CFF1Fonts.SourceSans3_Regular, 2);
        }
        
        [Test]
        public void LoadOtfCff1Font_Quicksand_Regular()
        {
            var typeFace = TypeFace.LoadFont(CFF1Fonts.Quicksand_Regular, 2);
        }
        
        [Test]
        public void LoadOtfCff2Font_AdobeVPPrototype()
        {
            var typeFace = TypeFace.LoadFont(CFF2Fonts.AdobeVFPrototype, 2);
        }
        
        [Test]
        public void LoadOtfCff2Font_SourceHanSerifVFProtoJP()
        {
            var typeFace = TypeFace.LoadFont(CFF2Fonts.SourceHanSerifVFProtoJP, 2);
        }
        
        [Test]
        public void LoadOtfCff1FontCollection_NotoSansCJK_Regular()
        {
            var typeFace = TypeFace.LoadFont(FontCollections.Cff1_NotoSansCJK_Regular, 2);
        }
        
        [Test]
        public void LoadOtfCff1FontCollection_Asana()
        {
            var typeFace = TypeFace.LoadFont(FontCollections.Ttf_Asana, 2);
        }
        
        [Test]
        public void OutputFeatures_SourceSans3_Regular()
        {
            var typeFace = TypeFace.LoadFont(CFF1Fonts.SourceSans3_Regular, 2);
            foreach (var font in typeFace.Fonts)
            {
                font.FeatureService.EnableFeature("kern", true);
                foreach (var language in font.FeatureService.AvailableLanguages)
                {
                    Debug.WriteLine($"Lang: {language}");
                    Debug.WriteLine("GPOS:");
                    foreach (var gposFeature in language.GPOSFeatures)
                    {
                        Debug.WriteLine(gposFeature);
                    }
                    
                    Debug.WriteLine("GSUB:");
                    foreach (var gsubFeature in language.GSUBFeatures)
                    {
                        Debug.WriteLine(gsubFeature);
                    }
                }
            }
        }

        [Test]
        public async Task FontManager_Test()
        {
            var fontManager = await FontService.LoadTypeFaceAsync(CFF1Fonts.SourceSans3_Regular);
            var font = fontManager.GetTypeFace(0).GetFont(0);
            font.FeatureService.EnableFeature(FeatureNames.kern, true);
            var layoutContainer = new GlyphLayoutContainer(fontManager.GetTypeFace(0));
            var demoString = "rw";
            var glyphs = font.TranslateIntoGlyphs(demoString);
            layoutContainer.AddGlyphs(glyphs);

            var result = font.FeatureService.ApplyFeature(FeatureNames.kern, layoutContainer, 0, (uint)glyphs.Length);
            
        }
    }
}