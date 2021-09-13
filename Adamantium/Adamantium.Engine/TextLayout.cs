using System;
using System.Collections.Generic;
using System.Numerics;
using Adamantium.Engine.Core.Models;
using Adamantium.Fonts;
using Adamantium.Fonts.Common;
using Adamantium.Mathematics;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.Engine
{
    public class TextLayout
    {
        private IFont font;
        private string inputString;
        private float fontSizeInPixels;
        private Rectangle boundingRectangle;
        public float Scale { get; }

        public Mesh Mesh { get; private set; }

        public TextLayout(IFont font, string inputString, float fontSizeInPixels, Rectangle boundingRectangle)
        {
            this.font = font;
            this.inputString = inputString;
            this.fontSizeInPixels = fontSizeInPixels;
            this.boundingRectangle = boundingRectangle;
            Mesh = new Mesh();
            Scale = fontSizeInPixels / font.UnitsPerEm;

            ProcessInput();
        }

        private double SmartRoundValue(double value)
        {
            if ((Math.Ceiling(value) - value) < 0.5)
            {
                return Math.Ceiling(value);
            }

            return Math.Floor(value);
        }

        private SampledOutline[] SmartOutlinesScale(SampledOutline[] outlines)
        {
            var roundedOutlines = new List<SampledOutline>();

            var cachedX = new Dictionary<double, double>();
            var cachedY = new Dictionary<double, double>();
            
            foreach (var outline in outlines)
            {
                var roundedOutline = new List<Vector2D>();

                for (var j = 0; j < outline.Points.Length - 1; j++)
                {
                    var currentPoint = outline.Points[j] * Scale;
                    roundedOutline.Add(currentPoint);
                    
                    if (cachedX.ContainsKey(currentPoint.X))
                    {
                        currentPoint.X = cachedX[currentPoint.X];
                    }
                    
                    if (cachedY.ContainsKey(currentPoint.Y))
                    {
                        currentPoint.Y = cachedY[currentPoint.Y];
                    }

                    var nextPoint = outline.Points[j + 1] * Scale;

                    if (currentPoint.X == nextPoint.X)
                    {
                        // vertical line, round X only
                        var roundedValue = SmartRoundValue(currentPoint.X);
                        cachedX[currentPoint.X] = roundedValue;
                        currentPoint.X = roundedValue;
                    }
                    else if (currentPoint.Y == nextPoint.Y)
                    {
                        // horizontal line, round Y only
                        var roundedValue = SmartRoundValue(currentPoint.Y);
                        cachedY[currentPoint.Y] = roundedValue;
                        currentPoint.Y = roundedValue;
                    }
                    
                    roundedOutline[j] = currentPoint;
                }
                
                roundedOutline.Add(roundedOutline[0]);

                var roundedSampledOutline = new SampledOutline(roundedOutline.ToArray()); 
                
                roundedOutlines.Add(roundedSampledOutline);
            }

            return roundedOutlines.ToArray();
        }
        
        private void ProcessInput()
        {
            var glyphs = font.TranslateIntoGlyphs(inputString);
            float stringWidth = 0;

            foreach (var glyph in glyphs)
            {
                //var outlines = glyph.Triangulate(2);
                var outlines = glyph.Sample(2);

                var res = SmartOutlinesScale(outlines);
                
                var polygon = new Polygon();
                polygon.FillRule = FillRule.NonZero;
                foreach (var outline in res)
                {
                    polygon.Polygons.Add(new PolygonItem(outline.Points));
                }
                
                var points = polygon.Fill().ToArray();


                if (glyph.HasOutlines)
                {
                    var mesh = new Mesh();
                    mesh.SetPositions(points);

                    var translationMatrix = Matrix4x4F.Identity;
                    //translationMatrix.ScaleVector = new Vector3F(Scale, Scale, 1);
                    translationMatrix.TranslationVector = new Vector3F((float)Math.Ceiling(stringWidth * Scale), 30, 0);
                    // var scale = Matrix4x4F.Scaling(Scale, Scale, 1);
                    // var result = scale * translationMatrix;
                    mesh.ApplyTransform(translationMatrix);
                    Mesh = Mesh.Merge(mesh);
                }

                stringWidth += glyph.AdvanceWidth/* - glyph.LeftSideBearing*/;
            }

            // for (var index = 0; index < Mesh.Positions.Length; index++)
            // {
            //     var position = Mesh.Positions[index];
            //     position.X = (float)Math.Round(position.X * Scale, 0, MidpointRounding.AwayFromZero);
            //     position.Y = (float)Math.Round(position.Y * Scale, 0, MidpointRounding.AwayFromZero);
            //     Mesh.Positions[index] = position;
            // }

            Mesh.Optimize(false, false);
        }
    }
}