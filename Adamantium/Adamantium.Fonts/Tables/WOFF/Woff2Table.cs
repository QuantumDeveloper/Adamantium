using System;

namespace Adamantium.Fonts.Tables.WOFF
{
    internal class Woff2Table
    {
        public string Name { get; set; }
        
        public byte PreprocessingTransform { get; set; }
        
        public UInt32 OrigLength { get; set; }
        
        public UInt32 TransformLength { get; set; }
        
        public Int64 ExpectedOffset { get; set; }

        public override string ToString()
        {
            return $"{Name} Offset - {ExpectedOffset} - Transform: {PreprocessingTransform}";
        }
    }
}