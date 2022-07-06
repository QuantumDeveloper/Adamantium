using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Adamantium.Engine.Effects
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
            @"(?<unk>[^\s]+)",
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
