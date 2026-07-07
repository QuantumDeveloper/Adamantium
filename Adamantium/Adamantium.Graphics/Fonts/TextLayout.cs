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

    /// <summary>
    /// When true, a newline (<c>\n</c>) is materialised as a zero-width glyph at the end of its line so a text editor
    /// can address a caret position for it (carrying the true post-wrap line index). It is skipped by rendering. Off by
    /// default so ordinary text blocks are completely unaffected.
    /// </summary>
    public bool EmitNewlineCarets { get; set; }

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
        VerticalTextAlignment verticalTextAlignment,
        bool justifyLastLine = false)
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
                JustifyLastLine = justifyLastLine,
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

        // Kerning (screen px) to add to the pen after the glyph at this global index = the kern between it
        // and the next glyph; applying it to the cursor propagates it to every following glyph. With GPOS the
        // feature stored the pair adjustment on the first glyph's advance (GetAdvance(pos)); without GPOS we
        // fall back to the legacy TTF 'kern' table. Bounds-guarded against the displayed-glyph count.
        double KernAdvance(int pos)
        {
            if (pos < 0 || pos >= (int)layoutContainer.Count) return 0;
            if (kernApplied)
                return layoutContainer.GetAdvance((uint)pos).X * scale;
            if (pos + 1 < (int)layoutContainer.Count)
                return Font.GetKerningValue(
                    (ushort)layoutContainer.GetGlyph(pos).Index,
                    (ushort)layoutContainer.GetGlyph(pos + 1).Index) * scale;
            return 0;
        }

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
                // Sub-pixel like the glyphs (no Ceiling) so the space doesn't inflate the line bounds.
                var rect = new RectangleF((float)cursorPosition,
                    (float)(height + baseLine),
                    (float)spaceWidth,
                    0f); 
                // add space after word
                glyphsData.Add(new GlyphWordData(spaceGlyph, ' ',
                    rect,
                    -1,
                    lineIndex));
                cursorPosition += spaceWidth + KernAdvance(positionInString);
                positionInString++; // the space is one glyph in the displayed-glyph stream
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

            // WrapByWords decides at the WORD boundary, BEFORE laying any glyph: if the whole word won't fit in
            // what's left of the current line (and the line already has content), advance to a fresh line and place
            // the word there in one pass. A word wider than the entire line only wraps when the line is non-empty; as
            // the first word on a line it simply overflows, because an unbreakable word can't be split. Deciding
            // up-front removes both the old per-glyph re-check (which re-fired on every glyph of an over-wide word and
            // exploded the block vertically) and the post-hoc RearrangeData shuffle (whose walk-back mis-handled a
            // leading over-wide word and left everything on one overflowing line).
            if (renderingParameters.TextWrapping == TextWrapping.WrapByWords
                && wordIndex > 0
                && cursorPosition > 0
                && cursorPosition + wordWidth > textArea.Width
                && height + lineHeight < textArea.Height)
            {
                lineIndex++;
                height += lineHeight;
                cursorPosition = 0;
            }

            wordStartPosition = cursorPosition;
            for (var i = 0; i < word.Length; i++)
            {
                var symbol = word[i];
                var glyph = Font.GetGlyphByCharacter(symbol);
                switch (symbol)
                {
                    case '\n':
                        // With caret sentinels on (text editor), emit a zero-width glyph at the end of the current
                        // line so the newline has a caret-addressable position carrying the true (post-wrap) line
                        // index. Rendering skips it (Symbol == '\n', like a space). Then advance to the next line.
                        if (EmitNewlineCarets)
                        {
                            var caretRect = new RectangleF((float)cursorPosition, (float)(height + baseLine), 0f, 0f);
                            glyphsData.Add(new GlyphWordData(glyph, '\n', caretRect, positionInString, lineIndex));
                        }
                        height += lineHeight;
                        cursorPosition = 0;
                        lineIndex++;
                        break;
                    default:
                    {
                        var glyphLeft = cursorPosition;
                        var glyphBase = height + baseLine;

                        var glyphRect = CalculateGlyphPosition(glyph,
                            glyphLeft,
                            glyphBase,
                            scale);

                        // Advance the pen by this glyph's advance plus its GPOS kern with the next glyph
                        // (GetAdvance is stored on the first glyph of the pair); adding it to the cursor
                        // propagates the kern to every following glyph.
                        cursorPosition += glyph.AdvanceWidth * scale + KernAdvance(positionInString);

                        glyphsData.Add(new GlyphWordData(glyph, symbol, glyphRect, positionInString, lineIndex));

                        switch (renderingParameters.TextWrapping)
                        {
                            case TextWrapping.NoWrap:
                                if (cursorPosition > textArea.Width)
                                {
                                    if (!IsLastGlyph(positionInString, text.Length))
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
                                        else if (!IsLastGlyph(positionInString, text.Length))
                                        {
                                            PrepareDataAndTrim(glyphsDataCopy, i, glyphBase);
                                            return false;
                                        }
                                    }
                                }
                                break;
                            // WrapByWords is handled at the word boundary in ProcessWord (see above), not per glyph.
                        }
                        break;
                    }
                }
                // One displayed glyph consumed (letter, space or newline) - keep the global index in lockstep
                // with layoutContainer's glyph stream so GPOS GetAdvance(pos) lines up.
                positionInString++;
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

                        // Centre by the INK extent (ignore leading/trailing spaces) so they don't pull the
                        // line off-centre - matching how Right alignment measures. diff places the ink left at
                        // exactly (areaWidth - inkWidth)/2.
                        var ink = glyphsForLine.Where(x => x.Symbol != ' ').ToArray();
                        if (ink.Length == 0) continue;
                        minX = ink.Min(x => x.Rect.Left);
                        maxX = ink.Max(x => x.Rect.Right);
                        var lineWidth = maxX - minX;
                        var diff = (finalRect.Width - lineWidth) / 2 - minX;
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
                        // The last line of a justified block stays ragged (left-aligned), per typography
                        // convention - so a single line is never stretched either. Opt out via
                        // JustifyLastLine (text-align-last) to stretch the last/only line as well.
                        if (i == maxLines && !renderingParameters.JustifyLastLine) break;

                        // Word-spacing justification: widen the gaps BETWEEN words to fill the line, leaving
                        // each word's internal layout (letter spacing + kerning) untouched. We only SHIFT
                        // glyphs - never re-lay them - so sub-pixel positions and side bearings stay correct
                        // (the old code re-laid every glyph: int-truncated the pen and dropped the first LSB).
                        var lineGlyphs = _wordData.Where(x => x.LineIndex == i).OrderBy(x => x.Rect.X).ToArray();
                        if (lineGlyphs.Length == 0) continue;

                        int firstInk = -1, lastInk = -1;
                        for (int k = 0; k < lineGlyphs.Length; k++)
                        {
                            if (lineGlyphs[k].Symbol == ' ') continue;
                            if (firstInk < 0) firstInk = k;
                            lastInk = k;
                        }
                        if (firstInk < 0) continue; // line has no ink to justify

                        // expandable gaps = spaces strictly between the first and last ink glyph
                        var spaceCount = 0;
                        for (int k = firstInk + 1; k < lastInk; k++)
                            if (lineGlyphs[k].Symbol == ' ') spaceCount++;
                        if (spaceCount == 0) continue; // single word - nothing to stretch

                        var extra = finalRect.Width - lineGlyphs[lastInk].Rect.Right;
                        if (extra <= 0) continue; // already fills / overflows the line

                        var perSpace = extra / spaceCount;
                        double shift = 0;
                        for (int k = 0; k < lineGlyphs.Length; k++)
                        {
                            var rect = lineGlyphs[k].Rect;
                            rect.X += (float)shift;
                            lineGlyphs[k].Rect = rect;
                            if (k > firstInk && k < lastInk && lineGlyphs[k].Symbol == ' ')
                                shift += perSpace;
                        }
                    }
                }
                break;
            }
            
            switch (renderingParameters.VerticalTextAlignment)
            {
                case VerticalTextAlignment.Center:
                {
                    // Centre by a GLYPH-INDEPENDENT reference box (the ascent above the baseline per line), NOT this
                    // string's ink bounding box. Ink extents differ between strings (ascenders/descenders/round
                    // overshoot), so ink-centring shifted the baseline a pixel or two per string and same-size text
                    // wobbled as content changed (visible scrolling a DropDown). We deliberately measure ascent-to-
                    // baseline only (NO descent reserve below): almost all UI labels have no descender, and reserving
                    // descent space would push the optical centre a couple pixels high. Descenders simply hang below,
                    // as they should - the caps/x-height stay put regardless of the exact characters.
                    var lineCount = _wordData.Max(x => x.LineIndex) + 1;
                    var ascent = Font.Ascender * scale;
                    var blockHeight = (lineCount - 1) * lineHeight + ascent;
                    var blockTop = baseLine - ascent;                      // line 0's ascent top in the current coords
                    var diff = (finalRect.Height - blockHeight) / 2 - blockTop;
                    foreach (var glyphWordData in _wordData)
                    {
                        var rect = glyphWordData.Rect;
                        rect.Y += (float)diff;
                        glyphWordData.Rect = rect;
                    }
                }
                break;
                case VerticalTextAlignment.Bottom:
                {
                    // Drop the block so its lowest point sits on the area's bottom edge. (Top is the default
                    // no-op layout.)
                    var diff = finalRect.Height - maxY;
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
                    scale);

                glyphData.Rect = glyphRect;
                glyphData.LineIndex = lineIndex;
                // Same GPOS kern as the first-pass layout, so wrapped lines stay kerned. Spaces carry
                // PositionInString = -1, for which KernAdvance returns 0.
                cursorPosition += glyphData.Glyph.AdvanceWidth * scale + KernAdvance(glyphData.PositionInString);
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
                    scale);
                glyphsData.Add(new GlyphWordData(dotGlyph, '.', glyphRect, -1, lineIndex));

                cursorPosition += dotGlyph.AdvanceWidth * scale;
            }
        }
    }

    public void Update(IGraphicsDevice graphicsDevice)
    {
        // Nothing to upload until ProcessText has shaped the text into _wordData. A render unit can be built for a text
        // component in the SAME frame its text changed but BEFORE it was measured/shaped (a popup added + built before
        // its layout pass ran), so _textUpdated is set but _wordData is still null - guard it, and retry next frame
        // (_textUpdated stays set) once ProcessText has run.
        if (!_textUpdated || _wordData == null) return;

        FontAtlas ??= FontAtlasStore.GetOrCreateFrom(graphicsDevice, Typeface, FontParameters.Default(sortingVariant:GlyphSortingVariant.ByIndex));
        FontAtlas.Update(Text+".");
        ElementsCount = 0;
        // Rewritten via SetData when text changes → CPU-to-GPU upload (BAR window), not device-local.
        VertexBuffer ??= Adamantium.Graphics.Buffer.Vertex.New<FontItem>(graphicsDevice, MaxItemsCount,
            Adamantium.Graphics.BufferMemoryUsage.UploadFromCpuToGpu);

        for (int i = 0; i < _wordData.Count; ++i)
        {
            var word = _wordData[i];
            if (word.Glyph == spaceGlyph || word.Symbol == '\n') continue;

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

    // CPU pre-transform text batch (docs/TEXT_GLYPH_BATCH_PLAN.md §9 Stage 2): copy this block's glyphs into a shared
    // aggregate buffer with their positions baked to WORLD space and the block's foreground written per glyph, so many
    // blocks render in ONE instanced draw (the batch VS then applies only a static Projection). A glyph's local quad
    // g -> (g + textAreaOffset) * world. Only translation + axis-aligned scale can be folded into the axis-aligned
    // FontItem rect; a rotated/sheared world (non-zero off-diagonal) would need 4 baked corners - returns false so the
    // caller renders that block via the per-block direct draw instead (Stage 1). Also false (no state change) if it
    // would overflow dest, so the caller can flush the current batch and retry. Row-vector convention (see FontEffect.fx).
    public bool TryBakeWorldGlyphs(FontItem[] dest, ref int count, Matrix4x4F world, Vector2F textAreaOffset, Vector4F color)
    {
        if (ElementsCount == 0) return true;   // nothing to contribute

        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;   // rotation/shear -> direct path
        if (count + (int)ElementsCount > dest.Length) return false;                 // caller flushes + retries

        var sx = world.M11;
        var sy = world.M22;
        var tx = world.M41;
        var ty = world.M42;

        for (int i = 0; i < ElementsCount; i++)
        {
            var item = fontItems[i];
            var d = item.ArrangeRect;   // local x, y, w, h
            item.ArrangeRect = new Vector4F(
                (d.X + textAreaOffset.X) * sx + tx,
                (d.Y + textAreaOffset.Y) * sy + ty,
                d.Z * sx,
                d.W * sy);
            item.Color = color;
            dest[count++] = item;
        }
        return true;
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
        double scale)
    {
        // Kerning is applied to the pen in ProcessWord (GPOS GetAdvance, propagated), not here.
        var verticalShift = -glyph.BoundingRectangle.Y * scale;
        var horizontalShift = glyph.LeftSideBearing * scale;

        var glyphWidth = glyph.BoundingRectangle.Width * scale;
        var glyphHeight = glyph.BoundingRectangle.Height * scale;
        var glyphTop = (glyphBase - glyphHeight) + verticalShift;
        // Apply the left side bearing for EVERY glyph. Previously the first glyph skipped it, so its ink was
        // shifted left while the pen still advanced by the full width - the gap after the first letter came
        // out inflated by that bearing. Now the line just starts at the natural LSB indent.
        glyphLeft += horizontalShift;

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