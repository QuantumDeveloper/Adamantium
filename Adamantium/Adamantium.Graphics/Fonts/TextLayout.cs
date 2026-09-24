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
    /// Emits a zero-width glyph for each newline, so a text editor can place the caret there. Skipped by rendering; off
    /// by default.
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
    /// How far (screen px) a glyph's effect (outline/glow/shadow) reaches beyond its body: the atlas margin at the
    /// current font size. 0 before the atlas is built.
    /// </summary>
    public int EffectPadding => FontAtlas == null
        ? 0
        : (int)Math.Ceiling(FontAtlas.GlyphMargin * FontSize / FontAtlas.MSDFTextureSize);

    public Size RealTextDimensions { get; private set; }

    private bool _textUpdated;

    // Behind the atlas version: glyphs have arrived since, and the quads must be built again.
    private int _atlasVersion = -1;
    private bool _vertexBufferDirty;

    public TextLayout(Typeface typeface, IFont font)
    {
        Guid = Guid.NewGuid();
        Typeface = typeface;
        Font = font;
        spaceGlyph = font.GetGlyphByCharacter(' ');
        dotGlyph = font.GetGlyphByCharacter('.');

        layoutContainer = new GlyphLayoutContainer(typeface, font);
        // Grown to fit: preallocating the 4096 cap cost 256KB per TextBlock.
        fontItems = Array.Empty<FontItem>();
    }

    private void EnsureItemCapacity(int needed)
    {
        if (needed <= fontItems.Length) return;

        var size = Math.Max(16, fontItems.Length);
        while (size < needed) size *= 2;
        Array.Resize(ref fontItems, (int)Math.Min(size, MaxItemsCount));
    }

    public GlyphWordData[] GetTextData()
    {
        return _wordData.ToArray();
    }

    // Takes the list explicitly: it is called mid-shaping, on data not yet published as _wordData.
    private void CalculateRealTextDimensions(List<GlyphWordData> glyphsData)
    {
        var minX = glyphsData.Min(x => x.Rect.Left);
        var maxX = glyphsData.Max(x => x.Rect.Right);
        var minY = glyphsData.Min(x => x.Rect.Top);
        var maxY = glyphsData.Max(x => x.Rect.Bottom);
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

    /// <summary>What laying out one string allocates, by stage. Cumulative.</summary>
    public static long TranslateBytes;
    public static long FeatureBytes;
    public static long WordLoopBytes;
    public static long TailBytes;
    public static int ProcessCount;

    public Size ProcessText(string text, double fontSize, TextRenderingParameters renderingParameters)
    {
        // Empty text clears the previous glyphs, or a recycled row keeps drawing its old text.
        if (string.IsNullOrEmpty(text))
        {
            _wordData?.Clear();
            ElementsCount = 0;
            CalculatedLayoutSize = Size.Zero;
            RealTextDimensions = Size.Zero;
            _textUpdated = true;
            _vertexBufferDirty = true;
            return Size.Zero;
        }

        RenderingParameters = renderingParameters;

        var _b0 = System.GC.GetAllocatedBytesForCurrentThread();


        var glyphs = Font.TranslateIntoGlyphs(text);
        layoutContainer.SetText(text);
        var _b1 = System.GC.GetAllocatedBytesForCurrentThread();

        var kernApplied = Font.FeatureService.ApplyFeature(Features.kern, layoutContainer, 0, (uint)glyphs.Count);
        // var subApp = font.FeatureService.ApplyFeature(Features.aalt, layoutContainer, 0, (uint)glyphs.Length);

        var _b2 = System.GC.GetAllocatedBytesForCurrentThread();


        var scale = fontSize / Font.UnitsPerEm;

        // Kern between this glyph and the next: GPOS stores it on the first glyph's advance, otherwise the legacy
        // 'kern' table. Added to the pen, so it carries to every following glyph.
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
                // Sub-pixel like the glyphs, so the space doesn't inflate the line bounds.
                var rect = new RectangleF((float)cursorPosition,
                    (float)(height + baseLine),
                    (float)spaceWidth,
                    0f);
                glyphsData.Add(new GlyphWordData(spaceGlyph, ' ',
                    rect,
                    -1,
                    lineIndex));
                cursorPosition += spaceWidth + KernAdvance(positionInString);
                positionInString++;
            }
        }

        // Not published yet: the render thread reads _wordData, and the alignment below still moves every glyph.
        // The height is a font metric (last baseline plus descent), not the ink, so same-size strings measure alike and
        // a turned label keeps its descenders.
        var lastBaseline = height + baseLine;
        height = lastBaseline + System.Math.Abs(Font.Descender) * scale;

        var _b3 = System.GC.GetAllocatedBytesForCurrentThread();


        CalculateRealTextDimensions(glyphsData);

        var maxX = glyphsData.Max(x => x.Rect.Right);
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

        var _b4 = System.GC.GetAllocatedBytesForCurrentThread();

        TranslateBytes += _b1 - _b0; FeatureBytes += _b2 - _b1; WordLoopBytes += _b3 - _b2; TailBytes += _b4 - _b3; ProcessCount++;

        // One reference write: a reader sees the previous layout or this one, never a half-aligned mixture.
        _wordData = glyphsData;

        CalculatedLayoutSize = finalRect;

        return CalculatedLayoutSize;

        bool ProcessWord(string word)
        {
            var wordWidth = GetWordWidth(scale, word);

            // Wrapped at the word boundary, before any glyph is laid: a word that does not fit starts a new line, and
            // a word wider than the line only overflows as the first on its line.
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
                        }
                        break;
                    }
                }
                // In lockstep with layoutContainer's glyph stream, so GetAdvance(pos) lines up.
                positionInString++;
            }
            return true;
        }

        void ArrangeText()
        {
            var minX = glyphsData.Min(x => x.Rect.Left);
            var maxX = glyphsData.Max(x => x.Rect.Right);
            var minY = glyphsData.Min(x => x.Rect.Top);
            switch (renderingParameters.HorizontalTextAlignment)
            {
                case HorizontalTextAlignment.Center:
                {
                    var maxLines = glyphsData.Max(x => x.LineIndex);
                    for (int i = 0; i <= maxLines; ++i)
                    {
                        var glyphsForLine = glyphsData.Where(x => x.LineIndex == i).ToArray();
                        if (glyphsForLine.Length == 0) break;

                        // By the ink, so leading/trailing spaces don't pull the line off-center.
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
                    var maxLines = glyphsData.Max(x => x.LineIndex);
                    for (int i = 0; i <= maxLines; ++i)
                    {
                        var glyphsForLine = glyphsData.Where(x => x.LineIndex == i).ToArray();
                        if (glyphsForLine.Length == 0) break;

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
                    var maxLines = glyphsData.Max(x => x.LineIndex);
                    for (int i = 0; i <= maxLines; ++i)
                    {
                        // The last line stays ragged unless JustifyLastLine asks otherwise.
                        if (i == maxLines && !renderingParameters.JustifyLastLine) break;

                        // Only the gaps between words widen; glyphs are shifted, never re-laid, so kerning and bearings stay.
                        var lineGlyphs = glyphsData.Where(x => x.LineIndex == i).OrderBy(x => x.Rect.X).ToArray();
                        if (lineGlyphs.Length == 0) continue;

                        int firstInk = -1, lastInk = -1;
                        for (int k = 0; k < lineGlyphs.Length; k++)
                        {
                            if (lineGlyphs[k].Symbol == ' ') continue;
                            if (firstInk < 0) firstInk = k;
                            lastInk = k;
                        }
                        if (firstInk < 0) continue;

                        var spaceCount = 0;
                        for (int k = firstInk + 1; k < lastInk; k++)
                            if (lineGlyphs[k].Symbol == ' ') spaceCount++;
                        if (spaceCount == 0) continue;

                        var extra = finalRect.Width - lineGlyphs[lastInk].Rect.Right;
                        if (extra <= 0) continue;

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
                    // Centred by ascent above the baseline, not the ink: ink differs per string and made same-size
                    // text wobble. No descent reserve, so descenders hang below.
                    var lineCount = glyphsData.Max(x => x.LineIndex) + 1;
                    var ascent = Font.Ascender * scale;
                    var blockHeight = (lineCount - 1) * lineHeight + ascent;
                    var blockTop = baseLine - ascent;
                    var diff = (finalRect.Height - blockHeight) / 2 - blockTop;
                    foreach (var glyphWordData in glyphsData)
                    {
                        var rect = glyphWordData.Rect;
                        rect.Y += (float)diff;
                        glyphWordData.Rect = rect;
                    }
                }
                break;
                case VerticalTextAlignment.Bottom:
                {
                    // The last baseline on the bottom edge, not the lowest ink, for the same reason.
                    var diff = finalRect.Height - lastBaseline;
                    foreach (var glyphWordData in glyphsData)
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

    /// <summary>Glyphs have landed in the atlas since this block built its quads; the render side rebuilds on this.</summary>
    public bool NeedsGlyphRefresh => FontAtlas != null && (FontAtlas.IsDisposed || FontAtlas.Version != _atlasVersion);

    /// <summary>Re-run the quad build against the atlas as it stands now (see <see cref="NeedsGlyphRefresh"/>).</summary>
    public void RefreshGlyphs(IGraphicsDevice graphicsDevice)
    {
        _textUpdated = true;
        Update(graphicsDevice);
    }

    /// <summary>This block's atlas, created on first use and again when its device is gone.</summary>
    public FontAtlas EnsureAtlas(IGraphicsDevice graphicsDevice)
    {
        if (FontAtlas == null || FontAtlas.IsDisposed)
        {
            FontAtlas = FontAtlasStore.GetOrCreateFrom(graphicsDevice, Typeface,
                FontParameters.Default(sortingVariant: GlyphSortingVariant.ByIndex));
        }

        return FontAtlas;
    }

    public void Update(IGraphicsDevice graphicsDevice)
    {
        // Built before its first measure (a popup built ahead of its layout pass): retried next frame.
        if (!_textUpdated || _wordData == null) return;

        // Asked for, not waited for: glyphs land over the next frames and bump the atlas version (NeedsGlyphRefresh).
        var atlas = EnsureAtlas(graphicsDevice);
        atlas.RequestAsync(Text + ".");
        _atlasVersion = atlas.Version;
        ElementsCount = 0;
        // No vertex buffer here: only the direct draw path needs one (EnsureVertexBuffer), and one per block ran the BAR
        // heap out of memory.

        for (int i = 0; i < _wordData.Count; ++i)
        {
            var word = _wordData[i];
            if (word.Glyph == spaceGlyph || word.Symbol == '\n') continue;

            // The full cell (body plus margin), so outline/glow/shadow have room to draw.
            var gd = FontAtlas.GetGlyphData(word.Glyph.Index);
            FontItem item;
            if (gd != null && gd.BoundingRect.Width > 0 && gd.BoundingRect.Height > 0)
            {
                var rect = word.Rect;
                // Per-axis margin maps the body exactly onto the pixel-snapped rect; a zero-size side falls back to the
                // uniform margin, or the quad collapses.
                var uniform = gd.Margin * (double)FontSize / FontAtlas.MSDFTextureSize;
                var mx = rect.Width > 0 ? gd.Margin * (double)rect.Width / gd.BoundingRect.Width : uniform;
                var my = rect.Height > 0 ? gd.Margin * (double)rect.Height / gd.BoundingRect.Height : uniform;
                item = new FontItem
                {
                    ArrangeRect = new Vector4F((float)(rect.X - mx), (float)(rect.Y - my),
                        (float)(rect.Width + 2 * mx), (float)(rect.Height + 2 * my)),
                    Source = gd.UVRectFull,
                    Layer = gd.DepthLayer,
                    Depth = 1.0f
                };
            }
            else if (gd != null)
            {
                item = new FontItem
                {
                    ArrangeRect = word.Rect,
                    Source = FontAtlas.GetUVCoordinatesForGlyph(word.Glyph.Index),
                    Layer = gd.DepthLayer,
                    Depth = 1.0f
                };
            }
            else
            {
                // Not rasterized yet: no quad, or it draws a piece of a neighbour.
                continue;
            }
            if (ElementsCount >= MaxItemsCount) break;
            EnsureItemCapacity((int)ElementsCount + 1);
            fontItems[ElementsCount] = item;
            ElementsCount++;
        }

        _vertexBufferDirty = true;

        _textUpdated = true;
    }

    /// <summary>
    /// Creates and uploads the per-block vertex buffer on demand; only the direct draw path (rotated/sheared text) uses it.
    /// </summary>
    public void EnsureVertexBuffer(IGraphicsDevice graphicsDevice)
    {
        VertexBuffer ??= Adamantium.Graphics.Buffer.Vertex.New<FontItem>(graphicsDevice, MaxItemsCount,
            Adamantium.Graphics.BufferMemoryUsage.UploadFromCpuToGpu);
        if (_vertexBufferDirty)
        {
            VertexBuffer.SetData(fontItems, 0, ElementsCount, 0);
            _vertexBufferDirty = false;
        }
    }

    /// <summary>Copies this block's glyphs into a shared batch, baked to world space with its color, so many blocks draw
    /// in one call. False for a rotated/sheared world (direct path instead) or when <paramref name="dest"/> is full.</summary>
    public bool TryBakeWorldGlyphs(FontItem[] dest, ref int count, Matrix4x4F world, Vector2F textAreaOffset, Vector4F color)
    {
        if (ElementsCount == 0) return true;

        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;
        if (count + (int)ElementsCount > dest.Length) return false;

        var sx = world.M11;
        var sy = world.M22;
        var tx = world.M41;
        var ty = world.M42;

        for (int i = 0; i < ElementsCount; i++)
        {
            var item = fontItems[i];
            var d = item.ArrangeRect;
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

    /// <summary>A frozen copy of the shaped glyphs (call after <see cref="Update"/>), so the render thread never reads
    /// this layout while it is reshaped.</summary>
    public FrozenGlyphRun SnapshotGlyphs()
    {
        var n = (int)ElementsCount;
        var copy = new FontItem[n];
        Array.Copy(fontItems, copy, n);
        return new FrozenGlyphRun(copy, n, FontAtlas, FontSize);
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
        var verticalShift = -glyph.BoundingRectangle.Y * scale;
        var horizontalShift = glyph.LeftSideBearing * scale;

        var glyphWidth = glyph.BoundingRectangle.Width * scale;
        var glyphHeight = glyph.BoundingRectangle.Height * scale;
        var glyphTop = (glyphBase - glyphHeight) + verticalShift;
        glyphLeft += horizontalShift;

        // Y snaps to shared baseline/ascender rows, rounded once, so round and flat letters sit on the same pixel row.
        var baseR = (int)System.Math.Round(glyphBase);
        var ascLine = glyphBase - Font.Ascender * scale;
        var ascR = (int)System.Math.Round(ascLine);
        var top = ascR + (int)System.Math.Round(glyphTop - ascLine);
        var bottom = baseR - (int)System.Math.Round(glyphBase - (glyphTop + glyphHeight));

        // X stays sub-pixel: snapping fractional advances made equal gaps render a pixel apart.
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
