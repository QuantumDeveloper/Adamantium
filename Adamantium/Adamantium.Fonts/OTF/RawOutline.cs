using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public struct RawOutline
    {
        public uint character; // @TODO UNICODE / AFII / ETC. REPRESENTATION
        public CharacterEncoding characterEncoding;
        public List<Command> rawCommands;

        public override string ToString()
        {
            return $"character {character} <{string.Join(" -> ", rawCommands.ToArray())}>";
        }
    }
}
