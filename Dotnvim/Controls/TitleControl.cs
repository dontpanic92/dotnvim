// <copyright file="TitleControl.cs">
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
    using SharpDX.Mathematics.Interop;
    using D2D = SharpDX.Direct2D1;
    using DWrite = SharpDX.DirectWrite;

    /// <summary>
    /// The Title Control.
    /// </summary>
    public class TitleControl : ControlBase
    {
        private const float PaddingHorizontal = 8;
        private readonly RawVector2 origin = new RawVector2(PaddingHorizontal, 0);

        private DWrite.Factory dwriteFactory = new DWrite.Factory();
        private DWrite.TextFormat textFormat;
        private DWrite.TextLayout textLayout;
        private D2D.Brush textBrush;

        private string text = "dotnvim";
        private RawColor4 color;

        /// <summary>
        /// Initializes a new instance of the <see cref="TitleControl"/> class.
        /// </summary>
        /// <param name="parent">The parent control.</param>
        public TitleControl(IElement parent)
            : base(parent)
        {
            this.textFormat = new DWrite.TextFormat(this.dwriteFactory, "Segoe UI", Helpers.GetFontSize(10));
            this.textFormat.SetParagraphAlignment(DWrite.ParagraphAlignment.Center);
            this.textFormat.SetTextAlignment(DWrite.TextAlignment.Trailing);
            this.textFormat.SetWordWrapping(DWrite.WordWrapping.NoWrap);
            var sign = new DWrite.EllipsisTrimming(this.dwriteFactory, this.textFormat);
            this.textFormat.SetTrimming(new DWrite.Trimming() { Granularity = DWrite.TrimmingGranularity.Character, Delimiter = 0, DelimiterCount = 0 }, sign);
            sign.Dispose();

            this.Color = new RawColor4(1, 1, 1, 1);
        }

        /// <summary>
        /// Gets or sets the title.
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
        /// Gets or sets the text color.
        /// </summary>
        public RawColor4 Color
        {
            get
            {
                return this.color;
            }

            set
            {
                this.color = value;

                this.textBrush?.Dispose();
                this.textBrush = new D2D.SolidColorBrush(this.DeviceContext, this.color);

                this.Invalidate();
            }
        }

        /// <inheritdoc />
        public override void Layout()
        {
            base.Layout();

            this.textLayout?.Dispose();

            var width = this.Size.Width - (PaddingHorizontal * 2);
            this.textLayout = new DWrite.TextLayout(this.dwriteFactory, this.text, this.textFormat, width <= 0 ? 1 : width, this.Size.Height);
        }

        /// <inheritdoc />
        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            this.textFormat.Dispose();
            this.textLayout?.Dispose();
            this.textBrush?.Dispose();
            this.dwriteFactory.Dispose();
        }

        /// <inheritdoc />
        protected override void Draw()
        {
            this.DeviceContext.BeginDraw();
            this.DeviceContext.Clear(new RawColor4(0, 0, 0, 0));

            this.DeviceContext.DrawTextLayout(this.origin, this.textLayout, this.textBrush);

            this.DeviceContext.EndDraw();
        }
    }
}
