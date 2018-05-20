// <copyright file="D2D1HwndRenderer.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using D2D = SharpDX.Direct2D1;
    using D3D = SharpDX.Direct3D;
    using D3D11 = SharpDX.Direct3D11;
    using DWrite = SharpDX.DirectWrite;
    using DXGI = SharpDX.DXGI;

    /// <summary>
    /// A renderer using Direct2D, rendering to a native control
    /// </summary>
    public sealed class D2D1HwndRenderer : IDisposable
    {
        private readonly DXGI.SwapChain swapChain;
        private readonly D3D11.Device device;
        private readonly D2D.Factory1 factory2d = new D2D.Factory1();
        private readonly D2D.Device device2d;
        private readonly DWrite.Factory factoryDWrite = new DWrite.Factory();
        private readonly D2D.DeviceContext deviceContext2d;
        private readonly Cursor cursor;
#if DEBUG
        private readonly D3D11.DeviceDebug deviceDebug;
#endif

        private D2D.Bitmap renderTarget;
        private D3D11.Texture2D backBuffer = null;
        private D2D.Bitmap backTarget;

        private SharpDX.Mathematics.Interop.RawColor4 backgroundColor;
        private SharpDX.Mathematics.Interop.RawColor4 foregroundColor;
        private SharpDX.Mathematics.Interop.RawColor4 specialColor;

        private string fontName = "Consolas";
        private float fontPoint = 11;
        private float lineHeight;
        private float charWidth;

        private int col = 80;
        private int row = 25;

        private int cursorX = 0;
        private int cursorY = 0;

        /*private List<string> fontNames;*/

        /// <summary>
        /// Initializes a new instance of the <see cref="D2D1HwndRenderer"/> class.
        /// </summary>
        /// <param name="handle">The native control HWND</param>
        public D2D1HwndRenderer(IntPtr handle)
        {
            var desc = new DXGI.SwapChainDescription()
            {
                BufferCount = 2,
                ModeDescription = new DXGI.ModeDescription(DXGI.Format.B8G8R8A8_UNorm),
                IsWindowed = true,
                OutputHandle = handle,
                SampleDescription = new DXGI.SampleDescription(1, 0),
                Usage = DXGI.Usage.RenderTargetOutput,
                SwapEffect = DXGI.SwapEffect.FlipDiscard,
            };

            D3D11.Device.CreateWithSwapChain(
                D3D.DriverType.Hardware,
                D3D11.DeviceCreationFlags.BgraSupport | D3D11.DeviceCreationFlags.Debug,
                new D3D.FeatureLevel[] { D3D.FeatureLevel.Level_11_1 },
                desc,
                out this.device,
                out this.swapChain);

            using (var device = this.device.QueryInterface<DXGI.Device>())
            {
                this.device2d = new D2D.Device(this.factory2d, device);
            }

            this.deviceContext2d = new D2D.DeviceContext(this.device2d, D2D.DeviceContextOptions.EnableMultithreadedOptimizations)
            {
                DotsPerInch = this.factory2d.DesktopDpi,
                AntialiasMode = D2D.AntialiasMode.Aliased,
                TextAntialiasMode = D2D.TextAntialiasMode.Cleartype,
            };

            this.cursor = new Cursor(this.deviceContext2d);

            using (var textFormat = new DWrite.TextFormat(this.factoryDWrite, this.fontName, DWrite.FontWeight.Normal, DWrite.FontStyle.Normal, this.GetFontSize(this.fontPoint)))
            using (var textLayout = new DWrite.TextLayout(this.factoryDWrite, "H", textFormat, 1000, 1000))
            {
                this.lineHeight = textLayout.Metrics.Height;
                this.charWidth = textLayout.Metrics.Width;
            }

            this.backgroundColor = new SharpDX.Mathematics.Interop.RawColor4(1, 1, 1, 0.2f);
            this.foregroundColor = new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 0.2f);
            this.specialColor = new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 0.2f);
#if DEBUG
            this.deviceDebug = new D3D11.DeviceDebug(this.device);
#endif
        }

        /// <summary>
        /// Gets the desired row count according to current font
        /// </summary>
        public uint DesiredRowCount
        {
            get
            {
                return (uint)(this.backTarget.Size.Height / this.lineHeight);
            }
        }

        /// <summary>
        /// Gets the desired col count according to current font
        /// </summary>
        public uint DesiredColCount
        {
            get
            {
                return (uint)(this.backTarget.Size.Width / this.charWidth);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the needs presents
        /// </summary>
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            this.ReleaseResources();
            this.renderTarget?.Dispose();
            this.cursor.Dispose();
            this.deviceContext2d.Dispose();
            this.device2d.Dispose();
            this.device.Dispose();
            this.swapChain.Dispose();
            this.factory2d.Dispose();
            this.factoryDWrite.Dispose();
#if DEBUG
            this.deviceDebug.Dispose();
#endif
        }

        /// <summary>
        /// Resize
        /// </summary>
        public void Resize()
        {
            this.ReleaseResources();
            this.swapChain.ResizeBuffers(2, 0, 0, DXGI.Format.B8G8R8A8_UNorm, DXGI.SwapChainFlags.None);
            this.CreateResources();
        }

        /// <summary>
        /// Begin draw
        /// </summary>
        public void BeginDraw()
        {
            // this.renderTarget.BeginDraw();
            this.deviceContext2d.BeginDraw();

            this.deviceContext2d.Target = this.renderTarget;
        }

        /// <summary>
        /// Render the bitmap onto the surface
        /// </summary>
        public void EndDraw()
        {
            this.deviceContext2d.Target = null;
            this.deviceContext2d.EndDraw();
            this.IsDirty = true;
        }

        /// <summary>
        /// Present the image
        /// </summary>
        public void Present()
        {
            if (this.backTarget == null)
            {
                return;
            }

            this.deviceContext2d.BeginDraw();
            this.deviceContext2d.Target = this.backTarget;

            if (this.renderTarget != null)
            {
                var rect = new SharpDX.Mathematics.Interop.RawRectangleF()
                {
                    Left = 0,
                    Top = 0,
                    Right = this.renderTarget.Size.Width,
                    Bottom = this.renderTarget.Size.Height,
                };

                // this.deviceContext2d.DrawBitmap(this.renderTarget, rect, 1, D2D.InterpolationMode.NearestNeighbor, rect, null);
            }

            var cursorRect = new SharpDX.Mathematics.Interop.RawRectangleF()
            {
                Left = this.cursorX * this.charWidth,
                Top = this.cursorY * this.lineHeight,
                Right = (this.cursorX + 1) * this.charWidth,
                Bottom = (this.cursorY + 1) * this.lineHeight,
            };

            // this.cursor.DrawCursor(this.renderTarget, cursurRect);
            // this.deviceContext2d.FillRectangle(cursorRect, new D2D.SolidColorBrush(this.deviceContext2d, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 0.2f)));
            this.deviceContext2d.Clear(new SharpDX.Mathematics.Interop.RawColor4(1, 1, 1, 0.2f));

            this.deviceContext2d.Target = null;
            this.deviceContext2d.EndDraw();
            var r = this.swapChain.Present(1, DXGI.PresentFlags.None);
            this.IsDirty = false;
        }

        /// <summary>
        /// Resize event
        /// </summary>
        /// <param name="col">col</param>
        /// <param name="row">row</param>
        public void Resize(int col, int row)
        {
            this.col = col;
            this.row = row;

            this.ResizeRenderTarget();
            this.deviceContext2d.Target = this.renderTarget;
        }

        /// <summary>
        /// UpdateFg event
        /// </summary>
        /// <param name="color">Foreground color</param>
        public void UpdateFg(int color)
        {
            this.foregroundColor = this.GetColor(color);
        }

        /// <summary>
        /// UpdateBg event
        /// </summary>
        /// <param name="color">Background color</param>
        public void UpdateBg(int color)
        {
            this.backgroundColor = this.GetColor(color);
        }

        /// <summary>
        /// UpdateSp event
        /// </summary>
        /// <param name="color">Special color</param>
        public void UpdateSp(int color)
        {
            this.specialColor = this.GetColor(color);
        }

        /// <summary>
        /// Clear event
        /// </summary>
        public void Clear()
        {
            this.deviceContext2d.Clear(this.backgroundColor);

            this.cursorX = 0;
            this.cursorY = 0;
        }

        /// <summary>
        /// Clear to end of line event
        /// </summary>
        public void EolClear()
        {
            var rect = new SharpDX.Mathematics.Interop.RawRectangleF()
            {
                Left = this.cursorX * this.charWidth,
                Top = this.cursorY * this.lineHeight,
                Right = this.backBuffer.Description.Width,
                Bottom = (this.cursorY + 1) * this.lineHeight,
            };

            using (var backBrush = new D2D.SolidColorBrush(this.deviceContext2d, this.backgroundColor))
            {
                this.deviceContext2d.FillRectangle(rect, backBrush);
            }
        }

        /// <summary>
        /// CursorGoto event
        /// </summary>
        /// <param name="row">Y position of cursor</param>
        /// <param name="col">X position of cursor</param>
        public void CursorGoto(int row, int col)
        {
            this.cursorX = col;
            this.cursorY = row;
        }

        /// <summary>
        /// Put event
        /// </summary>
        /// <param name="textList">The text to be rendered</param>
        /// <param name="foreground">Foreground color</param>
        /// <param name="background">Background color</param>
        /// <param name="special">Special color</param>
        /// <param name="reverse">IsReverse</param>
        /// <param name="italic">IsItalic</param>
        /// <param name="bold">IsBold</param>
        /// <param name="underline">IsUnderline</param>
        /// <param name="undercurl">IsUnderCurl</param>
        public void Put(IList<string> textList, int? foreground, int? background, int? special, bool reverse, bool italic, bool bold, bool underline, bool undercurl)
        {
            float x = this.charWidth * this.cursorX;
            float y = this.lineHeight * this.cursorY;
            var rect = new SharpDX.Mathematics.Interop.RawRectangleF()
            {
                Left = x,
                Top = y,
                Right = x + (this.charWidth * textList.Count),
                Bottom = y + this.lineHeight,
            };

            var fontWeight = bold ? DWrite.FontWeight.Bold : DWrite.FontWeight.Normal;
            var fontStyle = italic ? DWrite.FontStyle.Italic : DWrite.FontStyle.Normal;
            var foregroundColor = foreground == null ? this.foregroundColor : this.GetColor(foreground.Value);
            var backgroundColor = background == null ? this.backgroundColor : this.GetColor(background.Value);

            using (var foregroundBrush = new D2D.SolidColorBrush(this.deviceContext2d, reverse ? backgroundColor : foregroundColor))
            using (var backgroundBrush = new D2D.SolidColorBrush(this.deviceContext2d, reverse ? foregroundColor : backgroundColor))
            {
                this.deviceContext2d.FillRectangle(rect, backgroundBrush);
                using (var textFormat = new DWrite.TextFormat(this.factoryDWrite, this.fontName, fontWeight, fontStyle, this.GetFontSize(this.fontPoint)))
                {
                    foreach (var text in textList)
                    {
                        if (text == string.Empty)
                        {
                            this.cursorX += 1;
                            continue;
                        }

                        using (var textLayout = new DWrite.TextLayout(this.factoryDWrite, text, textFormat, this.backBuffer.Description.Width, this.backBuffer.Description.Height))
                        {
                            textLayout.SetUnderline(underline, new DWrite.TextRange(0, text.Length));
                            var origin = new SharpDX.Mathematics.Interop.RawVector2()
                            {
                                X = this.charWidth * this.cursorX,
                                Y = this.lineHeight * this.cursorY,
                            };

                            this.cursorX += 1;
                            this.deviceContext2d.DrawTextLayout(origin, textLayout, foregroundBrush, D2D.DrawTextOptions.DisableColorBitmapSnapping);
                        }
                    }
                }
            }
        }

        private void ReleaseResources()
        {
            this.backBuffer?.Dispose();
            this.backTarget?.Dispose();
#if DEBUG
            this.deviceDebug.ReportLiveDeviceObjects(D3D11.ReportingLevel.Detail);
#endif
        }

        private void CreateResources()
        {
            this.backBuffer = D3D11.Texture2D.FromSwapChain<D3D11.Texture2D>(this.swapChain, 0);
            this.backBuffer.DebugName = "BackBuffer";

            using (var surface = this.backBuffer.QueryInterface<DXGI.Surface>())
            {
                var properties = new D2D.BitmapProperties(
                    new D2D.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, D2D.AlphaMode.Premultiplied),
                    this.factory2d.DesktopDpi.Width,
                    this.factory2d.DesktopDpi.Height);

                this.backTarget = new D2D.Bitmap(this.deviceContext2d, surface, properties);
            }

            this.IsDirty = true;
        }

        private void ResizeRenderTarget()
        {
            if (this.renderTarget != null)
            {
                if (this.renderTarget.PixelSize == this.backTarget.PixelSize)
                {
                    return;
                }
            }

            var p = new D2D.BitmapProperties1(
                new D2D.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, D2D.AlphaMode.Premultiplied),
                this.factory2d.DesktopDpi.Width,
                this.factory2d.DesktopDpi.Height,
                D2D.BitmapOptions.Target);

            this.renderTarget = new D2D.Bitmap1(this.deviceContext2d, this.backTarget.PixelSize, p);
        }

        private float GetFontSize(float pt)
        {
            return pt * 96 / 72;
        }

        private SharpDX.Mathematics.Interop.RawColor4 GetColor(int color)
        {
            float b = color % 256;
            color /= 256;
            float g = color % 256;
            color /= 256;
            float r = color % 256;

            return new SharpDX.Mathematics.Interop.RawColor4(r / 256, g / 256, b / 256, 0.2f);
        }
    }
}
