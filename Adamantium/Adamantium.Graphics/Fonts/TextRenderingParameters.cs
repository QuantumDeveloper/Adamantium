using System;
using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts
{
    public class TextRenderingParameters : IEquatable<TextRenderingParameters>
    {
        /// <summary>
        /// Defines text area inside render target
        /// </summary>
        public Rectangle TextArea { get; set; }

        public TextWrapping TextWrapping { get; set; }

        public HorizontalTextAlignment HorizontalTextAlignment { get; set; }
        
        public VerticalTextAlignment VerticalTextAlignment { get; set; }
        public TextTrimming TextTrimming { get; set; }
        
        public Color Color { get; set; }

        public bool Equals(TextRenderingParameters other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return TextArea.Equals(other.TextArea) && 
                   TextWrapping == other.TextWrapping &&
                   HorizontalTextAlignment == other.HorizontalTextAlignment && 
                   VerticalTextAlignment == other.VerticalTextAlignment && 
                   TextTrimming == other.TextTrimming &&
                   Color.Equals(other.Color);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TextRenderingParameters)obj);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(TextArea, (int)TextWrapping, (int)HorizontalTextAlignment, (int)VerticalTextAlignment, (int)TextTrimming, Color);
        }
        
        public static bool operator ==(TextRenderingParameters @paramA, TextRenderingParameters @paramB)
        {
            return paramA.Equals(@paramB);
        }

        public static bool operator !=(TextRenderingParameters paramA, TextRenderingParameters paramB)
        {
            return !@paramA.Equals(@paramB);
        }
    }
}
