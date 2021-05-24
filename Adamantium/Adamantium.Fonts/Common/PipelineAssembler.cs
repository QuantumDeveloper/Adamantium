using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts.Exceptions;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Parsers.CFF;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Common
{
    class PipelineAssembler
    {
        private CommandList commandList;
        private Glyph glyph;
        private CFFVersion cffVersion;

        public PipelineAssembler(ICFFParser parser, CFFVersion cffVersion = CFFVersion.CFF)
        {
            commandList = new CommandList(parser);
            this.cffVersion = cffVersion;
        }

        public PipelineAssembler CreateGlyph(uint index)
        {
            glyph = new Glyph(index);
            glyph.OutlineType = OutlineType.CompactFontFormat;
            return this;
        }
        
        public PipelineAssembler FillCommandList(
            CFFFont font, 
            Stack<byte> stack, 
            FontDict fontDict, 
            bool clearMainStack = true, 
            int index = 0)
        {
            commandList.Clear();
            commandList.Fill(font, stack, fontDict, index);

            if (cffVersion == CFFVersion.CFF &&
                commandList.commands.Count > 0 &&
                commandList.commands.Last().Operator != OperatorsType.endchar)
            {
                throw new CommandException($"Glyph {index} is not ending with EndChar command");
            }

            if (index == 14)
            {
                
            }

            if (clearMainStack)
                stack.Clear();

            return this;
        }

        public PipelineAssembler FillOutlines()
        {
            glyph.FillOutlines(commandList.commands);

            return this;
        }

        public PipelineAssembler Sample(byte rate)
        {
            glyph.Sample(rate);
            
            return this;
        }

        public Glyph GetGlyph()
        {
            return glyph;
        }
    }
}
