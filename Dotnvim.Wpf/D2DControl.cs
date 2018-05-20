// <copyright file="D2DControl.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.Windows.Forms;

    /// <summary>
    /// A control for D2D displaying
    /// </summary>
    public class D2DControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="D2DControl"/> class.
        /// </summary>
        public D2DControl()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.SetStyle(ControlStyles.Opaque, true);
            this.BackColor = Color.Transparent;

            // this.BackColor = Color.White;
        }

        /// <inheritdoc />
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle = cp.ExStyle | 0x20;
                return cp;
            }
        }

        /// <inheritdoc/>
        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
        }

        /*// <inheritdoc/>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // WM_EXITSIZEMOVE
            if (m.Msg == 0x232)
            {
                Debug.WriteLine("!!!!!");
            }

            // WM_SIZING
            else if (m.Msg == 0x214)
            {
                Debug.WriteLine("---------");
            }

            // WM_SIZE
            else if (m.Msg == 0x5)
            {
                // int param = wParam.ToInt32();
                Debug.WriteLine("??????");

                // SIZE_MAXIMIZED & SIZE_RESTORED
                // if (param == 2 || param == 0)
                {
                    // this.renderer.Resize();
                    // this.neovim.UI.TryResize(this.renderer.DesiredColCount, this.renderer.DesiredRowCount);
                }
            }
        }*/
    }
}
