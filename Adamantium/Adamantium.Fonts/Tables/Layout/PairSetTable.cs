namespace Adamantium.Fonts.Tables.Layout
{
    internal class PairSetTable
    {
        public PairSet[] PairSets { get; set; }

        public bool FindPairSet(ushort secondGlyphIndex, out PairSet foundPairSet)
        {
            for (int i = 0; i < PairSets.Length; ++i)
            {
                if (PairSets[i].SecondGlyph == secondGlyphIndex)
                {
                    foundPairSet = PairSets[i];
                    return true;
                }
            }

            foundPairSet = null;
            return false;
        }
    }
}