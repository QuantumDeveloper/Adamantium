using System;

namespace Adamantium.Fonts.Tables.Layout
{
    public class FeatureParametersTable
    {
        /// <summary>
        /// Format number is set to 0.
        /// </summary>
        /// <returns></returns>
        public UInt16 Format { get; set; }
        
        /// <summary>
        /// The 'name' table name ID that specifies a string (or strings, for multiple languages) for a user-interface label for this feature. (May be NULL.)
        /// </summary>
        public UInt16 FeatUiLabelNameId { get; set; }
        
        /// <summary>
        /// The 'name' table name ID that specifies a string (or strings, for multiple languages) that an application can use for tooltip text for this feature. (May be NULL.)
        /// </summary>
        public UInt16 FeatUiTooltipTextNameId { get; set; }
        
        /// <summary>
        /// The 'name' table name ID that specifies sample text that illustrates the effect of this feature. (May be NULL.)
        /// </summary>
        /// <returns></returns>
        public UInt16 SampleTextNameId { get; set; }
        
        /// <summary>
        /// Number of named parameters. (May be zero.)
        /// </summary>
        /// <returns></returns>
        public UInt16 NumNamedParameters { get; set; }
        
        /// <summary>
        /// The first 'name' table name ID used to specify strings for user-interface labels for the feature parameters. (Must be zero if numParameters is zero.)
        /// </summary>
        public UInt16 FirstParamUiLabelNameId { get; set; }
        
        /// <summary>
        /// The count of characters for which this feature provides glyph variants. (May be zero.)
        /// </summary>
        /// <returns></returns>
        public UInt16 CharCount { get; set; }
        
        /// <summary>
        /// The Unicode Scalar Value of the characters for which this feature provides glyph variants.
        /// uint24
        /// </summary>
        /// <returns></returns>
        public UInt32[] Character { get; set; }
    }
}