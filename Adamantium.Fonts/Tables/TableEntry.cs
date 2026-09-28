using System;

namespace Adamantium.Fonts.Tables
{
    internal class TableEntry
    {
        /// <summary>
        /// integer tag
        /// </summary>
        public UInt32 Tag { get; set; } 
        
        /// <summary>
        /// readable tag - table name
        /// </summary>
        public String Name { get; set; }
        
        /// <summary>
        /// Check sum
        /// </summary>
        public UInt32 CheckSum { get; set; }
        
        /// <summary>
        /// offset from beginning of file
        /// </summary>
        public Int64 Offset { get; set; } 
        
        /// <summary>
        /// length of the table in bytes
        /// </summary>
        public UInt32 Length { get; set; }
        
        public byte PreprocessingTransform { get; set; }

        public override string ToString()
        {
            return $"{Name} : {Offset}";
        }
    };
}
