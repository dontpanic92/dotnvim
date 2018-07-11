// <copyright file="Helpers.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
    using System;
    using SharpDX;
    using SharpDX.Mathematics.Interop;
    using D2D = SharpDX.Direct2D1;

    /// <summary>
    /// Utility functions.
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Convert font Pt size to DIP size.
        /// </summary>
        /// <param name="pt">Font point.</param>
        /// <returns>DIP size.</returns>
        public static float GetFontSize(float pt)
        {
            return pt * 96 / 72;
        }

        /// <summary>
        /// Convert DIP size to Pixel size.
        /// </summary>
        /// <param name="size">Size in Dip.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>Size in Pixel.</returns>
        public static Size2 GetPixelSize(Size2F size, Size2F dpi)
        {
            return new Size2(
                (int)Math.Round(dpi.Width * size.Width / 96),
                (int)Math.Round(dpi.Height * size.Height / 96));
        }

        /// <summary>
        /// Convert Pixel size to Dip size.
        /// </summary>
        /// <param name="size">Size in pixel.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>Size in DIP.</returns>
        public static Size2F GetDipSize(Size2 size, Size2F dpi)
        {
            (var fx, var fy) = GetDipSize(size.Width, size.Height, dpi);
            return new Size2F(fx, fy);
        }

        /// <summary>
        /// Convert Pixel point to DIP point.
        /// </summary>
        /// <param name="x">x in pixel.</param>
        /// <param name="y">y in pixel.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>Vector in Dip.</returns>
        public static RawVector2 GetDipPoint(int x, int y, Size2F dpi)
        {
            (var fx, var fy) = GetDipSize(x, y, dpi);
            return new RawVector2(fx, fy);
        }

        /// <summary>
        /// Convert Pixel size to Dip size.
        /// </summary>
        /// <param name="x">x in pixel.</param>
        /// <param name="y">y in pixel.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>Size in DIP.</returns>
        public static (float x, float y) GetDipSize(int x, int y, Size2F dpi)
        {
            return (x * 96 / dpi.Width, y * 96 / dpi.Height);
        }

        /// <summary>
        /// Convert rectangle in DIP to Pixel.
        /// </summary>
        /// <param name="rect">rRct in DIP.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>Rect in Pixel.</returns>
        public static RawRectangle GetRawRectangle(RawRectangleF rect, Size2F dpi)
        {
            return new RawRectangle()
            {
                Top = (int)Math.Round(dpi.Height * rect.Top / 96),
                Bottom = (int)Math.Round(dpi.Height * rect.Bottom / 96),
                Left = (int)Math.Round(dpi.Width * rect.Left / 96),
                Right = (int)Math.Round(dpi.Width * rect.Right / 96),
            };
        }

        /// <summary>
        /// Round DIP to make it represents an integral pixels.
        /// </summary>
        /// <param name="dip">DIP.</param>
        /// <param name="dpi">dpi.</param>
        /// <returns>DIP aligned.</returns>
        public static float AlignToPixel(float dip, float dpi)
        {
            int pixel = (int)Math.Round(dip / 96 * dpi);
            return pixel * 96.0f / dpi;
        }

        /// <summary>
        /// Convert int-based color to RawColor.
        /// </summary>
        /// <param name="color">Color in int.</param>
        /// <param name="alpha">Alpha.</param>
        /// <returns>ShartDX RawColor.</returns>
        public static RawColor4 GetColor(int color, float alpha = 1)
        {
            float b = color % 256;
            color /= 256;
            float g = color % 256;
            color /= 256;
            float r = color % 256;

            return new RawColor4(r / 256, g / 256, b / 256, alpha);
        }

        /// <summary>
        /// Copy a rect of bitmap into a new one.
        /// </summary>
        /// <param name="renderTarget">The render target, e.g. device context.</param>
        /// <param name="bitmap">Original bitmap.</param>
        /// <param name="rect">The area to be copied.</param>
        /// <param name="dpi">Dpi.</param>
        /// <returns>The new copied bitmap.</returns>
        public static D2D.Bitmap CopyBitmap(D2D.RenderTarget renderTarget, D2D.Bitmap bitmap, RawRectangleF rect, Size2F dpi)
        {
            var bitmapProperties = new D2D.BitmapProperties(
                bitmap.PixelFormat,
                dpi.Width,
                dpi.Height);

            var pixelRect = GetRawRectangle(rect, dpi);
            var pixelSize = new Size2(pixelRect.Right - pixelRect.Left, pixelRect.Bottom - pixelRect.Top);
            var newBitmap = new D2D.Bitmap(renderTarget, pixelSize, bitmapProperties);
            newBitmap.CopyFromBitmap(bitmap, new RawPoint(0, 0), pixelRect);
            return newBitmap;
        }
    }
}
