// <copyright file="D2D1Renderer.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf.Rendering
{
    using System;
    using System.Collections.Generic;
    using SharpDX;
    using D2D = SharpDX.Direct2D1;
    using D3D = SharpDX.Direct3D;
    using D3D11 = SharpDX.Direct3D11;
    using DWrite = SharpDX.DirectWrite;
    using DXGI = SharpDX.DXGI;

    /// <summary>
    /// A renderer using Direct2D, rendering to a D3D11 Image
    /// </summary>
    public sealed class D2D1Renderer : IDisposable
    {
        private readonly D2D.Factory1 factory2d = new D2D.Factory1();
        private readonly DWrite.Factory factoryDWrite = new DWrite.Factory();
        private readonly D3D11.Device device;
        private readonly D2D.Device device2d;
        private readonly D2D.DeviceContext deviceContext2d;
        private readonly Cursor cursor;

        private IntPtr backBufferPtr = IntPtr.Zero;
        private D2D.Bitmap backBitmap;
        private D2D.Bitmap renderBitmap;

        private Size2F targetSize;

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

        private int scrollTop = 0;
        private int scrollBottom = 0;
        private int scrollLeft = 0;
        private int scrollRight = 0;

        private bool transparentBackground = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="D2D1Renderer"/> class.
        /// </summary>
        /// <param name="transparentBackground">Whether the background should be transparent</param>
        public D2D1Renderer(bool transparentBackground)
        {
            this.device = new D3D11.Device(
                D3D.DriverType.Hardware,
                D3D11.DeviceCreationFlags.BgraSupport | D3D11.DeviceCreationFlags.Debug,
                new D3D.FeatureLevel[] { D3D.FeatureLevel.Level_11_1 });

            using (var device = this.device.QueryInterface<DXGI.Device>())
            {
                this.device2d = new D2D.Device(this.factory2d, device);
            }

            this.deviceContext2d = new D2D.DeviceContext(this.device2d, D2D.DeviceContextOptions.EnableMultithreadedOptimizations)
            {
                DotsPerInch = this.factory2d.DesktopDpi,
                AntialiasMode = D2D.AntialiasMode.Aliased,
            };

            this.cursor = new Cursor(this.deviceContext2d);

            using (var textFormat = new DWrite.TextFormat(this.factoryDWrite, this.fontName, DWrite.FontWeight.Normal, DWrite.FontStyle.Normal, Utilities.GetFontSize(this.fontPoint)))
            using (var textLayout = new DWrite.TextLayout(this.factoryDWrite, "A", textFormat, 1000, 1000))
            {
                this.lineHeight = textLayout.Metrics.Height;
                this.charWidth = textLayout.OverhangMetrics.Left + (1000 + textLayout.OverhangMetrics.Right);
            }

            this.transparentBackground = transparentBackground;

            this.backgroundColor = new SharpDX.Mathematics.Interop.RawColor4(1, 1, 1, this.transparentBackground ? 0 : 1);
            this.foregroundColor = new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1f);
            this.specialColor = new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1f);
        }

        /// <summary>
        /// Gets or sets the texture that to be rendered on
        /// </summary>
        public IntPtr RenderBufferPtr
        {
            get
            {
                return this.backBufferPtr;
            }

            set
            {
                if (value == this.backBufferPtr)
                {
                    return;
                }

                this.backBufferPtr = value;
                this.InitializeRenderTarget(value);
            }
        }

        /// <summary>
        /// Gets the desired row count
        /// </summary>
        public uint DesiredRowCount
        {
            get
            {
                return (uint)(this.targetSize.Height / this.lineHeight);
            }
        }

        /// <summary>
        /// Gets the desired col count
        /// </summary>
        public uint DesiredColCount
        {
            get
            {
                return (uint)(this.targetSize.Width / this.charWidth);
            }
        }

        /// <summary>
        /// Surface is resized
        /// </summary>
        /// <param name="size">Size of the surface</param>
        public void Resize(Size2F size)
        {
            this.targetSize = size;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            this.renderBitmap?.Dispose();
            this.cursor?.Dispose();
            this.deviceContext2d.Dispose();
            this.device2d.Dispose();
            this.device.Dispose();
            this.factory2d.Dispose();
            this.factoryDWrite.Dispose();
        }

        /// <summary>
        /// Begin draw
        /// </summary>
        public void BeginDraw()
        {
            this.deviceContext2d.BeginDraw();

            this.deviceContext2d.Target = this.renderBitmap;
        }

        /// <summary>
        /// Render the bitmap onto the surface
        /// </summary>
        public void EndDraw()
        {
            this.deviceContext2d.Target = null;
            this.deviceContext2d.EndDraw();
        }

        /// <summary>
        /// Present the image
        /// </summary>
        public void Present()
        {
            if (this.backBitmap == null)
            {
                return;
            }

            this.deviceContext2d.BeginDraw();
            this.deviceContext2d.Target = this.backBitmap;

            if (this.renderBitmap != null)
            {
                var rect = new SharpDX.Mathematics.Interop.RawRectangleF()
                {
                    Left = 0,
                    Top = 0,
                    Right = this.renderBitmap.Size.Width,
                    Bottom = this.renderBitmap.Size.Height,
                };

                var cursorRect = new SharpDX.Mathematics.Interop.RawRectangleF()
                {
                    Left = this.cursorX * this.charWidth,
                    Top = this.cursorY * this.lineHeight,
                    Right = (this.cursorX + 1) * this.charWidth,
                    Bottom = (this.cursorY + 1) * this.lineHeight,
                };

                this.deviceContext2d.Clear(this.backgroundColor);
                this.deviceContext2d.DrawBitmap(this.renderBitmap, rect, 1, D2D.InterpolationMode.NearestNeighbor, rect, null);
                this.cursor.DrawCursor(this.renderBitmap, cursorRect);
            }

            this.deviceContext2d.Target = null;
            this.deviceContext2d.EndDraw();
            this.device.ImmediateContext.Flush();
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
            this.deviceContext2d.Target = this.renderBitmap;
        }

        /// <summary>
        /// SetScrollRegion event
        /// </summary>
        /// <param name="top">Top row in the region</param>
        /// <param name="bottom">Bottom row in the region</param>
        /// <param name="left">Leftmost col in the region</param>
        /// <param name="right">Rightmost col in the region</param>
        public void SetScrollRegion(int top, int bottom, int left, int right)
        {
            this.scrollTop = top;
            this.scrollBottom = bottom;
            this.scrollLeft = left;
            this.scrollRight = right;
        }

        /// <summary>
        /// Scroll event
        /// </summary>
        /// <param name="count">Row count to scroll</param>
        public void Scroll(int count)
        {
            var scrollRect = new SharpDX.Mathematics.Interop.RawRectangleF()
            {
                Top = this.lineHeight * this.scrollTop,
                Bottom = this.lineHeight * (this.scrollBottom + 1),
                Left = this.charWidth * this.scrollLeft,
                Right = this.charWidth * this.scrollRight,
            };

            var upperRect = new SharpDX.Mathematics.Interop.RawRectangleF()
            {
                Top = scrollRect.Top,
                Bottom = this.lineHeight * (this.scrollBottom - Math.Abs(count) + 1),
                Left = scrollRect.Left,
                Right = scrollRect.Right,
            };

            var lowerRect = new SharpDX.Mathematics.Interop.RawRectangleF()
            {
                Top = this.lineHeight * (this.scrollTop + Math.Abs(count)),
                Bottom = scrollRect.Bottom,
                Left = scrollRect.Left,
                Right = scrollRect.Right,
            };

            SharpDX.Mathematics.Interop.RawRectangleF srcRect, destRect;

            if (count > 0)
            {
                // Scroll down
                srcRect = lowerRect;
                destRect = upperRect;
            }
            else
            {
                // Scroll up
                srcRect = upperRect;
                destRect = lowerRect;
            }

            using (var tmpBitmap = Utilities.CopyBitmap(this.deviceContext2d, this.renderBitmap, srcRect, this.factory2d.DesktopDpi))
            {
                this.deviceContext2d.PushAxisAlignedClip(scrollRect, D2D.AntialiasMode.Aliased);
                this.deviceContext2d.Clear(this.backgroundColor);
                this.deviceContext2d.PopAxisAlignedClip();

                this.deviceContext2d.DrawBitmap(tmpBitmap, destRect, 1, D2D.BitmapInterpolationMode.NearestNeighbor);
            }
        }

        /// <summary>
        /// UpdateFg event
        /// </summary>
        /// <param name="color">Foreground color</param>
        public void UpdateFg(int color)
        {
            this.foregroundColor = Utilities.GetColor(color);
        }

        /// <summary>
        /// UpdateBg event
        /// </summary>
        /// <param name="color">Background color</param>
        public void UpdateBg(int color)
        {
            this.backgroundColor = Utilities.GetColor(color, this.transparentBackground ? 0 : 1);
        }

        /// <summary>
        /// UpdateSp event
        /// </summary>
        /// <param name="color">Special color</param>
        public void UpdateSp(int color)
        {
            this.specialColor = Utilities.GetColor(color);
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
                Right = this.targetSize.Width,
                Bottom = (this.cursorY + 1) * this.lineHeight,
            };

            this.deviceContext2d.PushAxisAlignedClip(rect, D2D.AntialiasMode.Aliased);
            this.deviceContext2d.Clear(this.backgroundColor);
            this.deviceContext2d.PopAxisAlignedClip();
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
            var foregroundColor = foreground == null ? this.foregroundColor : Utilities.GetColor(foreground.Value);

            var backgroundColor = background == null ? this.backgroundColor : Utilities.GetColor(background.Value);

            if (this.transparentBackground)
            {
                // If the background color is explictly specified and is different from the default
                // background color, set opacity to 50%
                if (backgroundColor.R == this.backgroundColor.R
                    && backgroundColor.G == this.backgroundColor.G
                    && backgroundColor.B == this.backgroundColor.B)
                {
                    backgroundColor.A = this.backgroundColor.A;
                }
                else
                {
                    backgroundColor.A = 0.5f;
                }
            }

            if (reverse)
            {
                var tmp = foregroundColor;
                foregroundColor = backgroundColor;
                backgroundColor = tmp;
                foregroundColor.A = 1;
            }

            using (var foregroundBrush = new D2D.SolidColorBrush(this.deviceContext2d, foregroundColor))
            using (var backgroundBrush = new D2D.SolidColorBrush(this.deviceContext2d, backgroundColor))
            {
                this.deviceContext2d.PushAxisAlignedClip(rect, D2D.AntialiasMode.Aliased);
                this.deviceContext2d.Clear(backgroundColor);
                this.deviceContext2d.PopAxisAlignedClip();

                using (var textFormat = new DWrite.TextFormat(this.factoryDWrite, this.fontName, fontWeight, fontStyle, Utilities.GetFontSize(this.fontPoint)))
                {
                    for (int i = 0; i < textList.Count; i++)
                    {
                        var text = textList[i];
                        if (text == string.Empty)
                        {
                            this.cursorX += 1;
                            continue;
                        }

                        int widthFactor = 1;
                        if (i != textList.Count - 1 && textList[i + 1] == string.Empty)
                        {
                            widthFactor = 2;
                        }

                        using (var textLayout = new DWrite.TextLayout(this.factoryDWrite, text, textFormat, this.charWidth * widthFactor, this.lineHeight))
                        {
                            textLayout.SetUnderline(underline, new DWrite.TextRange(0, text.Length));
                            var origin = new SharpDX.Mathematics.Interop.RawVector2()
                            {
                                X = this.charWidth * this.cursorX,
                                Y = this.lineHeight * this.cursorY,
                            };

                            this.cursorX += 1;
                            this.deviceContext2d.DrawTextLayout(origin, textLayout, foregroundBrush, D2D.DrawTextOptions.Clip);
                        }
                    }
                }
            }
        }

        private void InitializeRenderTarget(IntPtr ptr)
        {
            var comObject = new ComObject(ptr);
            var dxgiResource = comObject.QueryInterface<DXGI.Resource>();
            var backBuffer = this.device.OpenSharedResource<D3D11.Texture2D>(dxgiResource.SharedHandle);

            using (var surface = backBuffer.QueryInterface<DXGI.Surface>())
            {
                var properties = new D2D.BitmapProperties(
                    new D2D.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, D2D.AlphaMode.Premultiplied),
                    this.factory2d.DesktopDpi.Width,
                    this.factory2d.DesktopDpi.Height);

                this.backBitmap = new D2D.Bitmap(this.deviceContext2d, surface, properties);
            }
        }

        private void ResizeRenderTarget()
        {
            if (this.renderBitmap != null)
            {
                if (this.renderBitmap.Size == this.targetSize)
                {
                    return;
                }
            }

            var p = new D2D.BitmapProperties1(
                new D2D.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, D2D.AlphaMode.Premultiplied),
                this.factory2d.DesktopDpi.Width,
                this.factory2d.DesktopDpi.Height,
                D2D.BitmapOptions.Target);

            this.renderBitmap = new D2D.Bitmap1(
                this.deviceContext2d,
                Utilities.GetPixelSize(this.targetSize, this.factory2d.DesktopDpi),
                p);
        }
    }
}
