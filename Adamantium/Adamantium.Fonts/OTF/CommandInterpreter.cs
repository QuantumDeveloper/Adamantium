using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.OTF
{
    public class CommandInterpreter
    {
        private OutlinePoint currentPoint;

        public OutlinePoint[] GetPoints(Command command)
        {
            var pointList = new List<OutlinePoint>();

            switch (command.@operator)
            {
                case OperatorsType.rmoveto:
                    currentPoint.X += command.operands[0];
                    currentPoint.Y += command.operands[1];
                    pointList.Add(currentPoint);
                    break;
                case OperatorsType.hmoveto:
                    currentPoint.X += command.operands[0];
                    pointList.Add(currentPoint);
                    break;
                case OperatorsType.vmoveto:
                    currentPoint.Y += command.operands[0];
                    pointList.Add(currentPoint);
                    break;
                case OperatorsType.rlineto:
                    int i;
                    for (i = 0; i < command.operands.Count; ++i)
                    {
                        if ((i % 2) == 0)
                        {
                            currentPoint.X += command.operands[i];
                        }
                        else
                        {
                            currentPoint.Y += command.operands[i];
                            pointList.Add(currentPoint);
                        }
                    }
                    break;
                case OperatorsType.hlineto:
                    for (i = 0; i < command.operands.Count; ++i)
                    {
                        if ((i % 2) == 0)
                        {
                            currentPoint.X += command.operands[i];
                            pointList.Add(currentPoint);
                        }
                        else
                        {
                            currentPoint.Y += command.operands[i];
                            pointList.Add(currentPoint);
                        }
                    }
                    break;
                case OperatorsType.vlineto:
                    for (i = 0; i < command.operands.Count; ++i)
                    {
                        if ((i % 2) == 0)
                        {
                            currentPoint.Y += command.operands[i];
                            pointList.Add(currentPoint);
                        }
                        else
                        {
                            currentPoint.X += command.operands[i];
                            pointList.Add(currentPoint);
                        }
                    }
                    break;
                case OperatorsType.rrcurveto:
                    for (i = 0; i < command.operands.Count; i+=6)
                    {
                        // @TODO add cubic Bezier

                        // previous point is the start (real) point of cubic Bezier

                        // take next 6 values
                        var values = command.operands.Skip(i).Take(6).ToArray();

                        var points = Rrcurveto(values);
                        pointList.AddRange(points);
                    }
                    break;
                case OperatorsType.hhcurveto:
                    i = 0;

                    if ((command.operands.Count % 2) == 1)
                    {
                        double[] values = { command.operands[1], command.operands[0], command.operands[2], command.operands[3], command.operands[4], 0 };
                        var points = Rrcurveto(values);
                        pointList.AddRange(points);

                        i += 5;
                    }

                    for (; i < command.operands.Count; i += 4)
                    {
                        // take next 4 values
                        var values = command.operands.Skip(i).Take(4).ToArray();

                        var points = Rrcurveto(new double[] { values[0], 0, values[1], values[2], values[3], 0 });
                        pointList.AddRange(points);
                    }
                    break;
                case OperatorsType.vvcurveto:
                    i = 0;

                    if ((command.operands.Count % 2) == 1)
                    {
                        double[] values = { command.operands[0], command.operands[1], command.operands[2], command.operands[3], 0, command.operands[4] };
                        var points = Rrcurveto(values);
                        pointList.AddRange(points);

                        i += 5;
                    }

                    for (; i < command.operands.Count; i += 4)
                    {
                        // take next 4 values
                        var values = command.operands.Skip(i).Take(4).ToArray();

                        var points = Rrcurveto(new double[] { 0, values[0], values[1], values[2], 0, values[3] });
                        pointList.AddRange(points);
                    }
                    break;
                /*case OutlinesOperatorsType.hvcurveto:
                    break;
                case OutlinesOperatorsType.vhcurveto:
                    break;*/
                default:
                    break;
            }

            return pointList.ToArray();
        }

        private OutlinePoint[] Rrcurveto(double[] values)
        {
            var pointList = new List<OutlinePoint>();

            // first control point
            currentPoint.X += values[0];
            currentPoint.Y += values[1];
            currentPoint.IsControl = true;
            pointList.Add(currentPoint);

            // second control point
            currentPoint.X += values[2];
            currentPoint.Y += values[3];
            currentPoint.IsControl = true;
            pointList.Add(currentPoint);

            // end (real) control point
            currentPoint.X += values[4];
            currentPoint.Y += values[5];
            currentPoint.IsControl = false;
            pointList.Add(currentPoint);

            return pointList.ToArray();
        }
    }
}
