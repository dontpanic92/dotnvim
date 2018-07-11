// <copyright file="ButtonControl.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using Dotnvim.Events;
    using SharpDX;
    using SharpDX.Mathematics.Interop;
    using D2D = SharpDX.Direct2D1;
    using DWrite = SharpDX.DirectWrite;

    /// <summary>
    /// The button control.
    /// </summary>
    public class ButtonControl : ControlBase
    {
        private readonly RawVector2 origin = new RawVector2(0, 0);

        private DWrite.Factory dwriteFactory = new DWrite.Factory();
        private DWrite.TextFormat textFormat;
        private DWrite.TextLayout textLayout;
        private D2D.Brush foregroundBrush;
        private D2D.Brush backgroundBrush;
        private string text = string.Empty;
        private RawColor4 backgroundColor;
        private RawColor4 foregroundColor;

        private bool isMouseOver = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ButtonControl"/> class.
        /// </summary>
        /// <param name="parent">The parent control.</param>
        /// <param name="text">The text on the button.</param>
        /// <param name="size">Button size.</param>
        public ButtonControl(IElement parent, string text = "", Size2F? size = null)
            : base(parent)
        {
            this.textFormat = new DWrite.TextFormat(this.dwriteFactory, "Segoe UI", Helpers.GetFontSize(10));
            this.textFormat.SetParagraphAlignment(DWrite.ParagraphAlignment.Center);
            this.textFormat.SetTextAlignment(DWrite.TextAlignment.Center);
            this.textFormat.SetWordWrapping(DWrite.WordWrapping.NoWrap);

            this.text = text;

            if (size != null)
            {
                this.Size = size.Value;
                this.Layout();
            }

            this.BackgroundColor = new RawColor4(1, 1, 1, 1);
            this.ForegroundColor = new RawColor4(0, 0, 0, 1);
        }

        /// <summary>
        /// On click event handler type.
        /// </summary>
        public delegate void ButtonClickHandler();

        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        public string Text
        {
            get
            {
                return this.text;
            }

            set
            {
                this.text = value;
                this.Layout();

                this.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the foreground color.
        /// </summary>
        public RawColor4 ForegroundColor
        {
            get
            {
                return this.foregroundColor;
            }

            set
            {
                this.foregroundColor = value;

                this.foregroundBrush?.Dispose();
                this.foregroundBrush = new D2D.SolidColorBrush(this.DeviceContext, this.foregroundColor);

                this.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public RawColor4 BackgroundColor
        {
            get
            {
                return this.backgroundColor;
            }

            set
            {
                this.backgroundColor = value;

                this.backgroundBrush?.Dispose();
                this.backgroundBrush = new D2D.SolidColorBrush(this.DeviceContext, this.backgroundColor);

                this.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the click event.
        /// </summary>
        public ButtonClickHandler Click { get; set; }

        /// <inheritdoc />
        public override void Layout()
        {
            base.Layout();

            this.textLayout?.Dispose();
            this.textLayout = new DWrite.TextLayout(this.dwriteFactory, this.text, this.textFormat, this.Size.Width, this.Size.Height);
        }

        /// <inheritdoc />
        public override void OnMouseEnter(MouseEvent e)
        {
            base.OnMouseEnter(e);
            this.isMouseOver = true;
            this.Invalidate();
        }

        /// <inheritdoc />
        public override void OnMouseLeave(MouseEvent e)
        {
            base.OnMouseLeave(e);
            this.isMouseOver = false;
            this.Invalidate();
        }

        /// <inheritdoc />
        public override void OnMouseClick(MouseEvent e)
        {
            base.OnMouseClick(e);
            this.Click?.Invoke();
        }

        /// <inheritdoc />
        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            this.textFormat.Dispose();
            this.textLayout?.Dispose();
            this.foregroundBrush?.Dispose();
            this.backgroundBrush?.Dispose();
            this.dwriteFactory.Dispose();
        }

        /// <inheritdoc />
        protected override void Draw()
        {
            this.DeviceContext.BeginDraw();
            if (!this.isMouseOver)
            {
                this.DeviceContext.Clear(new RawColor4(0, 0, 0, 0));
                this.DeviceContext.DrawTextLayout(this.origin, this.textLayout, this.foregroundBrush);
            }
            else
            {
                this.DeviceContext.Clear(this.ForegroundColor);
                this.DeviceContext.DrawTextLayout(this.origin, this.textLayout, this.backgroundBrush);
            }

            this.DeviceContext.EndDraw();
        }
    }
}
