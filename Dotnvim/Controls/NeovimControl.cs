// <copyright file="NeovimControl.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Dotnvim.Controls.Utilities;
    using SharpDX;
    using SharpDX.Direct2D1;
    using SharpDX.Mathematics.Interop;
    using static Dotnvim.NeovimClient.NeovimClient;
    using D2D = SharpDX.Direct2D1;
    using D3D = SharpDX.Direct3D;
    using D3D11 = SharpDX.Direct3D11;
    using DWrite = SharpDX.DirectWrite;
    using DXGI = SharpDX.DXGI;

    /// <summary>
    /// The neovim control.
    /// </summary>
    public class NeovimControl : ControlBase
    {
        private readonly NeovimClient.NeovimClient neovimClient;
        private readonly DWrite.TextAnalyzer textAnalyzer;
        private readonly DWrite.Factory factoryDWrite = new DWrite.Factory();

        private readonly CursorEffects cursorEffects;
        private readonly DxItemCache dxItemCache;

        private string fontName = "Fira Code";
        private float fontPoint = 11;
        private float lineHeight;
        private float charWidth;

        /// <summary>
        /// Initializes a new instance of the <see cref="NeovimControl"/> class.
        /// </summary>
        /// <param name="parent">The parent control.</param>
        /// <param name="neovimClient">the neovim client.</param>
        public NeovimControl(IElement parent, NeovimClient.NeovimClient neovimClient)
            : base(parent)
        {
            this.neovimClient = neovimClient;
            this.neovimClient.Redraw += this.Invalidate;

            using (var textFormat = new DWrite.TextFormat(this.factoryDWrite, this.fontName, DWrite.FontWeight.Normal, DWrite.FontStyle.Normal, (float)Helpers.GetFontSize(this.fontPoint)))
            using (var textLayout = new DWrite.TextLayout(this.factoryDWrite, "A", textFormat, 1000, 1000))
            {
                this.lineHeight = Helpers.AlignToPixel(textLayout.Metrics.Height, this.Factory.DesktopDpi.Height);
                this.charWidth = Helpers.AlignToPixel(textLayout.OverhangMetrics.Left + (1000 + textLayout.OverhangMetrics.Right), this.Factory.DesktopDpi.Width);
            }

            this.textAnalyzer = new DWrite.TextAnalyzer(this.factoryDWrite);
            this.cursorEffects = new CursorEffects(this.DeviceContext);
            this.dxItemCache = new DxItemCache(this.factoryDWrite);
        }

        /// <inheritdoc />
        public override Size2F Size
        {
            get
            {
                return base.Size;
            }

            set
            {
                if (value.Width == 0)
                {
                    value.Width = 1;
                }

                if (value.Height == 0)
                {
                    value.Height = 1;
                }

                if (base.Size != value)
                {
                    base.Size = value;
                    this.neovimClient.TryResize(this.DesiredColCount, this.DesiredRowCount);
                }
            }
        }

        /// <summary>
        /// Gets the desired row count.
        /// </summary>
        public uint DesiredRowCount
        {
            get
            {
                var c = (uint)(this.Size.Height / this.lineHeight);
                if (c == 0)
                {
                    c = 1;
                }

                return c;
            }
        }

        /// <summary>
        /// Gets the desired col count.
        /// </summary>
        public uint DesiredColCount
        {
            get
            {
                var c = (uint)(this.Size.Width / this.charWidth);
                if (c == 0)
                {
                    c = 1;
                }

                return c;
            }
        }

        /// <inheritdoc />
        protected override EffectChain PostEffects => this.cursorEffects;

        /// <summary>
        /// Input text to neovim.
        /// </summary>
        /// <param name="text">the input text sequence.</param>
        public void Input(string text)
        {
            this.neovimClient.Input(text);
        }

        /// <inheritdoc />
        protected override void Draw()
        {
            var args = this.neovimClient.GetRedrawArgs();

            if (args == null)
            {
                return;
            }

            this.DeviceContext.BeginDraw();
            this.DeviceContext.Clear(new RawColor4(0, 0, 0, 0));

            // Paint the background
            for (int i = 0; i < args.Screen.GetLength(0); i++)
            {
                for (int j = 0; j < args.Screen.GetLength(1); j++)
                {
                    if (args.Screen[i, j].BackgroundColor != args.BackgroundColor || args.Screen[i, j].Reverse)
                    {
                        var x = j * this.charWidth;
                        var y = i * this.lineHeight;

                        var rect = new RawRectangleF(x, y, x + this.charWidth, y + this.lineHeight);
                        int color = args.Screen[i, j].Reverse ? args.Screen[i, j].ForegroundColor : args.Screen[i, j].BackgroundColor;
                        var brush = this.dxItemCache.GetBrush(this.DeviceContext, color);
                        this.DeviceContext.FillRectangle(rect, brush);
                    }
                }
            }

            // Paint the foreground
            for (int i = 0; i < args.Screen.GetLength(0); i++)
            {
                int j = 0;

                while (j < args.Screen.GetLength(1))
                {
                    // Cells with same style should be analyzed together.
                    // This prevents the inproper ligature in <html>=
                    // Of course, it relies on enabling the syntax.
                    int cellRangeStart = j;
                    int cellRangeEnd = j;
                    Cell startCell = args.Screen[i, cellRangeStart];
                    while (true)
                    {
                        if (cellRangeEnd == args.Screen.GetLength(1))
                        {
                            break;
                        }

                        Cell cell = args.Screen[i, cellRangeEnd];
                        if (cell.Character != null
                            && (cell.ForegroundColor != startCell.ForegroundColor
                                || cell.BackgroundColor != startCell.BackgroundColor
                                || cell.SpecialColor != startCell.SpecialColor
                                || cell.Italic != startCell.Italic
                                || cell.Bold != startCell.Bold
                                || cell.Reverse != startCell.Reverse
                                || cell.Undercurl != startCell.Undercurl
                                || cell.Underline != startCell.Underline))
                        {
                            break;
                        }

                        cellRangeEnd++;
                    }

                    j = cellRangeEnd;

                    var fontWeight = args.Screen[i, cellRangeStart].Bold ? DWrite.FontWeight.Bold : DWrite.FontWeight.Normal;
                    var fontStyle = args.Screen[i, cellRangeStart].Italic ? DWrite.FontStyle.Italic : DWrite.FontStyle.Normal;

                    int cellIndex = cellRangeStart;
                    using (var textSink = new TextAnalysisSink())
                    using (var textSource = new RowTextSource(this.factoryDWrite, args.Screen, i, cellRangeStart, cellRangeEnd))
                    {
                        this.textAnalyzer.AnalyzeScript(textSource, 0, textSource.Length, textSink);

                        // The result of AalyzeScript may cut the text into several ranges,
                        // and in each range the text's scripts are different.
                        foreach (var (codePointStart, codePointLength, scriptAnalysis) in textSink.ScriptAnalyses)
                        {
                            var glyphBufferLength = (codePointLength * 3 / 2) + 16;
                            var clusterMap = new short[codePointLength];
                            var textProperties = new DWrite.ShapingTextProperties[codePointLength];
                            short[] indices;
                            DWrite.ShapingGlyphProperties[] shapingProperties;
                            var fontFace = this.dxItemCache.GetPrimaryFontFace(fontWeight, fontStyle);
                            int actualGlyphCount;

                            // We don't know how many glyphs the text have. TextLength * 3 / 2 + 16
                            // is an empirical estimation suggested by MSDN. So using a loop to detect
                            // the actual glyph count.
                            while (true)
                            {
                                indices = new short[glyphBufferLength];
                                shapingProperties = new DWrite.ShapingGlyphProperties[glyphBufferLength];
                                try
                                {
                                    var str = textSource.GetSubString(codePointStart, codePointLength);
                                    this.textAnalyzer.GetGlyphs(
                                        str,
                                        str.Length,
                                        fontFace,
                                        false,
                                        false,
                                        scriptAnalysis,
                                        null,
                                        null,
                                        null,
                                        null,
                                        glyphBufferLength,
                                        clusterMap,
                                        textProperties,
                                        indices,
                                        shapingProperties,
                                        out actualGlyphCount);
                                    break;
                                }
                                catch (SharpDX.SharpDXException e)
                                {
                                    const int ERROR_INSUFFICIENT_BUFFER = 122;
                                    if (e.ResultCode == SharpDX.Result.GetResultFromWin32Error(ERROR_INSUFFICIENT_BUFFER))
                                    {
                                        glyphBufferLength *= 2;
                                    }
                                }
                            }

                            for (int codePointIndex = 0, glyphIndex = 0; codePointIndex < codePointLength;)
                            {
                                // var fontWeight = args.Screen[i, cellIndex].Bold ? DWrite.FontWeight.Bold : DWrite.FontWeight.Normal;
                                // var fontStyle = args.Screen[i, cellIndex].Italic ? DWrite.FontStyle.Italic : DWrite.FontStyle.Normal;
                                var foregroundColor = args.Screen[i, cellIndex].Reverse ? args.Screen[i, cellIndex].BackgroundColor : args.Screen[i, cellIndex].ForegroundColor;
                                var foregroundBrush = this.dxItemCache.GetBrush(this.DeviceContext, foregroundColor);
                                var fontFace2 = fontFace;
                                short[] indices2;

                                int cellWidth = 0;
                                int codePointCount = 0;
                                int glyphCount = 0;

                                if (indices[glyphIndex] == 0)
                                {
                                    // If the primary font doesn't have the glyph, get a font from system font fallback.
                                    // Ligatures for fallback fonts are not supported yet.
                                    int codePoint = args.Screen[i, cellIndex].Character.Value;
                                    fontFace2 = this.dxItemCache.GetFontFace(codePoint, fontWeight, fontStyle);
                                    indices2 = fontFace2.GetGlyphIndices(new int[] { codePoint });
                                    glyphCount = indices2.Length;

                                    // NativeInterop.Methods.wcwidth(textSource.GetCodePoint(codePointStart + codePointIndex));
                                    cellWidth = this.GetCharWidth(args.Screen, i, cellIndex);
                                    codePointCount = 1;
                                }
                                else
                                {
                                    // The cluster map stores the information about the codepoint-glyph mapping.
                                    // If several codepoints share the same glyph (e.g. ligature), then they will
                                    // have the same value in clusterMap.
                                    // If one code point has several corresponding glyphs, then for the next codepoint,
                                    // the value in clusterMap will bump higher.
                                    // Example:  CodePointLength = 5, GlyphCount = 5, clusterMap = [0, 1, 1, 2, 4] means:
                                    // Codepoint Index    Glyph Index
                                    //       0   -----------   0
                                    //       1   -----------   1
                                    //       2   ----------/
                                    //       3   -----------   2
                                    //           \----------   3
                                    //       4   -----------   4
                                    var cluster = clusterMap[codePointIndex];
                                    int nextCluster = cluster;
                                    while (true)
                                    {
                                        if (codePointIndex + codePointCount == clusterMap.Length)
                                        {
                                            nextCluster++;
                                            break;
                                        }

                                        nextCluster = clusterMap[codePointIndex + codePointCount];
                                        if (cluster != nextCluster)
                                        {
                                            break;
                                        }

                                        // NativeInterop.Methods.wcwidth(textSource.GetCodePoint(codePointStart + codePointIndex + codePointCount));
                                        cellWidth += this.GetCharWidth(args.Screen, i, cellIndex + cellWidth);
                                        codePointCount++;
                                    }

                                    glyphCount = nextCluster - cluster;
                                    indices2 = new short[glyphCount];
                                    for (int c = 0; c < glyphCount; c++)
                                    {
                                        indices2[c] = indices[glyphIndex];
                                    }
                                }

                                using (var glyphrun = new DWrite.GlyphRun
                                {
                                    FontFace = fontFace2,
                                    Advances = null,
                                    BidiLevel = 0,
                                    FontSize = Helpers.GetFontSize(this.fontPoint),
                                    Indices = indices2,
                                    IsSideways = false,
                                    Offsets = null,
                                })
                                {
                                    var origin = new RawVector2(this.charWidth * cellIndex, (this.lineHeight * i) + (this.lineHeight * 0.8f));
                                    this.DeviceContext.DrawGlyphRun(origin, glyphrun, foregroundBrush, D2D.MeasuringMode.Natural);
                                    glyphrun.FontFace = null;
                                }

                                cellIndex += cellWidth;
                                codePointIndex += codePointCount;
                                glyphIndex += glyphCount;
                            }
                        }
                    }
                }
            }

            this.DeviceContext.EndDraw();

            var cursorWidth = this.GetCharWidth(args.Screen, args.CursorPosition.Row, args.CursorPosition.Col);
            var cursorRect = new RawRectangleF()
            {
                Left = args.CursorPosition.Col * this.charWidth,
                Top = args.CursorPosition.Row * this.lineHeight,
                Right = (args.CursorPosition.Col + cursorWidth) * this.charWidth,
                Bottom = (args.CursorPosition.Row + 1) * this.lineHeight,
            };

            this.cursorEffects.SetCursorRect(cursorRect);
        }

        /// <inheritdoc />
        protected override void DisposeManaged()
        {
            base.DisposeManaged();

            this.cursorEffects.Dispose();
            this.dxItemCache.Dispose();
        }

        private int GetCharWidth(Cell[,] screen, int row, int col)
        {
            if (col >= screen.GetLength(1) - 1)
            {
                return 1;
            }

            if (screen[row, col + 1].Character == null)
            {
                return 2;
            }

            return 1;
        }

        private sealed class DxItemCache : IDisposable
        {
            private readonly Dictionary<int, D2D.SolidColorBrush> brushCache
                = new Dictionary<int, D2D.SolidColorBrush>();

            private readonly DWrite.Factory factory;
            private readonly DWrite.FontCollection fontCollection;

            private readonly DWrite.FontFallback fontFallback;
            private readonly List<FontFamilyCacheItem> fallbackFontFamilies = new List<FontFamilyCacheItem>();

            private string baseFontFamilyName;

            public DxItemCache(DWrite.Factory factory)
            {
                this.factory = factory;
                using (var factoryDWrite = factory.QueryInterface<DWrite.Factory2>())
                {
                    this.fontCollection = factoryDWrite.GetSystemFontCollection(false);
                    this.fontFallback = factoryDWrite.SystemFontFallback;
                    this.SetFallbackFontFamilyCache(new List<string>() { "Fira Code" });
                }
            }

            public void Dispose()
            {
                this.ClearBrushCache();
                this.ClearFallbackFontCache();
                this.fontCollection.Dispose();
            }

            public D2D.SolidColorBrush GetBrush(D2D.DeviceContext dc, int color)
            {
                if (this.brushCache.TryGetValue(color, out var brush))
                {
                    return brush;
                }
                else
                {
                    var newBrush = new D2D.SolidColorBrush(dc, Helpers.GetColor(color));
                    this.brushCache.Add(color, newBrush);
                    return newBrush;
                }
            }

            public void ClearBrushCache()
            {
                foreach (var brush in this.brushCache.Values)
                {
                    brush.Dispose();
                }
            }

            public DWrite.FontFace GetPrimaryFontFace(DWrite.FontWeight weight, DWrite.FontStyle style)
            {
                return this.fallbackFontFamilies[0].GetFontFace(weight, style);
            }

            public DWrite.FontFace GetFontFace(int codePoint, DWrite.FontWeight weight, DWrite.FontStyle style)
            {
                foreach (var f in this.fallbackFontFamilies)
                {
                    var face = f.GetFontFace(codePoint, weight, style);
                    if (face != null)
                    {
                        return face;
                    }
                }

                using (var source = new SingleCharTextSource(this.factory, codePoint))
                {
                    this.fontFallback.MapCharacters(
                        source,
                        0,
                        1,
                        this.fontCollection,
                        this.baseFontFamilyName,
                        weight,
                        style,
                        DWrite.FontStretch.Normal,
                        out var mappedLength,
                        out var font,
                        out var scale);

                    if (font != null)
                    {
                        var familyCache = new FontFamilyCacheItem(this.fontCollection, font.FontFamily.FamilyNames.GetString(0));
                        this.fallbackFontFamilies.Add(familyCache);
                        font.Dispose();

                        return familyCache.GetFontFace(codePoint, weight, style);
                    }
                }

                // OK we don't have the glyph for this codepoint in the fallbacks.
                // Use primary font to show something like '?'.
                return this.fallbackFontFamilies[0].GetFontFace(weight, style);
            }

            public void ClearFallbackFontCache()
            {
                foreach (var f in this.fallbackFontFamilies)
                {
                    f.Dispose();
                }
            }

            private void SetFallbackFontFamilyCache(List<string> familyNames)
            {
                this.ClearFallbackFontCache();

                this.baseFontFamilyName = familyNames[0];
                foreach (var name in familyNames)
                {
                    this.fallbackFontFamilies.Add(new FontFamilyCacheItem(this.fontCollection, name));
                }
            }

            private sealed class FontFamilyCacheItem : IDisposable
            {
                private readonly string fontFamilyName;
                private readonly DWrite.FontFamily fontFamily;
                private readonly Dictionary<(DWrite.FontWeight weight, DWrite.FontStyle style), (DWrite.Font font, DWrite.FontFace fontFace)> fontCache
                    = new Dictionary<(DWrite.FontWeight, DWrite.FontStyle), (DWrite.Font, DWrite.FontFace)>();

                public FontFamilyCacheItem(DWrite.FontCollection collection, string fontFamilyName)
                {
                    this.fontFamilyName = fontFamilyName;
                    collection.FindFamilyName(fontFamilyName, out var index);
                    this.fontFamily = collection.GetFontFamily(index);
                }

                public void Dispose()
                {
                    this.fontFamily.Dispose();
                    foreach (var (font, fontFace) in this.fontCache.Values)
                    {
                        font.Dispose();
                        fontFace.Dispose();
                    }
                }

                public DWrite.FontFace GetFontFace(DWrite.FontWeight weight, DWrite.FontStyle style)
                {
                    return this.GetOrAdd(weight, style).fontFace;
                }

                public DWrite.FontFace GetFontFace(int codePoint, DWrite.FontWeight weight, DWrite.FontStyle style)
                {
                    var (font, fontFace) = this.GetOrAdd(weight, style);

                    if (font.HasCharacter(codePoint))
                    {
                        return fontFace;
                    }

                    return null;
                }

                private (DWrite.Font font, DWrite.FontFace fontFace) GetOrAdd(DWrite.FontWeight weight, DWrite.FontStyle style)
                {
                    if (!this.fontCache.TryGetValue((weight, style), out var v))
                    {
                        var font = this.fontFamily.GetFirstMatchingFont(weight, DWrite.FontStretch.Normal, style);
                        var fontFace = new DWrite.FontFace(font);
                        v = (font, fontFace);
                        this.fontCache.Add((weight, style), v);
                    }

                    return v;
                }
            }
        }
    }
}
