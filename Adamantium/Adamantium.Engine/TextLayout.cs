using System;
using System.Collections.Generic;
using System.Numerics;
using Adamantium.Engine.Core.Models;
using Adamantium.Fonts;
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

        private void ProcessInput()
        {
            var glyphs = font.TranslateIntoGlyphs(inputString);
            float stringWidth = 0;

            foreach (var glyph in glyphs)
            {
                var outlines = glyph.Triangulate(2);

                if (glyph.HasOutlines)
                {
                    var mesh = new Mesh();
                    mesh.SetPositions(outlines);

                    var translationMatrix = Matrix4x4F.Identity;
                    //translationMatrix.ScaleVector = new Vector3F(Scale, Scale, 1);
                    translationMatrix.TranslationVector = new Vector3F(stringWidth, 1000, 0);
                    // var scale = Matrix4x4F.Scaling(Scale, Scale, 1);
                    // var result = scale * translationMatrix;
                    mesh.ApplyTransform(translationMatrix);
                    Mesh = Mesh.Merge(mesh);
                }

                stringWidth += glyph.AdvanceWidth - glyph.LeftSideBearing;
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