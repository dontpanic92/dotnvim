// <copyright file="Cursor.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf.Rendering
{
    using System;
    using SharpDX.Mathematics.Interop;
    using D2D = SharpDX.Direct2D1;

    /// <summary>
    /// Text Cursor renderer
    /// </summary>
    public sealed class Cursor : IDisposable
    {
        private readonly D2D.DeviceContext deviceContext;

        private readonly D2D.Effect cropEffect;
        private readonly D2D.Effect invertEffect;
        private readonly D2D.Effect colorMatrixEffect;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cursor"/> class.
        /// </summary>
        /// <param name="deviceContext">The D2D device context</param>
        public Cursor(D2D.DeviceContext deviceContext)
        {
            this.deviceContext = deviceContext;
            this.cropEffect = new D2D.Effect(this.deviceContext, D2D.Effect.Crop);
            this.invertEffect = new D2D.Effect(this.deviceContext, D2D.Effect.Invert);
            this.colorMatrixEffect = new D2D.Effect(this.deviceContext, D2D.Effect.ColorMatrix);

            // Set alpha = 1
            var colorMatrix = new RawMatrix5x4()
            {
                 M11 = 1,
                 M22 = 1,
                 M33 = 1,
                 M54 = 1,
            };

            this.invertEffect.SetInputEffect(0, this.cropEffect);
            this.colorMatrixEffect.SetInputEffect(0, this.invertEffect);
            this.colorMatrixEffect.SetValue((int)D2D.ColorMatrixProperties.ColorMatrix, colorMatrix);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            this.cropEffect.Dispose();
            this.invertEffect.Dispose();
            this.colorMatrixEffect.Dispose();
        }

        /// <summary>
        /// Draw cursor
        /// </summary>
        /// <param name="bitmap">The render target</param>
        /// <param name="cursorRect">The rect for cursor drawing</param>
        public void DrawCursor(D2D.Bitmap bitmap, RawRectangleF cursorRect)
        {
            this.cropEffect.SetInput(0, bitmap, true);
            this.cropEffect.SetValue((int)D2D.CropProperties.Rectangle, cursorRect);

            this.deviceContext.DrawImage(
                this.colorMatrixEffect.Output,
                new RawVector2(0, 0),
                D2D.InterpolationMode.NearestNeighbor);
        }
    }
}
