// <copyright file="BoxLayout.cs">
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
    using Dotnvim.Controls;
    using Dotnvim.Events;
    using SharpDX;
    using SharpDX.Direct2D1;
    using SharpDX.Mathematics.Interop;

    /// <summary>
    /// Box Layout.
    /// </summary>
    public abstract class BoxLayout : ElementBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BoxLayout"/> class.
        /// </summary>
        /// <param name="parent">Parent control.</param>
        public BoxLayout(IElement parent)
            : base(parent)
        {
        }

        /// <summary>
        /// Gets or sets the padding inside the layout.
        /// </summary>
        public float Padding { get; set; } = 0;

        /// <summary>
        /// Gets the controls.
        /// </summary>
        protected List<(IElement Control, ChildState State)> Controls { get; } = new List<(IElement, ChildState)>();

        /// <summary>
        /// Add a control as a child.
        /// </summary>
        /// <param name="control">The child control.</param>
        /// <param name="strench">Whether it should strench.</param>
        public void AddControl(IElement control, bool strench = false)
        {
            if (this.Controls.Any(c => c.Control == control))
            {
                return;
            }

            this.Controls.Add((control, new ChildState { Strentch = strench, IsMouseOver = false }));
        }

        /// <summary>
        /// Draw onto the context.
        /// </summary>
        /// <param name="deviceContext">the device context.</param>
        public override void Draw(DeviceContext deviceContext)
        {
            deviceContext.Clear(new RawColor4(0, 0, 0, 0));
            foreach (var (control, _) in this.Controls)
            {
                var boundary = new RawRectangleF()
                {
                    Top = control.Position.Y,
                    Left = control.Position.X,
                    Bottom = control.Position.Y + control.Size.Height,
                    Right = control.Position.X + control.Size.Width,
                };

                deviceContext.PushAxisAlignedClip(boundary, AntialiasMode.Aliased);
                control.Draw(deviceContext);
                deviceContext.PopAxisAlignedClip();
            }
        }

        /// <inheritdoc />
        public override void OnMouseMove(MouseEvent e)
        {
            base.OnMouseMove(e);

            foreach (var (control, state) in this.Controls)
            {
                if (control.HitTest(e.Point))
                {
                    if (!state.IsMouseOver)
                    {
                        state.IsMouseOver = true;
                        control.OnMouseEnter(e);
                    }

                    control.OnMouseMove(e);
                }
                else
                {
                    if (state.IsMouseOver)
                    {
                        state.IsMouseOver = false;
                        control.OnMouseLeave(e);
                    }
                }
            }
        }

        /// <inheritdoc />
        public override void OnMouseClick(MouseEvent e)
        {
            base.OnMouseClick(e);

            foreach (var (control, state) in this.Controls)
            {
                if (control.HitTest(e.Point))
                {
                    control.OnMouseClick(e);
                }
            }
        }

        /// <inheritdoc />
        public override void OnMouseLeave(MouseEvent e)
        {
            base.OnMouseLeave(e);

            foreach (var (control, state) in this.Controls)
            {
                if (state.IsMouseOver)
                {
                    state.IsMouseOver = false;
                    control.OnMouseLeave(e);
                }
            }
        }

        /// <inheritdoc />
        protected override void DisposeManaged()
        {
            foreach (var (control, _) in this.Controls)
            {
                control.Dispose();
            }
        }

        /// <summary>
        /// Represents the state for children controls.
        /// </summary>
        protected class ChildState
        {
            /// <summary>
            /// Gets or sets a value indicating whether it should strench.
            /// </summary>
            public bool Strentch { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the mouse is over on it.
            /// </summary>
            public bool IsMouseOver { get; set; }
        }
    }
}
