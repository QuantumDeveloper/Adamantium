using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
    public class GlyphLayoutContainer : IGlyphPositioning, IGlyphSubstitutions
    {
        private readonly List<Glyph> displayedGlyphs;
        private Dictionary<uint, FeatureCache> glyphCacheMap;
        private TypeFace typeFace;
        private int unprocessedGlyphs;

        public bool IsProcessingDone => unprocessedGlyphs == 0;
        
        public GlyphLayoutContainer(TypeFace typeFace)
        {
            this.typeFace = typeFace;
            displayedGlyphs = new List<Glyph>();
            glyphCacheMap = new Dictionary<uint, FeatureCache>();
        }

        public void AddGlyphs(params Glyph[] glyphs)
        {
            displayedGlyphs.AddRange(glyphs);
        }

        public void ReplaceGlyph(int index, Glyph glyph)
        {
            displayedGlyphs[index] = glyph;
        }

        public void InsertGlyph(int index, Glyph glyph)
        {
            displayedGlyphs.Insert(index, glyph);
        }

        public uint Count => (uint)displayedGlyphs.Count;

        public Glyph GetGlyph(int index)
        {
            return displayedGlyphs[index];
        }
        
        public uint GetGlyphIndex(uint index)
        {
            return displayedGlyphs[(int)index].Index;
        }

        public void Replace(FeatureInfo featureInfo, uint glyphIndex, uint substitutionGlyphIndex)
        {
            if (!glyphCacheMap.TryGetValue(glyphIndex, out var featureCache))
            {
                featureCache = new FeatureCache(glyphIndex);
                glyphCacheMap[glyphIndex] = featureCache;
            }

            if (typeFace.GetGlyphByIndex(substitutionGlyphIndex, out var glyph))
            {
                displayedGlyphs.RemoveAt((int)glyphIndex);
                displayedGlyphs.Insert((int)glyphIndex, glyph); 
            }

            unprocessedGlyphs--;
            
            featureCache.AddToFeatureCache(featureInfo, substitutionGlyphIndex);
        }

        public void Replace(FeatureInfo featureInfo, uint glyphIndex, uint[] substitutionArray)
        {
            if (!glyphCacheMap.TryGetValue(glyphIndex, out var featureCache))
            {
                featureCache = new FeatureCache(glyphIndex);
                glyphCacheMap[glyphIndex] = featureCache;
            }

            unprocessedGlyphs--;
            
            featureCache.AddToFeatureCache(featureInfo, substitutionArray);
        }

        public void Replace(FeatureInfo featureInfo, uint glyphIndex, ushort[] substitutionArray)
        {
            if (!glyphCacheMap.TryGetValue(glyphIndex, out var featureCache))
            {
                featureCache = new FeatureCache(glyphIndex);
                glyphCacheMap[glyphIndex] = featureCache;
            }

            if (typeFace.GetGlyphByIndex(substitutionArray[0], out var glyph))
            {
                displayedGlyphs.RemoveAt((int)glyphIndex);
                displayedGlyphs.Insert((int)glyphIndex, glyph); 
            }
            
            unprocessedGlyphs--;
            
            featureCache.AddToFeatureCache(featureInfo, substitutionArray);
        }

        public void Replace(FeatureInfo featureInfo, uint glyphIndex, int removeLength, ushort newGlyphIndex)
        {
            if (!glyphCacheMap.TryGetValue(glyphIndex, out var featureCache))
            {
                featureCache = new FeatureCache(glyphIndex);
                glyphCacheMap[glyphIndex] = featureCache;
            }

            // many to one replacement (e.g. ligatures)
            if (typeFace.GetGlyphByIndex(newGlyphIndex, out var glyph))
            {
                displayedGlyphs.RemoveRange((int)glyphIndex, removeLength);
                displayedGlyphs.Insert((int)glyphIndex, glyph); 
            }

            unprocessedGlyphs -= removeLength;
            
            featureCache.AddToFeatureCache(featureInfo, newGlyphIndex);
            //TODO: need to check does this logic is correct
        }

        public GlyphClassDefinition GetGlyphClassDefinition(uint index)
        {
            throw new System.NotImplementedException();
        }

        public void AppendGlyphOffset(FeatureInfo featureInfo, uint glyphIndex, Vector2F offset)
        {
            if (!glyphCacheMap.TryGetValue(glyphIndex, out var featureCache))
            {
                featureCache = new FeatureCache(glyphIndex);
                glyphCacheMap[glyphIndex] = featureCache;
            }

            featureCache.AddToFeatureCache(featureInfo, GlyphPosition.FromOffset(offset));
        }

        public void AppendGlyphAdvance(FeatureInfo featureInfo, uint glyphIndex, Vector2F advance)
        {
            if (!glyphCacheMap.TryGetValue(glyphIndex, out var featureCache))
            {
                featureCache = new FeatureCache(glyphIndex);
                glyphCacheMap[glyphIndex] = featureCache;
            }
            
            featureCache.AddToFeatureCache(featureInfo, GlyphPosition.FromAdvance(advance));
        }

        public Vector2F GetOffset(uint glyphIndex)
        {
            glyphCacheMap.TryGetValue(glyphIndex, out var featureCache);
            return featureCache != null ? featureCache.Layout.Position.Offset : new Vector2F(0, 0);
        }

        public Vector2F GetAdvance(uint glyphIndex)
        {
            glyphCacheMap.TryGetValue(glyphIndex, out var featureCache);
            return featureCache != null ? featureCache.Layout.Position.Advance : new Vector2F(0, 0);
        }

        public void NewProcessingStart()
        {
            unprocessedGlyphs = displayedGlyphs.Count;
        }
    }
}