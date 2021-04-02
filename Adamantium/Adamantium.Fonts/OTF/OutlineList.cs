using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.OTF
{
    public class OutlineList
    {
        private CommandInterpreter interpreter;
        internal List<Outline> Outlines { get; }

        public OutlineList()
        {
            interpreter = new CommandInterpreter();
            Outlines = new List<Outline>();
        }

        public void Fill(List<Command> commands, uint glyphIndex)
        {
            Outline outline = null;

            foreach (var command in commands)
            {
                if (IsNewOutline(command))
                {
                    outline = new Outline();
                    Outlines.Add(outline);
                }

                var pts = interpreter.GetOutlinePoints(command);
                outline?.Points.AddRange(pts);
            }
        }

        public void SplitToSegments()
        {
            foreach (var outline in Outlines)
            {
                // we assume that the outline starting from non-control point
                if (outline.Points[0].IsControl)
                {
                    throw new OutlineException("First point of outline is control");
                }
                
                var segment = new List<Vector2D>();
                
                for (var index = 0; index < outline.Points.Count; index++)
                {
                    var point = outline.Points[index];
                    segment.Add(new Vector2D(point.X, point.Y));

                    if (!point.IsControl && segment.Count > 1) // segment is closed
                    {
                        outline.Segments.Add(new OutlineSegment(segment));
                        segment = new List<Vector2D>();
                        segment.Add(new Vector2D(point.X, point.Y)); // add the same non-control point as start of new segment
                    }
                }
                
                // add the first point of current outline as the last point of the last segment
                segment.Add(new Vector2D(outline.Points[0].X, outline.Points[0].Y));
            }
        }

        public void Clear()
        {
            Outlines?.Clear();
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
