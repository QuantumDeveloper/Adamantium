using System.Collections.Generic;
using Adamantium.Fonts.Common;
using System.Linq;

namespace Adamantium.Fonts.OTF
{
    class PipelineAssembler
    {
        private CommandList commandList;
        private OutlineList outlineList;

        public PipelineAssembler(OTFParser parser)
        {
            commandList = new CommandList(parser);
            outlineList = new OutlineList();
        }

        //Glyph g = CommandList(mainStack).OutlineList().BezierSampling(int sampleRate);

        public PipelineAssembler GetCommandList(Stack<byte> stack, bool clearMainStack = true)
        {
            commandList.Clear();
            commandList.Fill(stack);

            if (commandList.commands.Last().@operator != OperatorsType.endchar)
            {
                throw new CommandException("Not ending with EndChar command");
            }

            if (clearMainStack)
                stack.Clear();

            return this;
        }

        public PipelineAssembler GetOutlines()
        {
            outlineList.Clear();
            outlineList.Fill(commandList.commands);

            return this;
        }

        public PipelineAssembler Sample(int rate)
        {
            outlineList.Sample(rate);
            
            return this;
        }

        public Glyph Build()
        {
            return new Glyph();
        }
    }
}
