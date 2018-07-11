// <copyright file="LogoControl.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System.Drawing;
    using System.Drawing.Imaging;
    using SharpDX;
    using SharpDX.Mathematics.Interop;
    using D2D = SharpDX.Direct2D1;

    /// <summary>
    /// The Logo control.
    /// </summary>
    public class LogoControl : ControlBase
    {
        private const float VerticalPadding = 3;
        private const float HorinzontalPadding = 8;
        private readonly D2D.Bitmap bitmap;
        private readonly float ratio;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogoControl"/> class.
        /// </summary>
        /// <param name="parent">The parent control.</param>
        public LogoControl(IElement parent)
            : base(parent)
        {
            var image = Properties.Resources.neovim_logo_flat;
            var size = new Rectangle(0, 0, image.Width, image.Height);
            var bitmapProperties = new D2D.BitmapProperties()
            {
                DpiX = this.Factory.DesktopDpi.Width,
                DpiY = this.Factory.DesktopDpi.Height,
                PixelFormat = new D2D.PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, D2D.AlphaMode.Premultiplied),
            };

            var bitmapData = image.LockBits(size, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            this.bitmap = new D2D.Bitmap(
                this.DeviceContext,
                new Size2(image.Width, image.Height),
                new DataPointer(bitmapData.Scan0, bitmapData.Stride * bitmapData.Height),
                bitmapData.Stride,
                bitmapProperties);
            image.UnlockBits(bitmapData);

            this.ratio = (float)image.Width / image.Height;
        }

        /// <inheritdoc />
        public override void Layout()
        {
            base.Layout();

            this.Size = new Size2F(((this.Size.Height - (2 * VerticalPadding)) * this.ratio) + (2 * HorinzontalPadding), this.Size.Height);
        }

        /// <inheritdoc />
        protected override void Draw()
        {
            var dest = new RawRectangleF(
                HorinzontalPadding,
                VerticalPadding,
                this.Size.Width - HorinzontalPadding,
                this.Size.Height - VerticalPadding);

            this.DeviceContext.BeginDraw();
            this.DeviceContext.Clear(new RawColor4(0, 0, 0, 0));
            this.DeviceContext.DrawBitmap(
                this.bitmap,
                dest,
                1,
                SharpDX.Direct2D1.InterpolationMode.HighQualityCubic,
                null,
                null);
            this.DeviceContext.EndDraw();
        }

        /// <inheritdoc />
        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            this.bitmap.Dispose();
        }
    }
}
