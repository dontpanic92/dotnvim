// <copyright file="ControlBase.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Dotnvim.Controls.Utilities;
    using Dotnvim.Utilities;
    using SharpDX;
    using SharpDX.Mathematics.Interop;
    using D2D = SharpDX.Direct2D1;
    using D3D = SharpDX.Direct3D;
    using D3D11 = SharpDX.Direct3D11;
    using DWrite = SharpDX.DirectWrite;
    using DXGI = SharpDX.DXGI;

    /// <summary>
    /// The base class for controls.
    /// </summary>
    public abstract class ControlBase : ElementBase
    {
        private D2D.Bitmap backBitmap;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlBase"/> class.
        /// </summary>
        /// <param name="parent">The parent control.</param>
        public ControlBase(IElement parent)
            : base(parent)
        {
            this.DeviceContext = new D2D.DeviceContext(this.Device, D2D.DeviceContextOptions.EnableMultithreadedOptimizations)
            {
                DotsPerInch = this.Factory.DesktopDpi,
                AntialiasMode = D2D.AntialiasMode.Aliased,
            };
        }

        /// <summary>
        /// Gets the device context.
        /// </summary>
        protected D2D.DeviceContext DeviceContext { get; }

        /// <summary>
        /// Gets the post effects.
        /// </summary>
        protected virtual EffectChain PostEffects => null;

        /// <inheritdoc />
        public override void Draw(D2D.DeviceContext deviceContext)
        {
            if (this.backBitmap == null || this.backBitmap.Size != this.Size)
            {
                this.InitializeBackBuffer(deviceContext, this.Size);
            }

            this.Draw();

            var boundary = new RawRectangleF()
            {
                Top = this.Position.Y,
                Left = this.Position.X,
                Bottom = this.Position.Y + this.Size.Height,
                Right = this.Position.X + this.Size.Width,
            };

            if (this.PostEffects?.Any() == true)
            {
                this.PostEffects.SetInput(this.backBitmap);
                using (var output = this.PostEffects.Output)
                {
                    deviceContext.DrawImage(output, new RawVector2(boundary.Left, boundary.Top), D2D.InterpolationMode.NearestNeighbor);
                }
            }
            else
            {
                deviceContext.DrawBitmap(this.backBitmap, boundary, 1, D2D.BitmapInterpolationMode.NearestNeighbor);

                // deviceContext.DrawImage(this.backBitmap, new RawVector2(boundary.Left, boundary.Top), D2D.InterpolationMode.NearestNeighbor);
            }
        }

        /// <summary>
        /// Invalidate this control.
        /// </summary>
        public void Invalidate()
        {
            this.Parent.Invalidate(this);
        }

        /// <inheritdoc />
        public override void Layout()
        {
        }

        /// <summary>
        /// Draw the control.
        /// </summary>
        protected abstract void Draw();

        /// <inheritdoc />
        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            this.backBitmap?.Dispose();
            this.DeviceContext.Dispose();
        }

        private void InitializeBackBuffer(D2D.DeviceContext deviceContext, SharpDX.Size2F size)
        {
            this.backBitmap?.Dispose();

            Size2 pixelSize = Helpers.GetPixelSize(size, this.Factory.DesktopDpi);

            var desc = new D3D11.Texture2DDescription()
            {
                ArraySize = 1,
                BindFlags = D3D11.BindFlags.RenderTarget | D3D11.BindFlags.ShaderResource,
                CpuAccessFlags = D3D11.CpuAccessFlags.None,
                Format = DXGI.Format.B8G8R8A8_UNorm,
                MipLevels = 1,
                OptionFlags = D3D11.ResourceOptionFlags.Shared,
                Usage = D3D11.ResourceUsage.Default,
                SampleDescription = new DXGI.SampleDescription(1, 0),
                Width = pixelSize.Width,
                Height = pixelSize.Height,
            };

            var p = new D2D.BitmapProperties1(
                new D2D.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, D2D.AlphaMode.Premultiplied),
                this.Factory.DesktopDpi.Width,
                this.Factory.DesktopDpi.Height,
                D2D.BitmapOptions.Target);

            this.backBitmap = new D2D.Bitmap1(
                deviceContext,
                pixelSize,
                p);

            this.DeviceContext.Target = this.backBitmap;
        }
    }
}
