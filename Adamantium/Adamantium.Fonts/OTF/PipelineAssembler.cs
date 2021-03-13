using System.Collections.Generic;
using Adamantium.Fonts.Common;
using System.Linq;

namespace Adamantium.Fonts.OTF
{
    class PipelineAssembler
    {
        private CommandList commandList;
        private Glyph glyph;

        public PipelineAssembler(OTFParser parser)
        {
            commandList = new CommandList(parser);
        }

        public PipelineAssembler CreateGlyph(uint index)
        {
            glyph = new Glyph(index);

            return this;
        }
        
        public PipelineAssembler FillCommandList(Stack<byte> stack, bool clearMainStack = true, int index = 0)
        {
            commandList.Clear();
            commandList.Fill(stack, index);

            if (commandList.commands.Last().@operator != OperatorsType.endchar)
            {
                throw new CommandException("Not ending with EndChar command");
            }

            if (clearMainStack)
                stack.Clear();

            return this;
        }

        public PipelineAssembler FillOutlines()
        {
            glyph.OutlineList.Fill(commandList.commands);

            return this;
        }

        public PipelineAssembler PrepareSegments()
        {
            glyph.OutlineList.SplitToSegments();

            return this;
        }
        
        public PipelineAssembler Sample(uint rate)
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
