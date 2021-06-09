﻿using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    internal class CommandInterpreter
    {
        private OutlinePoint currentPoint;

        public OutlinePoint[] GetOutlinePoints(Command command, VariationRegionList regionList, float[] variationPoint)
        {
            var pointList = new List<OutlinePoint>();

            command.ApplyBlend(regionList, variationPoint);
            
            switch (command.Operator)
            {
                case OperatorsType.rmoveto:
                    // If we have 3 operands, this means that first value is Glyph width, so we must omit it
                    // The same for hmoveto and vmoveto operators
                    int startingIndex = 0;
                    if (command.BlendedOperands.Count % 2 != 0)
                    {
                        startingIndex = 1;
                    }
                    currentPoint.X += command.BlendedOperands[startingIndex++].Value;
                    currentPoint.Y += command.BlendedOperands[startingIndex].Value;
                    pointList.Add(currentPoint);
                    break;
                case OperatorsType.hmoveto:
                    startingIndex = 0;
                    if (command.BlendedOperands.Count == 2)
                    {
                        startingIndex = 1;
                    }
                    currentPoint.X += command.BlendedOperands[startingIndex].Value;
                    pointList.Add(currentPoint);
                    break;
                case OperatorsType.vmoveto:
                    startingIndex = 0;
                    if (command.BlendedOperands.Count == 2)
                    {
                        startingIndex = 1;
                    }
                    currentPoint.Y += command.BlendedOperands[startingIndex].Value;
                    pointList.Add(currentPoint);
                    break;
                case OperatorsType.rlineto:
                    int i;
                    for (i = 0; i < command.BlendedOperands.Count; i += 2)
                    {
                        // previous point is the start (real) point of cubic Bezier

                        // take next 2 values
                        var values = command.BlendedOperands.Skip(i).Take(2).ToArray();

                        var points = Rlineto(values);
                        pointList.AddRange(points);
                    }

                    break;
                case OperatorsType.hlineto:
                    //|- dx1 {dya dxb}*  hlineto (6) |-
                    //|- {dxa dyb}+  hlineto (6) |-

                    //appends a horizontal line of length 
                    //dx1 to the current point. 

                    //With an odd number of arguments, subsequent argument pairs 
                    //are interpreted as alternating values of 
                    //dy and dx, for which additional lineto
                    //operators draw alternating vertical and 
                    //horizontal lines.

                    //With an even number of arguments, the 
                    //arguments are interpreted as alternating horizontal and 
                    //vertical lines. The number of lines is determined from the 
                    //number of arguments on the stack.

                    if (command.BlendedOperands.Count % 2 != 0)
                    {
                        currentPoint.X += command.BlendedOperands[0].Value;
                        pointList.Add(currentPoint);
                        
                        for (i = 1; i < command.BlendedOperands.Count; i+=2)
                        {
                            currentPoint.Y += command.BlendedOperands[i].Value;
                            pointList.Add(currentPoint);
                            
                            currentPoint.X += command.BlendedOperands[i+1].Value;
                            pointList.Add(currentPoint);
                        }
                    }
                    else
                    {
                        for (i = 0; i < command.BlendedOperands.Count; i+=2)
                        {
                            currentPoint.X += command.BlendedOperands[i].Value;
                            pointList.Add(currentPoint);
                            
                            currentPoint.Y += command.BlendedOperands[i+1].Value;
                            pointList.Add(currentPoint);
                        }
                    }
                    break;
                case OperatorsType.vlineto:
                    //|- dy1 {dxa dyb}*  vlineto (7) |-
                    //|- {dya dxb}+  vlineto (7) |-

                    //appends a vertical line of length 
                    //dy1 to the current point. 

                    //With an odd number of arguments, subsequent argument pairs are 
                    //interpreted as alternating values of dx and dy, for which additional 
                    //lineto operators draw alternating horizontal and 
                    //vertical lines.

                    //With an even number of arguments, the 
                    //arguments are interpreted as alternating vertical and 
                    //horizontal lines. The number of lines is determined from the 
                    //number of arguments on the stack. 
                    //first elem
                    
                    if (command.BlendedOperands.Count % 2 != 0)
                    {
                        currentPoint.Y += command.BlendedOperands[0].Value;
                        pointList.Add(currentPoint);
                        
                        for (i = 1; i < command.BlendedOperands.Count; i+=2)
                        {
                            currentPoint.X += command.BlendedOperands[i].Value;
                            pointList.Add(currentPoint);
                            
                            currentPoint.Y += command.BlendedOperands[i+1].Value;
                            pointList.Add(currentPoint);
                        }
                    }
                    else
                    {
                        for (i = 0; i < command.BlendedOperands.Count; i+=2)
                        {
                            currentPoint.Y += command.BlendedOperands[i].Value;
                            pointList.Add(currentPoint);
                            
                            currentPoint.X += command.BlendedOperands[i+1].Value;
                            pointList.Add(currentPoint);
                        }
                    }
                    break;
                case OperatorsType.rrcurveto:
                    for (i = 0; i < command.BlendedOperands.Count; i += 6)
                    {
                        // previous point is the start (real) point of cubic Bezier

                        // take next 6 values
                        var values = command.BlendedOperands.Skip(i).Take(6).ToArray();

                        var points = Rrcurveto(values);
                        pointList.AddRange(points);
                    }

                    break;
                case OperatorsType.hhcurveto:
                    i = 0;

                    if ((command.BlendedOperands.Count % 2) != 0)
                    {
                        var points = Rrcurveto(
                            command.BlendedOperands[1],
                            command.BlendedOperands[0],
                            command.BlendedOperands[2],
                            command.BlendedOperands[3],
                            command.BlendedOperands[4],
                            new CommandOperand(0));
                        pointList.AddRange(points);

                        i += 5;
                    }

                    for (; i < command.BlendedOperands.Count; i += 4)
                    {
                        // take next 4 values
                        var values = command.BlendedOperands.Skip(i).Take(4).ToArray();

                        var points = Rrcurveto(
                            values[0],
                            new CommandOperand(0),
                            values[1],
                            values[2],
                            values[3],
                            new CommandOperand(0));
                        pointList.AddRange(points);
                    }

                    break;
                case OperatorsType.vvcurveto:
                    i = 0;

                    if ((command.BlendedOperands.Count % 2) != 0)
                    {
                        var points = Rrcurveto(
                            command.BlendedOperands[0],
                            command.BlendedOperands[1],
                            command.BlendedOperands[2],
                            command.BlendedOperands[3],
                            new CommandOperand(0),
                            command.BlendedOperands[4]);
                        pointList.AddRange(points);

                        i += 5;
                    }

                    for (; i < command.BlendedOperands.Count; i += 4)
                    {
                        // take next 4 values
                        var values = command.BlendedOperands.Skip(i).Take(4).ToArray();

                        var points = Rrcurveto(
                            new CommandOperand(0),
                            values[0],
                            values[1],
                            values[2],
                            new CommandOperand(0),
                            values[3]);
                        pointList.AddRange(points);
                    }

                    break;
                case OperatorsType.hvcurveto:
                    var remainder = command.BlendedOperands.Count % 8;

                    switch (remainder)
                    {
                        case 0:
                        case 1:
                            //|- {dxa dxb dyb dyc dyd dxe dye dxf}+ dyf? hvcurveto (31) |-
                            i = 0;

                            for (; i < command.BlendedOperands.Count;)
                            {
                                // take next 8 values
                                var values = command.BlendedOperands.Skip(i).Take(8).ToArray();

                                var points = Rrcurveto(
                                    values[0],
                                    new CommandOperand(0),
                                    values[1],
                                    values[2],
                                    new CommandOperand(0),
                                    values[3]);
                                pointList.AddRange(points);

                                //last cycle
                                if (command.BlendedOperands.Count - i == 9)
                                {
                                    values = command.BlendedOperands.Skip(i).Take(9).ToArray();
                                    points = Rrcurveto(
                                        new CommandOperand(0),
                                        values[4],
                                        values[5],
                                        values[6],
                                        values[7],
                                        values[8]);
                                    pointList.AddRange(points);
                                    i += 9;
                                }
                                else
                                {
                                    points = Rrcurveto(
                                        new CommandOperand(0),
                                        values[4],
                                        values[5],
                                        values[6],
                                        values[7],
                                        new CommandOperand(0));
                                    pointList.AddRange(points);
                                    i += 8;
                                }
                            }

                            break;
                        
                        case 4:
                        case 5:
                            
                            //|- dx1 dx2 dy2 dy3 {dya dxb dyb dxc dxd dxe dye dyf}* dxf? hvcurveto (31) |-

                            //If there is a multiple of four arguments, the curve starts
                            //horizontal and ends vertical.
                            //Note that the curves alternate between start horizontal, end vertical, and start vertical, and
                            //end horizontal.The last curve(the odd argument case) need not
                            //end horizontal/vertical.
                            
                            i = 0;

                            if (command.BlendedOperands.Count == 5)
                            {
                                var values = command.BlendedOperands.Skip(i).Take(5).ToArray();
                                var points = Rrcurveto(
                                    values[0],
                                    new CommandOperand(0),
                                    values[1],
                                    values[2],
                                    values[4],
                                    values[3]);
                                pointList.AddRange(points);
                                i += 5;
                            }
                            else
                            {
                                var values = command.BlendedOperands.Skip(i).Take(4).ToArray();
                                var points = Rrcurveto(
                                    values[0],
                                    new CommandOperand(0),
                                    values[1],
                                    values[2],
                                    new CommandOperand(0),
                                    values[3]);
                                pointList.AddRange(points);
                                i += 4;
                            }

                            for (; i < command.BlendedOperands.Count;)
                            {
                                // take next 8 values
                                var values = command.BlendedOperands.Skip(i).Take(8).ToArray();

                                var points = Rrcurveto(
                                    new CommandOperand(0),
                                    values[0],
                                    values[1],
                                    values[2],
                                    values[3],
                                    new CommandOperand(0));
                                pointList.AddRange(points);

                                
                                if (command.BlendedOperands.Count - i == 9)
                                {
                                    values = command.BlendedOperands.Skip(i).Take(9).ToArray();
                                    points = Rrcurveto(
                                        values[4],
                                        new CommandOperand(0),
                                        values[5],
                                        values[6],
                                        values[8],
                                        values[7]);
                                    pointList.AddRange(points);
                                    i += 9;
                                }
                                else
                                {
                                    points = Rrcurveto(
                                        values[4],
                                        new CommandOperand(0),
                                        values[5],
                                        values[6],
                                        new CommandOperand(0),
                                        values[7]);
                                    pointList.AddRange(points);
                                    i += 8;                                    
                                }
                            }
                            break;
                    }
                    break;
                case OperatorsType.vhcurveto:
                    //|- dy1 dx2 dy2 dx3 {dxa dxb dyb dyc dyd dxe dye dxf}* dyf? vhcurveto (30) |-
                    //|- {dya dxb dyb dxc dxd dxe dye dyf}+ dxf? vhcurveto (30) |- 

                    //appends one or more Bézier curves to the current point, where 
                    //the first tangent is vertical and the second tangent is horizontal.
                    
                    //This command is the complement of 
                    //see the description of hvcurveto for more information.
                    
                    remainder = command.BlendedOperands.Count % 8;

                    switch (remainder)
                    {
                        case 0:
                        case 1:
                            //|- {dxa dxb dyb dyc dyd dxe dye dxf}+ dyf? hvcurveto (31) |-
                            i = 0;
                            
                            for (; i < command.BlendedOperands.Count;)
                            {
                                // take next 8 values
                                var values = command.BlendedOperands.Skip(i).Take(8).ToArray();

                                var points = Rrcurveto(
                                    new CommandOperand(0),
                                    values[0],
                                    values[1],
                                    values[2],
                                    values[3],
                                    new CommandOperand(0));
                                pointList.AddRange(points);

                                if (command.BlendedOperands.Count - i == 9)
                                {
                                    values = command.BlendedOperands.Skip(i).Take(9).ToArray();
                                    points = Rrcurveto(
                                        values[4],
                                        new CommandOperand(0),
                                        values[5],
                                        values[6],
                                        values[8],
                                        values[7]);
                                    pointList.AddRange(points);
                                    i += 9;
                                }
                                else
                                {
                                    points = Rrcurveto(
                                        values[4],
                                        new CommandOperand(0),
                                        values[5],
                                        values[6],
                                        new CommandOperand(0),
                                        values[7]);
                                    pointList.AddRange(points);
                                    i += 8;                                    
                                }
                            }
                            
                            break;
                        
                        case 4:
                        case 5:
                            
                            //|- dx1 dx2 dy2 dy3 {dya dxb dyb dxc dxd dxe dye dyf}* dxf? hvcurveto (31) |-

                            //If there is a multiple of four arguments, the curve starts
                            //horizontal and ends vertical.
                            //Note that the curves alternate between start horizontal, end vertical, and start vertical, and
                            //end horizontal.The last curve(the odd argument case) need not
                            //end horizontal/vertical.
                            
                            i = 0;

                            if (command.BlendedOperands.Count == 5)
                            {
                                var values = command.BlendedOperands.Skip(i).Take(5).ToArray();
                                var points = Rrcurveto(
                                    new CommandOperand(0),
                                    values[0],
                                    values[1],
                                    values[2],
                                    values[3],
                                    values[4]);
                                pointList.AddRange(points);
                                i += 5;
                            }
                            else
                            {
                                var values = command.BlendedOperands.Skip(i).Take(4).ToArray();
                                var points = Rrcurveto(
                                    new CommandOperand(0),
                                    values[0],
                                    values[1],
                                    values[2],
                                    values[3],
                                    new CommandOperand(0));
                                pointList.AddRange(points);
                                i += 4;
                            }
                            
                            for (; i < command.BlendedOperands.Count;)
                            {
                                // take next 8 values
                                var values = command.BlendedOperands.Skip(i).Take(8).ToArray();

                                var points = Rrcurveto(
                                    values[0],
                                    new CommandOperand(0),
                                    values[1],
                                    values[2],
                                    new CommandOperand(0),
                                    values[3]);
                                pointList.AddRange(points);

                                
                                if (command.BlendedOperands.Count - i == 9)
                                {
                                    values = command.BlendedOperands.Skip(i).Take(9).ToArray();
                                    points = Rrcurveto(
                                        new CommandOperand(0),
                                        values[4],
                                        values[5],
                                        values[6],
                                        values[7],
                                        values[8]);
                                    pointList.AddRange(points);
                                    i += 9;
                                }
                                else
                                {
                                    points = Rrcurveto(
                                        new CommandOperand(0),
                                        values[4],
                                        values[5],
                                        values[6],
                                        values[7],
                                        new CommandOperand(0));
                                    pointList.AddRange(points);
                                    i += 8;                                    
                                }
                            }
                            break;
                    }
                    break;
                case OperatorsType.rcurveline:
                    i = 0;

                    if (command.BlendedOperands.Count >= 8 &&
                        command.BlendedOperands.Count % 6 == 2)
                    {
                        var lastOperands = command.BlendedOperands.Skip(command.BlendedOperands.Count - 2).ToArray();
                        command.BlendedOperands.RemoveRange(command.BlendedOperands.Count - 2, 2);

                        for (; i < command.BlendedOperands.Count; i += 6)
                        {
                            // take next 6 values
                            var values = command.BlendedOperands.Skip(i).Take(6).ToArray();

                            pointList.AddRange(Rrcurveto(values));
                        }

                        pointList.AddRange(Rlineto(lastOperands));
                    }

                    break;
                case OperatorsType.rlinecurve:
                    i = 0;

                    if (command.BlendedOperands.Count >= 8 &&
                        command.BlendedOperands.Count % 2 == 0)
                    {
                        var lastOperands = command.BlendedOperands.Skip(command.BlendedOperands.Count - 6).ToArray();
                        command.BlendedOperands.RemoveRange(command.BlendedOperands.Count - 6, 6);

                        for (; i < command.BlendedOperands.Count; i += 2)
                        {
                            // take next 2 values
                            var values = command.BlendedOperands.Skip(i).Take(2).ToArray();

                            pointList.AddRange(Rlineto(values));
                        }

                        pointList.AddRange(Rrcurveto(lastOperands));
                    }

                    break;
                default:
                    break;
            }

            return pointList.ToArray();
        }

        private OutlinePoint[] Rlineto(params CommandOperand[] values)
        {
            var pointList = new List<OutlinePoint>();

            currentPoint.X += values[0].Value;
            currentPoint.Y += values[1].Value;
            pointList.Add(currentPoint);

            return pointList.ToArray();
        }
        
        private OutlinePoint[] Rrcurveto(params CommandOperand[] values)
        {
            var pointList = new List<OutlinePoint>();

            // first control point
            currentPoint.X += values[0].Value;
            currentPoint.Y += values[1].Value;
            currentPoint.IsControl = true;
            pointList.Add(currentPoint);

            // second control point
            currentPoint.X += values[2].Value;
            currentPoint.Y += values[3].Value;
            currentPoint.IsControl = true;
            pointList.Add(currentPoint);

            // end (real) control point
            currentPoint.X += values[4].Value;
            currentPoint.Y += values[5].Value;
            currentPoint.IsControl = false;
            pointList.Add(currentPoint);

            return pointList.ToArray();
        }
    }
}
