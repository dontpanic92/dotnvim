// <copyright file="D3D9SurfaceAdapter.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf.Rendering
{
    using System;
    using D3D11 = SharpDX.Direct3D11;
    using D3D9 = SharpDX.Direct3D9;
    using DXGI = SharpDX.DXGI;

    /// <summary>
    /// A helper class for adapting a D3D11 Texture to a D3D9 surface
    /// </summary>
    public sealed class D3D9SurfaceAdapter : IDisposable
    {
        private D3D9.Direct3DEx factory;
        private D3D9.DeviceEx device;

        /// <summary>
        /// Initializes a new instance of the <see cref="D3D9SurfaceAdapter"/> class.
        /// </summary>
        /// <param name="hwnd">Focus indow hwnd</param>
        public D3D9SurfaceAdapter(IntPtr hwnd)
        {
            this.factory = new D3D9.Direct3DEx();

            var parameters = new D3D9.PresentParameters()
            {
                Windowed = true,
                SwapEffect = D3D9.SwapEffect.Discard,
                DeviceWindowHandle = hwnd,
                PresentationInterval = D3D9.PresentInterval.Immediate,
            };

            this.device = new D3D9.DeviceEx(this.factory, 0, D3D9.DeviceType.Hardware, hwnd, D3D9.CreateFlags.Multithreaded | D3D9.CreateFlags.HardwareVertexProcessing | D3D9.CreateFlags.FpuPreserve, parameters);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            this.device.Dispose();
            this.factory.Dispose();
        }

        /// <summary>
        /// Get a shared D3D9 surface from a D3D11 texture
        /// </summary>
        /// <param name="texture">D3D11 texture</param>
        /// <returns>D3D9Surface</returns>
        public D3D9.Surface GetD3D9Surface(D3D11.Texture2D texture)
        {
            if (texture == null)
            {
                return null;
            }

            using (var surface = texture.QueryInterface<DXGI.Resource>())
            {
                var handle = surface.SharedHandle;
                using (var texture9 = new D3D9.Texture(
                    this.device,
                    texture.Description.Width,
                    texture.Description.Height,
                    1,
                    D3D9.Usage.RenderTarget,
                    D3D9.Format.A8R8G8B8,
                    D3D9.Pool.Default,
                    ref handle))
                {
                    var surface9 = texture9.GetSurfaceLevel(0);
                    return surface9;
                }
            }
        }
    }
}
