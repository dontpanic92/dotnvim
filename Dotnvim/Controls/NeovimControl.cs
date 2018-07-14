// <copyright file="NeovimControl.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Dotnvim.Controls.Utilities;
    using Dotnvim.NeovimClient.Utilities;
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

        private readonly ConcurrentQueue<Action> pendingActions = new ConcurrentQueue<Action>();

        private TextLayoutParameters textParam;

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
            this.neovimClient.FontChanged += this.OnFontChanged;

            this.textAnalyzer = new DWrite.TextAnalyzer(this.factoryDWrite);
            this.cursorEffects = new CursorEffects(this.DeviceContext);
            this.dxItemCache = new DxItemCache(this.factoryDWrite);

            this.textParam = new TextLayoutParameters(
                this.factoryDWrite,
                "Consolas",
                11,
                false,
                false,
                false,
                false,
                this.Factory.DesktopDpi);
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
                var c = (uint)(this.Size.Height / this.textParam.LineHeight);
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
                var c = (uint)(this.Size.Width / this.textParam.CharWidth);
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

        /// <summary>
        /// GuiFont changed.
        /// </summary>
        /// <param name="font">The font settings.</param>
        public void OnFontChanged(FontSettings font)
        {
            this.pendingActions.Enqueue(() =>
            {
                if (this.dxItemCache.SetPrimaryFontFamily(font.FontName))
                {
                    this.textParam = new TextLayoutParameters(
                        this.factoryDWrite,
                        font.FontName,
                        font.FontPointSize,
                        font.Bold,
                        font.Italic,
                        font.Underline,
                        font.StrikeOut,
                        this.Factory.DesktopDpi);
                    this.neovimClient.TryResize(this.DesiredColCount, this.DesiredRowCount);
                }
                else
                {
                    this.neovimClient.WriteErrorMessage($"Dotnvim: Unable to use font: {font.FontName}");
                }
            });
            this.Invalidate();
        }

        /// <inheritdoc />
        protected override void Draw()
        {
            while (this.pendingActions.TryDequeue(out var action))
            {
                action();
            }

            var args = this.neovimClient.GetScreen();

            if (args == null)
            {
                return;
            }

            this.DeviceContext.BeginDraw();
            this.DeviceContext.Clear(new RawColor4(0, 0, 0, 0));

            // Paint the background
            for (int i = 0; i < args.Cells.GetLength(0); i++)
            {
                for (int j = 0; j < args.Cells.GetLength(1); j++)
                {
                    if (args.Cells[i, j].BackgroundColor != args.BackgroundColor || args.Cells[i, j].Reverse)
                    {
                        var x = j * this.textParam.CharWidth;
                        var y = i * this.textParam.LineHeight;

                        var rect = new RawRectangleF(x, y, x + this.textParam.CharWidth, y + this.textParam.LineHeight);
                        int color = args.Cells[i, j].Reverse ? args.Cells[i, j].ForegroundColor : args.Cells[i, j].BackgroundColor;
                        var brush = this.dxItemCache.GetBrush(this.DeviceContext, color);
                        this.DeviceContext.FillRectangle(rect, brush);
                    }
                }
            }

            // Paint the foreground
            for (int i = 0; i < args.Cells.GetLength(0); i++)
            {
                int j = 0;

                while (j < args.Cells.GetLength(1))
                {
                    // Cells with same style should be analyzed together.
                    // This prevents the inproper ligature in <html>=
                    // Of course, it relies on enabling the syntax.
                    int cellRangeStart = j;
                    int cellRangeEnd = j;
                    Cell startCell = args.Cells[i, cellRangeStart];
                    while (true)
                    {
                        if (cellRangeEnd == args.Cells.GetLength(1))
                        {
                            break;
                        }

                        Cell cell = args.Cells[i, cellRangeEnd];
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

                    var fontWeight = args.Cells[i, cellRangeStart].Bold ? DWrite.FontWeight.Bold : this.textParam.Weight;
                    var fontStyle = args.Cells[i, cellRangeStart].Italic ? DWrite.FontStyle.Italic : this.textParam.Style;

                    int cellIndex = cellRangeStart;
                    using (var textSink = new TextAnalysisSink())
                    using (var textSource = new RowTextSource(this.factoryDWrite, args.Cells, i, cellRangeStart, cellRangeEnd))
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
                                var foregroundColor = args.Cells[i, cellIndex].Reverse ? args.Cells[i, cellIndex].BackgroundColor : args.Cells[i, cellIndex].ForegroundColor;
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
                                    int codePoint = args.Cells[i, cellIndex].Character.Value;
                                    fontFace2 = this.dxItemCache.GetFontFace(codePoint, fontWeight, fontStyle);
                                    indices2 = fontFace2.GetGlyphIndices(new int[] { codePoint });
                                    glyphCount = indices2.Length;

                                    // NativeInterop.Methods.wcwidth(textSource.GetCodePoint(codePointStart + codePointIndex));
                                    cellWidth = this.GetCharWidth(args.Cells, i, cellIndex);
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
                                        cellWidth += this.GetCharWidth(args.Cells, i, cellIndex + cellWidth);
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
                                    FontSize = this.textParam.DipSize,
                                    Indices = indices2,
                                    IsSideways = false,
                                    Offsets = null,
                                })
                                {
                                    var origin = new RawVector2(this.textParam.CharWidth * cellIndex, this.textParam.LineHeight * (i + 0.8f));
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

            var cursorWidth = this.GetCharWidth(args.Cells, args.CursorPosition.Row, args.CursorPosition.Col);
            var cursorRect = new RawRectangleF()
            {
                Left = args.CursorPosition.Col * this.textParam.CharWidth,
                Top = args.CursorPosition.Row * this.textParam.LineHeight,
                Right = (args.CursorPosition.Col + cursorWidth) * this.textParam.CharWidth,
                Bottom = (args.CursorPosition.Row + 1) * this.textParam.LineHeight,
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
            private const string DefaultFontFamilyName = "Consolas";

            private readonly Dictionary<int, D2D.SolidColorBrush> brushCache
                = new Dictionary<int, D2D.SolidColorBrush>();

            private readonly DWrite.Factory factory;
            private readonly DWrite.FontCollection fontCollection;

            private readonly DWrite.FontFallback systemFontFallback;
            private readonly List<FontFamilyCacheItem> fontFamilies = new List<FontFamilyCacheItem>();

            private string baseFontFamilyName;

            public DxItemCache(DWrite.Factory factory)
            {
                this.factory = factory;
                using (var factoryDWrite = factory.QueryInterface<DWrite.Factory2>())
                {
                    this.fontCollection = factoryDWrite.GetSystemFontCollection(false);
                    this.systemFontFallback = factoryDWrite.SystemFontFallback;
                    this.SetPrimaryFontFamily(DefaultFontFamilyName);
                }
            }

            public void Dispose()
            {
                this.ClearBrushCache();
                this.ClearFontCache();
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

            public bool SetPrimaryFontFamily(string fontFamilyName)
            {
                if (FontFamilyCacheItem.TryCreate(this.fontCollection, fontFamilyName, out var item))
                {
                    this.ClearFontCache();
                    this.baseFontFamilyName = fontFamilyName;
                    this.fontFamilies.Add(item);
                    return true;
                }

                return false;
            }

            public DWrite.FontFace GetPrimaryFontFace(DWrite.FontWeight weight, DWrite.FontStyle style)
            {
                return this.fontFamilies[0].GetFontFace(weight, style);
            }

            public DWrite.FontFace GetFontFace(int codePoint, DWrite.FontWeight weight, DWrite.FontStyle style)
            {
                foreach (var f in this.fontFamilies)
                {
                    var face = f.GetFontFace(codePoint, weight, style);
                    if (face != null)
                    {
                        return face;
                    }
                }

                using (var source = new SingleCharTextSource(this.factory, codePoint))
                {
                    this.systemFontFallback.MapCharacters(
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
                        FontFamilyCacheItem.TryCreate(this.fontCollection, font.FontFamily.FamilyNames.GetString(0), out var familyCache);
                        this.fontFamilies.Add(familyCache);
                        font.Dispose();

                        return familyCache.GetFontFace(codePoint, weight, style);
                    }
                }

                // OK we don't have the glyph for this codepoint in the fallbacks.
                // Use primary font to show something like '?'.
                return this.fontFamilies[0].GetFontFace(weight, style);
            }

            private void ClearFontCache()
            {
                foreach (var f in this.fontFamilies)
                {
                    f.Dispose();
                }

                this.fontFamilies.Clear();
            }

            private sealed class FontFamilyCacheItem : IDisposable
            {
                private readonly DWrite.FontFamily fontFamily;
                private readonly Dictionary<(DWrite.FontWeight weight, DWrite.FontStyle style), (DWrite.Font font, DWrite.FontFace fontFace)> fontCache
                    = new Dictionary<(DWrite.FontWeight, DWrite.FontStyle), (DWrite.Font, DWrite.FontFace)>();

                private FontFamilyCacheItem(DWrite.FontFamily fontFamily)
                {
                    this.fontFamily = fontFamily;
                }

                public static bool TryCreate(DWrite.FontCollection collection, string fontFamilyName, out FontFamilyCacheItem item)
                {
                    if (collection.FindFamilyName(fontFamilyName, out var index))
                    {
                        var fontFamily = collection.GetFontFamily(index);
                        item = new FontFamilyCacheItem(fontFamily);
                        return true;
                    }
                    else
                    {
                        item = null;
                        return false;
                    }
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

        private sealed class TextLayoutParameters
        {
            public TextLayoutParameters(
                DWrite.Factory factory,
                string name,
                float pointSize,
                bool bold,
                bool italic,
                bool underline,
                bool strikeout,
                Size2F dpi)
            {
                this.FontName = name;
                this.PointSize = pointSize;
                this.DipSize = Helpers.GetFontSize(pointSize);
                this.Weight = bold ? DWrite.FontWeight.Bold : DWrite.FontWeight.Normal;
                this.Style = italic ? DWrite.FontStyle.Italic : DWrite.FontStyle.Normal;
                this.Underline = underline;
                this.StrikeOut = strikeout;

                using (var textFormat = new DWrite.TextFormat(factory, this.FontName, this.Weight, this.Style, this.DipSize))
                using (var textLayout = new DWrite.TextLayout(factory, "A", textFormat, 1000, 1000))
                {
                    this.LineHeight = Helpers.AlignToPixel(textLayout.Metrics.Height, dpi.Height);
                    this.CharWidth = Helpers.AlignToPixel(textLayout.OverhangMetrics.Left + (1000 + textLayout.OverhangMetrics.Right), dpi.Width);
                }
            }

            public string FontName { get; }

            public float PointSize { get; }

            public float DipSize { get; }

            public DWrite.FontStyle Style { get; }

            public DWrite.FontWeight Weight { get; }

            public bool Underline { get; }

            public bool StrikeOut { get; }

            public float LineHeight { get; }

            public float CharWidth { get; }
        }
    }
}
