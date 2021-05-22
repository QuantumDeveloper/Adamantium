using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Parsers.CFF;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Extensions
{
    internal static class OutlineExtension
    {
        public static void FillOutlines(this Glyph glyph, List<Command> commands)
        {
            var interpreter = new CommandInterpreter();
            Outline outline = null;

            foreach (var command in commands)
            {
                if (command.IsNewOutline())
                {
                    outline = new Outline();
                    glyph.AddOutline(outline);
                }

                var pts = interpreter.GetOutlinePoints(command);
                outline?.Points.AddRange(pts);
            }
        }
        
        private static bool IsNewOutline(this Command command)
        {
            switch (command.Operator)
            {
                case OperatorsType.rmoveto:
                case OperatorsType.hmoveto:
                case OperatorsType.vmoveto:
                    return true;
                default:
                    return false;
            }
        }
    }
}