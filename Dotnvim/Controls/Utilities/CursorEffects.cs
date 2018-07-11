// <copyright file="CursorEffects.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Utilities
{
    using System;
    using System.Linq;
    using SharpDX;
    using SharpDX.Mathematics.Interop;
    using D2D = SharpDX.Direct2D1;

    /// <summary>
    /// Text Cursor renderer.
    /// </summary>
    public sealed class CursorEffects : EffectChain
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CursorEffects"/> class.
        /// </summary>
        /// <param name="deviceContext">The D2D device context.</param>
        public CursorEffects(D2D.DeviceContext deviceContext)
            : base(deviceContext)
        {
            // Set alpha = 1
            var colorMatrix = new RawMatrix5x4()
            {
                M11 = 1,
                M22 = 1,
                M33 = 1,
                M54 = 1,
            };

            this.PushEffect(D2D.Effect.Crop)
                    .SetupLast((e) => e.SetValue((int)D2D.CropProperties.Rectangle, new RawRectangleF(0, 0, 0, 0)))
                .PushEffect(D2D.Effect.Invert)
                .PushEffect(D2D.Effect.ColorMatrix)
                    .SetupLast((e) => e.SetValue((int)D2D.ColorMatrixProperties.ColorMatrix, colorMatrix))
                .SetCompositionMode(D2D.CompositeMode.DestinationOver);
        }

        /// <summary>
        /// Set the cursor boundary.
        /// </summary>
        /// <param name="cursorRect">The cursor rect.</param>
        public void SetCursorRect(RawRectangleF cursorRect)
        {
            this.Effects[0].SetValue((int)D2D.CropProperties.Rectangle, cursorRect);
        }
    }
}
