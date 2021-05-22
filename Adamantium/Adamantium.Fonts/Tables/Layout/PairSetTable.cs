namespace Adamantium.Fonts.Tables.Layout
{
    internal class PairSetTable
    {
        public PairSet[] PairSets { get; set; }

        public bool FindPairSet(ushort sendGlyphIndex, out PairSet foundPairSet)
        {
            for (int i = 0; i < PairSets.Length; ++i)
            {
                if (PairSets[i].SecondGlyph == sendGlyphIndex)
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