﻿// <copyright file="NeovimControl.cs">
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
    using Dotnvim.Controls.Cache;
    using Dotnvim.Controls.Utilities;
    using Dotnvim.NeovimClient.Utilities;
    using Dotnvim.Utilities;
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
        private readonly BrushCache brushCache;
        private readonly FontCache fontCache;
        private readonly ScriptAnalysesCache scriptAnalysesCache;

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
            this.brushCache = new BrushCache();
            this.scriptAnalysesCache = new ScriptAnalysesCache();
            this.fontCache = new FontCache(this.factoryDWrite);

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

        /// <summary>
        /// Gets or sets a value indicating whether the font ligature is enabled.
        /// </summary>
        public bool EnableLigature { get; set; }

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
                if (this.fontCache.SetPrimaryFontFamily(font.FontName))
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

            this.scriptAnalysesCache.StartNewFrame();
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
                        var brush = this.brushCache.GetBrush(this.DeviceContext, color);
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
                    using (var textSource = new RowTextSource(this.factoryDWrite, args.Cells, i, cellRangeStart, cellRangeEnd))
                    {
                        var scriptAnalyses = this.scriptAnalysesCache.GetOrAddAnalysisResult(
                            textSource.GetTextAtPosition(0),
                            (_) =>
                            {
                                using (var textSink = new TextAnalysisSink())
                                {
                                    this.textAnalyzer.AnalyzeScript(textSource, 0, textSource.Length, textSink);
                                    return textSink.ScriptAnalyses;
                                }
                            });

                        // The result of AalyzeScript may cut the text into several ranges,
                        // and in each range the text's scripts are different.
                        foreach (var (codePointStart, codePointLength, scriptAnalysis) in scriptAnalyses)
                        {
                            var glyphBufferLength = (codePointLength * 3 / 2) + 16;
                            var clusterMap = new short[codePointLength];
                            var textProperties = new DWrite.ShapingTextProperties[codePointLength];
                            short[] indices;
                            DWrite.ShapingGlyphProperties[] shapingProperties;
                            var fontFace = this.fontCache.GetPrimaryFontFace(fontWeight, fontStyle);
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

                                    DWrite.FontFeature[][] fontFeatures = null;
                                    int[] featureLength = null;

                                    if (!this.EnableLigature)
                                    {
                                        fontFeatures = new DWrite.FontFeature[][]
                                        {
                                            new DWrite.FontFeature[]
                                            {
                                                new DWrite.FontFeature(DWrite.FontFeatureTag.StandardLigatures, 0),
                                            },
                                        };

                                        featureLength = new int[]
                                        {
                                            str.Length,
                                        };
                                    }

                                    this.textAnalyzer.GetGlyphs(
                                        str,
                                        str.Length,
                                        fontFace,
                                        false,
                                        false,
                                        scriptAnalysis,
                                        null,
                                        null,
                                        fontFeatures,
                                        featureLength,
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
                                var foregroundBrush = this.brushCache.GetBrush(this.DeviceContext, foregroundColor);
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
                                    fontFace2 = this.fontCache.GetFontFace(codePoint, fontWeight, fontStyle);
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
            this.brushCache.Dispose();
            this.fontCache.Dispose();
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
