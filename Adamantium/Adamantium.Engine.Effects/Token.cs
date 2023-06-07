using System;

namespace Adamantium.Engine.Effects
{
    internal class Token
    {
        public Token() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Token" /> struct.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="value">The value.</param>
        /// <param name="span">The span.</param>
        public Token(TokenType type, string value, SourceSpan span)
        {
            Type = type;
            Value = value;
            Span = span;
        }

        /// <summary>
        /// The type of the token.
        /// </summary>
        public TokenType Type;

        /// <summary>
        /// Value of the token.
        /// </summary>
        public string Value;

        /// <summary>
        /// The source span.
        /// </summary>
        public SourceSpan Span;

        public bool EqualString(string value, bool caseSensitive = false)
        {
            return string.Compare(Value, value, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) == 0;
        }

        public bool StartsWith(string value, bool caseSensitive = false)
        {
            return Value.StartsWith(value, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return $"{{{Type}/{Value}}}";
        }
    }
}
