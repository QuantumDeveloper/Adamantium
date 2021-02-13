using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    class OutlineList
    {
        private CommandInterpreter interpreter;
        internal List<Outline> originalOutlines;

        public List<Outline> sampledOutlines;

        public OutlineList()
        {
            interpreter = new CommandInterpreter();
            originalOutlines = new List<Outline>();
        }

        public void Fill(List<Command> commands)
        {
            Outline outline = null;

            foreach (var command in commands)
            {
                if (IsNewOutline(command))
                {
                    outline = new Outline();
                    originalOutlines.Add(outline);
                }

                var pts = interpreter.GetPoints(command);
                outline?.Points.AddRange(pts);
            }
        }

        public void Sample(int resolution)
        {
            BezierSampler.SampleOutlines(originalOutlines.ToArray(), resolution);
        }

        public void Clear()
        {
            originalOutlines?.Clear();
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
