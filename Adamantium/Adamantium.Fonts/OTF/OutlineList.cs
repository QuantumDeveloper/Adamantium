using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    class OutlineList
    {
        private CommandInterpreter interpreter;
        internal List<Outline> outlines;

        public OutlineList()
        {
            interpreter = new CommandInterpreter();
            outlines = new List<Outline>();
        }

        public void Fill(List<Command> commands)
        {
            Outline outline = null;

            foreach (var command in commands)
            {
                if (IsNewOutline(command))
                {
                    outline = new Outline();
                    outlines.Add(outline);
                }

                var pts = interpreter.GetPoints(command);
                outline?.Points.AddRange(pts);
            }
        }

        public void Clear()
        {
            outlines?.Clear();
        }

        private bool IsNewOutline(Command command)
        {
            switch (command.@operator)
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
