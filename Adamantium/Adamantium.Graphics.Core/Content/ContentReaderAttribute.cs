using System;

namespace Adamantium.Graphics.Core.Content
{
   public class ContentReaderAttribute:Attribute
   {
      public Type ContentReaderType { get; private set; }

      public ContentReaderAttribute(Type contentReaderType)
      {
         ContentReaderType = contentReaderType;
      }

   }
}
