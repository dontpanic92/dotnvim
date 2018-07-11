// <copyright file="FormRenderer.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Forms;
    using Dotnvim.Controls;
    using SharpDX.Mathematics.Interop;
    using D2D = SharpDX.Direct2D1;
    using D3D = SharpDX.Direct3D;
    using D3D11 = SharpDX.Direct3D11;
    using DComp = SharpDX.DirectComposition;
    using DWrite = SharpDX.DirectWrite;
    using DXGI = SharpDX.DXGI;

    /// <summary>
    /// The renderer to render a form.
    /// </summary>
    public sealed class FormRenderer : IDisposable
    {
        private readonly DXGI.SwapChain1 swapChain;
        private readonly D3D11.Device device;
        private readonly D2D.Factory1 factory2d = new D2D.Factory1();
        private readonly D2D.Device device2d;
        private readonly DWrite.Factory factoryDWrite = new DWrite.Factory();
        private readonly D2D.DeviceContext deviceContext2d;
        private readonly DComp.Device deviceComp;
        private readonly DComp.Target compositionTarget;

#if DEBUG
        private readonly D3D11.DeviceDebug deviceDebug;
#endif
        private readonly Form1 form;

        private D3D11.Texture2D backBuffer;
        private D2D.Bitmap backBitmap;
        private D2D.Bitmap renderBitmap;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormRenderer"/> class.
        /// </summary>
        /// <param name="form">The form.</param>
        public FormRenderer(Form1 form)
        {
            this.form = form;
#if DEBUG
            var creationFlags = D3D11.DeviceCreationFlags.BgraSupport | D3D11.DeviceCreationFlags.Debug;
            var debugFactory = true;
#else
            var creationFlags = D3D11.DeviceCreationFlags.BgraSupport;
            var debugFactory = false;
#endif

            this.device = new D3D11.Device(D3D.DriverType.Hardware, creationFlags);

#if DEBUG
            this.deviceDebug = new D3D11.DeviceDebug(this.device);
#endif

            using (var dxgiDevice = this.device.QueryInterface<DXGI.Device>())
            {
                using (var dxgiFactory = new DXGI.Factory2(debugFactory))
                {
                    var desc = new DXGI.SwapChainDescription1()
                    {
                        BufferCount = 2,
                        AlphaMode = DXGI.AlphaMode.Premultiplied,
                        SampleDescription = new DXGI.SampleDescription(1, 0),
                        Usage = DXGI.Usage.RenderTargetOutput,
                        SwapEffect = DXGI.SwapEffect.FlipDiscard,
                        Format = DXGI.Format.B8G8R8A8_UNorm,
                        Width = form.Width,
                        Height = form.Height,
                    };

                    this.swapChain = new DXGI.SwapChain1(dxgiFactory, dxgiDevice, ref desc, null);

                    this.deviceComp = new DComp.Device(dxgiDevice);
                    this.compositionTarget = DComp.Target.FromHwnd(this.deviceComp, form.Handle, true);

                    using (var visual = new DComp.Visual(this.deviceComp))
                    {
                        visual.SetContent(this.swapChain);
                        this.compositionTarget.SetRoot(visual);
                    }

                    this.deviceComp.Commit();
                }
            }

            using (var device = this.device.QueryInterface<DXGI.Device>())
            {
                this.device2d = new D2D.Device(this.factory2d, device);
            }

            this.deviceContext2d = new D2D.DeviceContext(this.device2d, D2D.DeviceContextOptions.None)
            {
                DotsPerInch = this.factory2d.DesktopDpi,
                AntialiasMode = D2D.AntialiasMode.Aliased,
            };

            this.CreateResources();
        }

        /// <summary>
        /// Gets the Direct2D factory.
        /// </summary>
        public D2D.Factory1 Factory => this.factory2d;

        /// <summary>
        /// Gets the Direct2D device.
        /// </summary>
        public D2D.Device Device2D => this.device2d;

        /// <summary>
        /// Gets the Direc3D device.
        /// </summary>
        public D3D11.Device Device => this.device;

        /// <summary>
        /// Gets the DesktopDpi.
        /// </summary>
        public SharpDX.Size2F Dpi => this.factory2d.DesktopDpi;

        /// <summary>
        /// Gets the Direct2D DeviceContext.
        /// </summary>
        public D2D.DeviceContext DeviceContext => this.deviceContext2d;

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            this.ReleaseResources();
            this.renderBitmap?.Dispose();
            this.deviceContext2d.Dispose();
            this.device2d.Dispose();
            this.compositionTarget.Dispose();
            this.deviceComp.Dispose();
#if DEBUG
            this.deviceDebug.Dispose();
#endif
            this.device.Dispose();
            this.swapChain.Dispose();
            this.factory2d.Dispose();
            this.factoryDWrite.Dispose();
        }

        /// <summary>
        /// Draw the form.
        /// </summary>
        /// <param name="controls">The children controls.</param>
        public void Draw(IList<IElement> controls)
        {
            if (this.backBitmap == null)
            {
                return;
            }

            this.deviceContext2d.BeginDraw();
            this.deviceContext2d.Target = this.renderBitmap;
            this.deviceContext2d.Clear(new RawColor4(0, 0, 0, 0));

            foreach (var control in controls)
            {
                var boundary = new RawRectangleF()
                {
                    Top = control.Position.Y,
                    Left = control.Position.X,
                    Bottom = control.Position.Y + control.Size.Height,
                    Right = control.Position.X + control.Size.Width,
                };

                this.deviceContext2d.PushAxisAlignedClip(boundary, D2D.AntialiasMode.Aliased);
                control.Draw(this.deviceContext2d);
                this.deviceContext2d.PopAxisAlignedClip();
            }

            this.deviceContext2d.Target = null;
            this.deviceContext2d.EndDraw();

            this.backBitmap.CopyFromBitmap(this.renderBitmap);
            this.device.ImmediateContext.Flush();
            this.swapChain.Present(1, DXGI.PresentFlags.None);
        }

        /// <summary>
        /// Resize.
        /// </summary>
        public void Resize()
        {
            this.ReleaseResources();
            this.swapChain.ResizeBuffers(2, this.form.Width, this.form.Height, DXGI.Format.B8G8R8A8_UNorm, DXGI.SwapChainFlags.None);
            this.CreateResources();

            // this.deviceDebug.ReportLiveDeviceObjects(D3D11.ReportingLevel.Detail);
        }

        private void ReleaseResources()
        {
            this.backBuffer?.Dispose();
            this.backBitmap?.Dispose();
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

                this.backBitmap = new D2D.Bitmap(this.deviceContext2d, surface, properties);

                if (this.renderBitmap != null)
                {
                    this.backBitmap.CopyFromBitmap(this.renderBitmap);
                }
            }

            if (this.renderBitmap != null)
            {
                this.renderBitmap.Dispose();
            }

            var p = new D2D.BitmapProperties1(
                new D2D.PixelFormat(DXGI.Format.B8G8R8A8_UNorm, D2D.AlphaMode.Premultiplied),
                this.factory2d.DesktopDpi.Width,
                this.factory2d.DesktopDpi.Height,
                D2D.BitmapOptions.Target);

            this.renderBitmap = new D2D.Bitmap1(
                this.deviceContext2d,
                Helpers.GetPixelSize(this.backBitmap.Size, this.factory2d.DesktopDpi),
                p);
            this.renderBitmap.CopyFromBitmap(this.backBitmap);
        }
    }
}
