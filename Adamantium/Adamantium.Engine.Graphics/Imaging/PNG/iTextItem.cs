using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public class ITextItem
    {
        /*the English keyword of the text chunk (e.g. "Comment")*/
        public string Key { get; set; }

        /*language tag for this text's language, ISO/IEC 646 string, e.g. ISO 639 language tag*/
        public string LangTag { get; set; }

        /*keyword translated to the international language - UTF-8 string*/
        public string TransKey { get; set; }

        /*the actual international text - UTF-8 string*/
        public string Text { get; set; }
    }
}
