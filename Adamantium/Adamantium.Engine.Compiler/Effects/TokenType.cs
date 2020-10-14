namespace Adamantium.Engine.Compiler.Effects
{
   /// <summary>
   /// Type of a token language.
   /// </summary>
   internal enum TokenType
   {
      /// <summary>
      /// A Newline.
      /// </summary>
      Newline,

      /// <summary>
      /// An identifier.
      /// </summary>
      Identifier,

      /// <summary>
      /// A number in hexadecimal form.
      /// </summary>
      Hexa,

      /// <summary>
      /// A number.
      /// </summary>
      Number,

      /// <summary>
      /// The symbol '='.
      /// </summary>
      Equal,

      /// <summary>
      /// A comma ','.
      /// </summary>
      Comma,

      /// <summary>
      /// A Semicolon ';'.
      /// </summary>
      SemiColon,

      /// <summary>
      /// A left curly brace '{'.
      /// </summary>
      LeftCurlyBrace,

      /// <summary>
      /// A right curly brace '}'.
      /// </summary>
      RightCurlyBrace,

      /// <summary>
      /// A left parenthesis '('.
      /// </summary>
      LeftParent,

      /// <summary>
      /// A right parenthesis ')'.
      /// </summary>
      RightParent,

      /// <summary>
      /// A left bracket '['.
      /// </summary>
      LeftBracket,

      /// <summary>
      /// A right bracket ']'.
      /// </summary>
      RightBracket,

      /// <summary>
      /// A string.
      /// </summary>
      String,

      /// <summary>
      /// A preprocessor token '#'
      /// </summary>
      Preprocessor,

      /// <summary>
      /// A double colon '::'.
      /// </summary>
      DoubleColon,

      /// <summary>
      /// A dot '.'.
      /// </summary>
      Dot,

      /// <summary>
      /// A '&lt;'.
      /// </summary>
      LessThan,

      /// <summary>
      /// A '&gt;'.
      /// </summary>
      GreaterThan,

      /// <summary>
      /// An unknown symbol.
      /// </summary>
      Unknown,

      /// <summary>
      /// A end of file token.
      /// </summary>
      EndOfFile,
   }
}
