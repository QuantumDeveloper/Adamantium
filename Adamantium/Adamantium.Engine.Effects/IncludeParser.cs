using System.Text;

namespace Adamantium.Engine.Effects;

internal class IncludeParser : EffectParser
{
    public override EffectParserResult Parse(string input, string fileName)
    {
        return ParseInput(PrepareParsing(input, fileName));
    }

    private EffectParserResult ParseInput(EffectParserResult previousParsing)
    {
        // Reset count
        parentCount = 0;
        curlyBraceCount = 0;
        bracketCount = 0;

        result = previousParsing;
        newPreprocessedSource = new StringBuilder(result.PreprocessedSource);

        tokenEnumerator = Tokenizer.Run(previousParsing.PreprocessedSource).GetEnumerator();

        do
        {
            var token = NextToken();
            if (token.Type == TokenType.EndOfFile)
                break;

        } while (true);

        result.PreprocessedSource = newPreprocessedSource.ToString().TrimStart(' ');

        return result;
    }

    protected override Token NextToken()
    {
        var token = InternalNextToken();

        while (token.Type == TokenType.Preprocessor)
        {
            InternalNextToken();
            if (Expect("include"))
            {
                var prevToken = token.Value + currentToken.Value;
                token = InternalNextToken();
                var includePath = token.Value.Replace("\"", "").Replace('/', '\\');

                foreach (var include in Includes)
                {
                    if (!include.Path.EndsWith(includePath)) continue;

                    var includeParser = new IncludeParser() { Logger = Logger, Includes = Includes };
                    var parseResult = includeParser.Parse(include.Content, include.Path);
                    newPreprocessedSource =
                        newPreprocessedSource.Replace(prevToken, "").Replace(token.Value, parseResult.PreprocessedSource);
                    break;
                }
            }
            else
            {
                Logger.Error("Unsupported preprocessor token [{0}].", token.Span,
                    currentToken.Value);
                return currentToken;
            }
        }

        // Set correct location for current token
        currentToken.Span.Line = currentLine;
        currentToken.Span.Column = token.Span.StartIndex - currentLineAbsolutePos + 1;
        currentToken.Span.FilePath = currentFile;

        return currentToken;
    }
}