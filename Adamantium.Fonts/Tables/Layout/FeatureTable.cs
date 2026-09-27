using Adamantium.Fonts.Extensions;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class FeatureTable
    {
        public FeatureTable(uint tag)
        {
            Tag = tag;
            Name = tag.GetString();
        }
        
        public FeatureParametersTable FeatureParameters { get; set; }
        
        public uint Tag { get; }
        
        public string Name { get; }
        
        public ushort[] LookupListIndices { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}