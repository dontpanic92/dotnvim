// <copyright file="ElementBase.cs">
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
    using Dotnvim.Events;
    using SharpDX;
    using SharpDX.Direct2D1;
    using SharpDX.Mathematics.Interop;
    using D3D11 = SharpDX.Direct3D11;

    /// <summary>
    /// The base class for elements.
    /// </summary>
    public abstract class ElementBase : IElement
    {
        private bool isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ElementBase"/> class.
        /// </summary>
        /// <param name="parent">The parent control.</param>
        public ElementBase(IElement parent)
        {
            this.Parent = parent;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ElementBase"/> class.
        /// </summary>
        ~ElementBase()
        {
            this.Dispose(false);
        }

        /// <inheritdoc />
        public virtual Size2F Size { get; set; }

        /// <inheritdoc />
        public virtual RawVector2 Position { get; set; }

        /// <inheritdoc />
        public Factory1 Factory => this.Parent.Factory;

        /// <inheritdoc />
        public Device Device2D => this.Parent.Device2D;

        /// <inheritdoc />
        public D3D11.Device Device => this.Parent.Device;

        /// <summary>
        /// Gets the parent.
        /// </summary>
        protected IElement Parent { get; }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public abstract void Draw(DeviceContext deviceContext);

        /// <inheritdoc />
        public abstract void Layout();

        /// <inheritdoc />
        public void Invalidate(IElement control)
        {
            this.Parent.Invalidate(control);
        }

        /// <inheritdoc />
        public virtual void OnMouseMove(MouseEvent e)
        {
        }

        /// <inheritdoc />
        public virtual void OnMouseEnter(MouseEvent e)
        {
        }

        /// <inheritdoc />
        public virtual void OnMouseLeave(MouseEvent e)
        {
        }

        /// <inheritdoc />
        public virtual void OnMouseClick(MouseEvent e)
        {
        }

        /// <inheritdoc />
        public bool HitTest(RawVector2 point)
        {
            return point.X >= this.Position.X
                && point.Y >= this.Position.Y
                && point.X <= this.Position.X + this.Size.Width
                && point.Y <= this.Position.Y + this.Size.Height;
        }

        /// <summary>
        /// Dispose managed objects.
        /// </summary>
        protected virtual void DisposeManaged()
        {
        }

        /// <summary>
        /// Dispose unmanaged objects.
        /// </summary>
        protected virtual void DisposeUnmanaged()
        {
        }

        private void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.DisposeManaged();
                }

                this.DisposeUnmanaged();
                this.isDisposed = true;
            }
        }
    }
}
