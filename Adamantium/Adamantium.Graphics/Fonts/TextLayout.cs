using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Core;
using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts;

public class TextLayout : DisposableObject
{
    public Guid Guid { get; }
    
    private const uint MaxItemsCount = 4096;
    
    private readonly GlyphLayoutContainer layoutContainer;
    public Typeface Typeface { get; }
    public IFont Font { get; }
    
    public uint ElementsCount { get; private set; }

    private Glyph spaceGlyph;
    private Glyph dotGlyph;
    private double dotGlyphsWidth;
    public Size CalculatedLayoutSize { get; private set; }

    private TextRenderingParameters _previousRenderingParameters;

    private List<GlyphWordData> _wordData;
    private FontItem[] fontItems;

    public TextRenderingParameters RenderingParameters { get; private set; }
    public Buffer<FontItem> VertexBuffer { get; private set; } 
    
    public FontAtlas FontAtlas { get; private set; }

    public string Text { get; private set; }
    
    public float FontSize { get; private set; }

    /// <summary>
    /// How far (screen px) a glyph's effect (outline/glow/shadow) can reach beyond its body = the atlas
    /// margin scaled to the current font size (same ratio the glyph quad is expanded by, constant across
    /// glyphs). The text render target and composite quad are padded by this so effects on the edge glyphs
    /// of the block aren't clipped at the text boundary. 0 when the atlas isn't built yet. 
    /// </summary>
    public int EffectPadding => FontAtlas == null
        ? 0
        : (int)Math.Ceiling(FontAtlas.GlyphMargin * FontSize / FontAtlas.MSDFTextureSize);

    public Size RealTextDimensions { get; private set; }
    
    private bool _textUpdated;

    public TextLayout(Typeface typeface, IFont font)
    {
        Guid = Guid.NewGuid();
        Typeface = typeface;
        Font = font;
        spaceGlyph = font.GetGlyphByCharacter(' ');
        dotGlyph = font.GetGlyphByCharacter('.');

        layoutContainer = new GlyphLayoutContainer(typeface, font);
        fontItems = new FontItem[MaxItemsCount];
    }

    public GlyphWordData[] GetTextData()
    {
        return _wordData.ToArray();
    }

    private void CalculateRealTextDimensions()
    {
        var minX = _wordData.Min(x => x.Rect.Left);
        var maxX = _wordData.Max(x => x.Rect.Right);
        var minY = _wordData.Min(x => x.Rect.Top);
        var maxY = _wordData.Max(x => x.Rect.Bottom);
        RealTextDimensions = new Size(maxX - minX, maxY - minY);
    }

    private bool CompareInputParameters(string text, 
        double fontSize, 
        TextRenderingParameters renderingParameters)
    {
        return Text == text && MathHelper.IsZero(FontSize - fontSize) &&
               _previousRenderingParameters == renderingParameters;
    }

    public Size ProcessText(string text, 
        double fontSize, 
        Size textArea,
        TextWrapping textWrapping, 
        TextTrimming textTrimming,
        HorizontalTextAlignment horizontalTextAlignment,
        VerticalTextAlignment verticalTextAlignment)
    {
        if (Double.IsNaN(textArea.Width))
        {
            textArea.Width = Int32.MaxValue;
        }
        if (Double.IsNaN(textArea.Height))
        {
            textArea.Height = Int32.MaxValue;
        }
        var @params = new TextRenderingParameters()
            { 
                HorizontalTextAlignment = horizontalTextAlignment, 
                VerticalTextAlignment = verticalTextAlignment,
                TextWrapping = textWrapping, 
                TextTrimming = textTrimming,
                TextArea = new Rectangle(Vector2F.Zero, textArea)
            };
        
        if (CompareInputParameters(text, fontSize, @params))
            return CalculatedLayoutSize;
        
        Text = text;
        FontSize = (float)fontSize;
        _previousRenderingParameters = @params;
        
        _textUpdated = true;
        return ProcessText(text, fontSize, @params);
    }

    public Size ProcessText(string text, double fontSize, TextRenderingParameters renderingParameters)
    {
        if (string.IsNullOrEmpty(text))
            return Size.Zero;

        RenderingParameters = renderingParameters;

        var glyphs = Font.TranslateIntoGlyphs(text);
        layoutContainer.SetText(text);

        // try to apply GPOS kern
        var kernApplied = Font.FeatureService.ApplyFeature(Features.kern, layoutContainer, 0, (uint)glyphs.Count);
        // var subApp = font.FeatureService.ApplyFeature(Features.aalt, layoutContainer, 0, (uint)glyphs.Length);

        var scale = fontSize / Font.UnitsPerEm;
        var lineHeight = Font.LineGap == 0 ? fontSize : Font.LineGap * scale;
        lineHeight += fontSize;
        //var capH = (font.UnitsPerEm - (font.Ascender - font.LineGap)) * scale;
        var baseLine = Font.Baseline * scale;
        double spaceWidth = spaceGlyph.AdvanceWidth * scale;

        dotGlyphsWidth = (dotGlyph.AdvanceWidth * scale * 3);

        var textArea = renderingParameters.TextArea;
        double width = 0;
        double height = textArea.Y;
        double cursorPosition = 0;
        double wordStartPosition = 0;
        var words = text.Split(' ');
        var glyphsData = new List<GlyphWordData>();
        int wordIndex = 0;
        int lineIndex = 0;
        int positionInString = 0;
        for (var index = 0; index < words.Length; index++)
        {
            wordIndex = index;
            var word = words[index];
            var proceed = ProcessWord(word);

            if (!proceed)
            {
                break;
            }

            if (wordIndex < words.Length - 1)
            {
                var rect = new RectangleF((float)Math.Ceiling(cursorPosition),
                    (float)Math.Ceiling(height + baseLine),
                    (float)Math.Ceiling(spaceWidth),
                    0f); 
                // add space after word
                glyphsData.Add(new GlyphWordData(spaceGlyph, ' ',
                    rect,
                    -1,
                    lineIndex));
                cursorPosition += spaceWidth;
            }
        }

        _wordData = glyphsData;
        height = _wordData.Max(x => x.Rect.Bottom);
        
        CalculateRealTextDimensions();
        
        var maxX = _wordData.Max(x => x.Rect.Right);
        var finalRect = new Size(Math.Ceiling(maxX), Math.Ceiling(height));
        if (renderingParameters.TextArea.Width != Int32.MaxValue)
        {
            finalRect.Width = renderingParameters.TextArea.Width;
        }
        
        if (renderingParameters.TextArea.Height != Int32.MaxValue)
        {
            finalRect.Height = renderingParameters.TextArea.Height;
        }
        
        ArrangeText();
        
        CalculatedLayoutSize = finalRect;
        
        return CalculatedLayoutSize;

        bool ProcessWord(string word)
        {
            var wordWidth = GetWordWidth(scale, word);
            wordStartPosition = cursorPosition;
            for (var i = 0; i < word.Length; i++)
            {
                var symbol = word[i];
                var glyph = Font.GetGlyphByCharacter(symbol);
                switch (symbol)
                {
                    case '\n':
                        height += lineHeight;
                        cursorPosition = 0;
                        lineIndex++;
                        break;
                    case ' ':
                    {
                        cursorPosition += glyph.AdvanceWidth * scale;
                        break;
                    }
                    default:
                    {
                        var glyphLeft = cursorPosition;
                        var glyphBase = height + baseLine;

                        var glyphRect = CalculateGlyphPosition(glyph,
                            glyphLeft,
                            glyphBase,
                            kernApplied,
                            i,
                            fontSize,
                            scale);

                        cursorPosition += glyph.AdvanceWidth * scale;
                        
                        glyphsData.Add(new GlyphWordData(glyph, symbol, glyphRect, i, lineIndex));

                        switch (renderingParameters.TextWrapping)
                        {
                            case TextWrapping.NoWrap:
                                if (cursorPosition > textArea.Width)
                                {
                                    if (!IsLastGlyph(i, text.Length))
                                    {
                                        var glyphsDataCopy = glyphsData.ToArray();
                                        PrepareDataAndTrim(glyphsDataCopy, i, glyphBase);
                                        return false;
                                    }
                                }
                                break;
                            case TextWrapping.WrapBySymbols:
                                {
                                    if (cursorPosition > textArea.Width)
                                    {
                                        var glyphsDataCopy = glyphsData.ToArray();
                                        // We have more vertical space for text
                                        if (height + lineHeight < textArea.Height)
                                        {
                                            lineIndex++;
                                            height += lineHeight;
                                            glyphBase = height + baseLine;
                                            RearrangeData(glyphsDataCopy, glyphBase);
                                        }
                                        else if (!IsLastGlyph(i, text.Length))
                                        {
                                            PrepareDataAndTrim(glyphsDataCopy, i, glyphBase);
                                            return false;
                                        }
                                    }
                                }
                                break;
                            case TextWrapping.WrapByWords:
                                if (wordStartPosition + wordWidth > textArea.Width && wordIndex > 0)
                                {
                                    if (height + lineHeight < textArea.Height)
                                    {
                                        wordStartPosition = 0;
                                        lineIndex++;
                                        height += lineHeight;
                                        glyphBase = height + baseLine;
                                        var glyphsDataCopy = glyphsData.ToArray();
                                        RearrangeData(glyphsDataCopy, glyphBase);
                                    }
                                    else if (!IsLastGlyph(i, word.Length))
                                    {
                                        var glyphsDataCopy = glyphsData.ToArray();
                                        PrepareDataAndTrim(glyphsDataCopy, i, glyphBase);
                                        return false;
                                    }
                                }
                                break;
                        }
                        break;
                    }
                }
            }
            return true;
        }

        void ArrangeText()
        {
            var minX = _wordData.Min(x => x.Rect.Left);
            var maxX = _wordData.Max(x => x.Rect.Right);
            var minY = _wordData.Min(x => x.Rect.Top);
            var maxY = _wordData.Max(x => x.Rect.Bottom);
            switch (renderingParameters.HorizontalTextAlignment)
            {
                case HorizontalTextAlignment.Center:
                {
                    var maxLines = _wordData.Max(x => x.LineIndex);
                    for (int i = 0; i <= maxLines; ++i)
                    {
                        var glyphsForLine = _wordData.Where(x => x.LineIndex == i).ToArray();
                        if (glyphsForLine.Length == 0) break;
                        
                        minX = glyphsForLine.Min(x => x.Rect.Left);
                        maxX = glyphsForLine.Max(x => x.Rect.Right);
                        var lineWidth = maxX - minX;
                        var diff = (finalRect.Width - lineWidth) / 2;
                        foreach (var glyphWordData in glyphsForLine)
                        {
                            var rect = glyphWordData.Rect;
                            rect.X += (float)diff;
                            glyphWordData.Rect = rect;
                        }
                    }
                }
                break;
                case HorizontalTextAlignment.Right:
                {
                    var maxLines = _wordData.Max(x => x.LineIndex);
                    for (int i = 0; i <= maxLines; ++i)
                    {
                        var glyphsForLine = _wordData.Where(x => x.LineIndex == i).ToArray();
                        if (glyphsForLine.Length == 0) break;
                        
                        // get max right point ignoring spaces in the end of the line
                        maxX = glyphsForLine.Where(x=>x.Symbol != ' ').Max(x => x.Rect.Right);
                        var diff = (finalRect.Width - maxX);
                        foreach (var glyphWordData in glyphsForLine)
                        {
                            var rect = glyphWordData.Rect;
                            rect.X += (float)diff;
                            glyphWordData.Rect = rect;
                        }
                    }
                }
                break;
                case HorizontalTextAlignment.Justify:
                {
                    var maxLines = _wordData.Max(x => x.LineIndex);
                    for (int i = 0; i <= maxLines; ++i)
                    {
                        var glyphsForLine = _wordData.Where(x => x.LineIndex == i && x.Symbol != ' ').ToArray();
                        if (glyphsForLine.Length == 0) break;
                        
                        var lineWidth = glyphsForLine.Take(glyphsForLine.Length - 1).Sum(x => x.Rect.Width);
                        var diff = (finalRect.Width - lineWidth) / (glyphsForLine.Length - 1);
                        var cursor = 0;

                        int cnt = 0;
                        foreach (var glyphWordData in glyphsForLine)
                        {
                            var leftSideBearing = glyphWordData.Glyph.LeftSideBearing * scale;
                            if (cnt == 0)
                            {
                                leftSideBearing = 0;
                            }
                            var rect = glyphWordData.Rect;
                            rect.X = (float)(cursor + leftSideBearing);
                            glyphWordData.Rect = rect;
                            cursor = (int)(rect.Right + diff);
                            cnt++;
                        }
                    }
                }
                break;
            }
            
            switch (renderingParameters.VerticalTextAlignment)
            {
                case VerticalTextAlignment.Center:
                {
                    var realTextSize = RealTextDimensions;
                    var center = (Vector2)(finalRect - realTextSize)/2;
                    var diff = center.Y - minY;
                    foreach (var glyphWordData in _wordData)
                    {
                        var rect = glyphWordData.Rect;
                        rect.Y += (float)diff;
                        glyphWordData.Rect = rect;
                    }
                    
                }
                break;
            }
        }

        void RearrangeData(GlyphWordData[] glyphsDataCopy, double glyphBase)
        {
            var rearrangeList = new List<GlyphWordData>();
            for (int k = glyphsDataCopy.Length - 1; k >= 0; k--)
            {
                var data = glyphsData[k];
                cursorPosition -= data.Glyph.AdvanceWidth * scale;
                var wordsLeft = glyphsDataCopy.Take(k).Count(x => x.Symbol == ' ') + 1;
                rearrangeList.Add(data);
                if (wordIndex > 0 && 
                    cursorPosition <= textArea.Width && 
                    renderingParameters.TextWrapping == TextWrapping.WrapByWords && 
                    data.Symbol == ' ')
                {
                    break;
                }
                else if (cursorPosition <= textArea.Width && 
                         renderingParameters.TextWrapping == TextWrapping.WrapBySymbols)
                {
                    break;
                }
                else if (wordsLeft == 1 &&
                         cursorPosition > textArea.Width &&
                         renderingParameters.TextWrapping == TextWrapping.WrapByWords)
                {
                    break;
                }
            }

            rearrangeList.Reverse();
            cursorPosition = 0;

            for (var index = 0; index < rearrangeList.Count; index++)
            {
                var glyphData = rearrangeList[index];
                if (index == 0 && glyphData.Glyph == spaceGlyph) continue;
                
                var glyphRect = CalculateGlyphPosition(glyphData.Glyph,
                    cursorPosition,
                    glyphBase,
                    kernApplied,
                    glyphData.PositionInString,
                    fontSize,
                    scale);

                glyphData.Rect = glyphRect;
                glyphData.LineIndex = lineIndex;
                cursorPosition += glyphData.Glyph.AdvanceWidth * scale;
            }
        }

        void PrepareDataAndTrim(GlyphWordData[] glyphsDataCopy, int position, double glyphBase)
        {
            for (int k = glyphsDataCopy.Length - 1; k >= 0; k--)
            {
                var data = glyphsData[k];
                cursorPosition -= data.Glyph.AdvanceWidth * scale;
                glyphsData.RemoveAt(k);
                if (renderingParameters.TextTrimming == TextTrimming.None &&
                    cursorPosition <= textArea.Width)
                {
                    break;
                }
                else if (renderingParameters.TextTrimming == TextTrimming.CharEllipses &&
                    cursorPosition + dotGlyphsWidth <= textArea.Width)
                {
                    break;
                }
                else if (renderingParameters.TextTrimming ==
                         TextTrimming.WordEllipses &&
                         cursorPosition + dotGlyphsWidth <= textArea.Width)
                {
                    if (wordIndex > 0 && data.Glyph != spaceGlyph)
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (renderingParameters.TextTrimming != TextTrimming.None)
            {
                TrimText(position, glyphBase);
            }

            width = cursorPosition;
        }

        // Adds ... to the end of the string
        void TrimText(int position, double glyphBase)
        {
            for (int j = 0; j < 3; j++)
            {
                var glyphRect = CalculateGlyphPosition(dotGlyph,
                    cursorPosition,
                    glyphBase,
                    kernApplied,
                    position,
                    fontSize,
                    scale);
                glyphsData.Add(new GlyphWordData(dotGlyph, '.', glyphRect, -1, lineIndex));

                cursorPosition += dotGlyph.AdvanceWidth * scale;
            }
        }
    }

    public void Update(IGraphicsDevice graphicsDevice)
    {
        if (!_textUpdated) return;
        
        FontAtlas ??= FontAtlasStore.GetOrCreateFrom(graphicsDevice, Typeface, FontParameters.Default(sortingVariant:GlyphSortingVariant.ByIndex));
        FontAtlas.Update(Text+".");
        ElementsCount = 0;
        VertexBuffer ??= Adamantium.Graphics.Buffer.Vertex.New<FontItem>(graphicsDevice, MaxItemsCount);

        for (int i = 0; i < _wordData.Count; ++i)
        {
            var word = _wordData[i];
            if (word.Glyph == spaceGlyph) continue;

            // Render the glyph quad as the FULL cell (body + margin), not just the body, so effects that
            // reach outside the contour (outline/glow/shadow) have geometry and field to draw into. The body
            // keeps its exact screen position/size - we only add the margin ring around it, scaled from atlas
            // texels to screen pixels by the glyph's own body scale. For plain text the margin samples
            // median < 0.5 -> opacity 0 -> fully transparent, so this is visually identical.
            var gd = FontAtlas.GetGlyphData(word.Glyph.Index);
            FontItem item;
            if (gd != null && gd.BoundingRect.Width > 0 && gd.BoundingRect.Height > 0)
            {
                var rect = word.Rect;
                // Per-axis margin: this maps the body EXACTLY onto [rect.X/Y .. +Width/Height], i.e. onto the
                // integer, baseline-flat glyph rect produced by CalculateGlyphPosition. A uniform margin would
                // instead map the body to its un-rounded sub-pixel position (via the cell proportions) and the
                // baseline would wobble again. For a sub-pixel-thin glyph the rounded side can be 0, which
                // would zero that axis' margin and collapse the quad (the glyph vanishes); fall back to the
                // uniform font-scale margin there - a 0-px side has no integer baseline row to preserve anyway.
                var uniform = gd.Margin * (double)FontSize / FontAtlas.MSDFTextureSize;
                var mx = rect.Width > 0 ? gd.Margin * (double)rect.Width / gd.BoundingRect.Width : uniform;
                var my = rect.Height > 0 ? gd.Margin * (double)rect.Height / gd.BoundingRect.Height : uniform;
                item = new FontItem
                {
                    ArrangeRect = new Vector4F((float)(rect.X - mx), (float)(rect.Y - my),
                        (float)(rect.Width + 2 * mx), (float)(rect.Height + 2 * my)),
                    Source = gd.UVRectFull,
                    Depth = 1.0f
                };
            }
            else
            {
                item = new FontItem
                {
                    ArrangeRect = word.Rect,
                    Source = FontAtlas.GetUVCoordinatesForGlyph(word.Glyph.Index),
                    Depth = 1.0f
                };
            }
            fontItems[ElementsCount] = item;
            ElementsCount++;
        }

        VertexBuffer.SetData(fontItems, 0, ElementsCount, 0);

        _textUpdated = true;
    }

    private bool IsLastGlyph(int position, int count)
    {
        return !(position < count - 1);
    }

    private double GetWordWidth(double scale, string word)
    {
        var wordGlyphs = Font.TranslateIntoGlyphs(word);
        double wordWidth = 0;
        for (int k = 0; k < wordGlyphs.Count; ++k)
        {
            wordWidth += wordGlyphs[k].BoundingRectangle.Width * scale;
        }

        return wordWidth;
    }

    private RectangleF CalculateGlyphPosition(
        Glyph glyph,
        double glyphLeft,
        double glyphBase,
        bool kernApplied,
        int position,
        double fontSize,
        double scale)
    {
        // if GPOS kern is not applied - try TTF kern approach
        if (!kernApplied && position > 0)
        {
            var prevGlyph = layoutContainer.GetGlyph(position - 1);
            glyphLeft += fontSize * Font.GetKerningValue((ushort)prevGlyph.Index, (ushort)glyph.Index) /
                         Font.UnitsPerEm;
        }

        var verticalShift = -glyph.BoundingRectangle.Y * scale;
        var horizontalShift = glyph.LeftSideBearing * scale;

        var glyphWidth = glyph.BoundingRectangle.Width * scale;
        var glyphHeight = glyph.BoundingRectangle.Height * scale;
        var glyphTop = (glyphBase - glyphHeight) + verticalShift;
        // Apply the left side bearing for EVERY glyph. Previously the first glyph skipped it, so its ink was
        // shifted left while the pen still advanced by the full width - the gap after the first letter came
        // out inflated by that bearing. Now the line just starts at the natural LSB indent.
        glyphLeft += horizontalShift;

        // if GPOS kern is applied - modify the advance for current glyph
        if (kernApplied && position > 0)
        {
            //glyphLeft += fontSize * layoutContainer.GetAdvance((uint)position).X * scale;
        }

        // Anchor every glyph to the line's baseline so they share an exact pixel row. Rounding glyphTop and
        // the ink bottom independently rounds each glyph's own ink extremes, so a letter that overshoots the
        // baseline (round bottoms: о е с а) lands a whole pixel below a flat-bottomed one (к и в) - the
        // baseline visibly wobbles once the text is crisp. Instead round the baseline ONCE (it's the same for
        // the whole line) and round the ascent/descent RELATIVE to it: sub-pixel overshoot is absorbed, real
        // descenders (р у) are preserved, and every baseline-sitting glyph bottoms on the same row.
        var baseR = (int)System.Math.Round(glyphBase);
        var top = baseR - (int)System.Math.Round(glyphBase - glyphTop);
        var bottom = baseR - (int)System.Math.Round(glyphBase - (glyphTop + glyphHeight));

        // X axis stays SUB-PIXEL: keep the glyph at its exact fractional position/width (do NOT round). The
        // pen advances are fractional, so snapping X to whole pixels makes equal metric gaps render 1px apart
        // (the pixel-snap wobble, e.g. "Проверка"). At sub-pixel X the gaps match the metrics exactly and the
        // MSDF anti-aliasing renders the fractional vertical edges cleanly. Y stays snapped (baseR above) so
        // horizontal strokes and the baseline remain crisp - we only give up snapping on the axis where even
        // spacing matters more than pixel-aligned stems.
        return new RectangleF((float)glyphLeft, top, (float)glyphWidth, bottom - top);
    }
}

class BackToFrontComparer : IComparer<FontItem>
{
    public int Compare(FontItem left, FontItem rigth)
    {
        return rigth.Depth.CompareTo(left.Depth);
    }
}

class FrontToBackComparer : IComparer<FontItem>
{
    public int Compare(FontItem left, FontItem rigth)
    {
        return left.Depth.CompareTo(rigth.Depth);
    }
}