using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Adamantium.EffectsCompiler
{
    internal class Tokenizer
    {
        private static readonly Regex RegexTokenizer = new Regex(
            @"(?<ws>[ \t]+)|" +
            @"(?<nl>(?:\r\n|\n))|" +
            @"(?<ident>[a-zA-Z_][a-zA-Z0-9_]*)|" +
            @"(?<hexa>0x[0-9a-fA-F]+)|" +
            @"(?<number>[\-\+]?\s*[0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?f?)|" +
            @"(?<equal>=)|" +
            @"(?<comma>,)|" +
            @"(?<semicolon>;)|" +
            @"(?<lcb>\{)|" +
            @"(?<rcb>\})|" +
            @"(?<lpar>\()|" +
            @"(?<rpar>\))|" +
            @"(?<lb>\[)|" +
            @"(?<rb>\])|" +
            @"(?<str>""[^""\\]*(?:\\.[^""\\]*)*"")|" +
            @"(?<prep>#)|" +
            @"(?<doublecolon>::)|" +
            @"(?<dot>\.)|" +
            @"(?<lt>\<)|" +
            @"(?<gt>\>)|" +
            @"(?<increment>\++)|" +
            @"(?<decrement>\--)|" +
            @"(?<plus>\+)|" +
            @"(?<minus>\-)|" +
            @"(?<multiply>\*)|" +
            // NOT a bare slash: it must not swallow the first character of a comment, or the comment rule below never
            // gets a turn and a comment is tokenised as ordinary code. That is not cosmetic - the parser acts on a `#`
            // wherever it finds one, so an #include written inside a COMMENT was obeyed. A file whose comment quoted its
            // own include line therefore included itself, forever, and the build died with a stack overflow in the
            // include parser rather than anything that names the file.
            @"(?<divide>\/(?![\/\*]))|" +
            // A whole comment, line or block, as ONE token the parser skips (see EffectParser.InternalNextToken). Block
            // comments are lazy so `/* a */ b /* c */` is two comments and not one that eats `b`.
            @"(?<comment>\/\/[^\r\n]*|\/\*[\s\S]*?\*\/)|" +
            // ONE character, not a run. The operators this grammar has no rule for - ! % & | ^ ~ ? : and every
            // non-ASCII character a comment may contain - land here, and a greedy run swallowed everything up to the
            // next space along with them: `!isEnd)` ate its own closing parenthesis, so the bracket counter never
            // balanced and the whole effect failed to parse with a position pointing at innocent code.
            @"(?<unk>[^\s])",
            RegexOptions.Compiled
            );

        /// <summary>
        /// Runs the tokenizer on an input string.
        /// </summary>
        /// <param name="input">The string to decode to tokens.</param>
        /// <returns>An enumeration of tokens.</returns>
        public static IEnumerable<Token> Run(string input)
        {
            var matches = RegexTokenizer.Matches(input);
            foreach (Match match in matches)
            {
                int i = 0;
                foreach (Group group in match.Groups)
                {
                    string matchValue = group.Value;
                    // Skip whitespaces
                    if (group.Success && i > 1)
                    {
                        yield return new Token { Type = (TokenType)(i - 2), Value = matchValue, Span = { StartIndex = @group.Index, Length = @group.Length } };
                    }
                    i++;
                }
            }
        }
    }
}
