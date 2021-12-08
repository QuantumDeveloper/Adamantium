using Adamantium.Fonts.Extensions;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class ScriptTable
    {
        public uint Tag { get; }
        
        public string Name { get; }
        
        public LangSysTable DefaultLang { get; set; }
        
        public LangSysTable[] LangSysTables { get; set; }

        public ScriptTable(uint tag)
        {
            Tag = tag;
            Name = tag.GetString();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}