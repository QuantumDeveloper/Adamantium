using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Core;
using Adamantium.Fonts;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics.Fonts;

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
        HorizontalTextAlignment horizontalTextAlignment)
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
        var kernApplied = Font.FeatureService.ApplyFeature(Features.kern, layoutContainer, 0, (uint)glyphs.Length);
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
                // add space after word
                glyphsData.Add(new GlyphWordData(spaceGlyph, ' ',
                    new RectangleF((float)Math.Ceiling(cursorPosition), 
                        (float)Math.Ceiling(height + baseLine), 
                        (float)Math.Ceiling(spaceWidth), 
                        0f), -1));
                cursorPosition += spaceWidth;
            }
        }

        _wordData = glyphsData;
        height = _wordData.Max(x => x.Rect.Bottom);
        
        CalculateRealTextDimensions();
        var maxX = _wordData.Max(x => x.Rect.Right);
        CalculatedLayoutSize = new Size(Math.Ceiling(maxX), Math.Ceiling(height));
        
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

                        cursorPosition += Math.Ceiling(glyph.AdvanceWidth * scale);
                        
                        glyphsData.Add(new GlyphWordData(glyph, symbol, glyphRect, i));

                        switch (renderingParameters.TextWrapping)
                        {
                            case TextWrapping.NoWrap:
                                switch (renderingParameters.TextTrimming)
                                {
                                    case TextTrimming.None:
                                    case TextTrimming.CharEllipses:
                                    case TextTrimming.WordEllipses:
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
                                }
                                break;
                            case TextWrapping.WrapBySymbols:
                                switch (renderingParameters.TextTrimming)
                                {
                                    case TextTrimming.None:
                                    case TextTrimming.CharEllipses:
                                    case TextTrimming.WordEllipses:
                                    {
                                        if (cursorPosition > textArea.Width)
                                        {
                                            var glyphsDataCopy = glyphsData.ToArray();
                                            // We have more vertical space for text
                                            if (height + lineHeight < textArea.Height)
                                            {
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

                                        break;
                                    }
                                }
                                break;
                            case TextWrapping.WrapByWords:
                                switch (renderingParameters.TextTrimming)
                                {
                                    case TextTrimming.None:
                                    case TextTrimming.CharEllipses:
                                    case TextTrimming.WordEllipses:
                                    {
                                        if (cursorPosition > textArea.Width && wordIndex == 0)
                                        {
                                            var glyphsDataCopy = glyphsData.ToArray();
                                            if (height + lineHeight < textArea.Height)
                                            {
                                                height += lineHeight;
                                                glyphBase = height + baseLine;
                                                RearrangeData(glyphsDataCopy, glyphBase);
                                            }
                                            else if (!IsLastGlyph(i, word.Length))
                                            {
                                                PrepareDataAndTrim(glyphsDataCopy, i, glyphBase);
                                                return false;
                                            }
                                        }
                                        if (wordStartPosition + wordWidth > textArea.Width && wordIndex > 0)
                                        {
                                            if (height + lineHeight < textArea.Height)
                                            {
                                                wordStartPosition = 0;
                                                cursorPosition = 0;
                                                height += lineHeight;
                                                glyphBase = height + baseLine;

                                                var dataCopy = glyphsData.TakeLast(i + 1).ToArray();
                                                for (var index = dataCopy.Length - 1; index >= 0; index--)
                                                {
                                                    var data = dataCopy[index];
                                                    glyphRect = CalculateGlyphPosition(data.Glyph,
                                                        cursorPosition,
                                                        glyphBase,
                                                        kernApplied,
                                                        data.PositionInString,
                                                        fontSize,
                                                        scale);
                                                    data.Rect = glyphRect;
                                                    cursorPosition += glyphRect.Width;
                                                }
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
                                }
                                break;
                        }
                        break;
                    }
                }
            }
            return true;
        }

        void RearrangeData(GlyphWordData[] glyphsDataCopy, double glyphBase)
        {
            var rearrangeList = new List<GlyphWordData>();
            for (int k = glyphsDataCopy.Length - 1; k >= 0; k--)
            {
                var data = glyphsData[k];
                cursorPosition -= Math.Ceiling(data.Glyph.AdvanceWidth * scale);
                rearrangeList.Add(data);
                if (cursorPosition <= textArea.Width)
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
                cursorPosition += Math.Ceiling(glyphData.Glyph.AdvanceWidth * scale);
            }
        }

        void PrepareDataAndTrim(GlyphWordData[] glyphsDataCopy, int position, double glyphBase)
        {
            for (int k = glyphsDataCopy.Length - 1; k >= 0; k--)
            {
                var data = glyphsData[k];
                cursorPosition -= Math.Ceiling(data.Glyph.AdvanceWidth * scale);
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
                glyphsData.Add(new GlyphWordData(dotGlyph, '.', glyphRect, -1));

                cursorPosition += Math.Ceiling(dotGlyph.AdvanceWidth * scale);
            }
        }
    }

    public void Update(GraphicsDevice graphicsDevice)
    {
        if (!_textUpdated) return;
        
        FontAtlas ??= FontAtlasStore.GetOrCreateFrom(graphicsDevice, Typeface, 64);
        FontAtlas.Update(Text+".");
        ElementsCount = 0;
        VertexBuffer ??= Buffer.Vertex.New<FontItem>(graphicsDevice, MaxItemsCount);

        for (int i = 0; i < _wordData.Count; ++i)
        {
            var word = _wordData[i];
            if (word.Glyph == spaceGlyph) continue;
            var item = new FontItem
            {
                ArrangeRect = word.Rect,
                Source = FontAtlas.GetUVCoordinatesForGlyph(word.Glyph.Index),
                Depth = 1.0f
            };
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
        for (int k = 0; k < wordGlyphs.Length; ++k)
        {
            wordWidth += Math.Ceiling(wordGlyphs[k].BoundingRectangle.Width * scale);
        }

        return wordWidth;
    }

    private Rectangle CalculateGlyphPosition(
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

        var glyphWidth = Math.Ceiling(glyph.BoundingRectangle.Width * scale);
        var glyphHeight = Math.Ceiling(glyph.BoundingRectangle.Height * scale);
        var glyphTop = Math.Ceiling((glyphBase - glyphHeight) + verticalShift);
        if (position > 0)
        {
            glyphLeft += horizontalShift;
        }

        // if GPOS kern is applied - modify the advance for current glyph
        if (kernApplied && position > 0)
        {
            glyphLeft += fontSize * layoutContainer.GetAdvance((uint)position).X * scale;
        }

        return new Rectangle((int)glyphLeft, (int)glyphTop, (int)glyphWidth, (int)glyphHeight);
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