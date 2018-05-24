// <copyright file="D2DControlWrapper.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf.Controls
{
    using System.Windows;
    using System.Windows.Forms;

    /// <summary>
    /// A wrapper for D2DControl
    /// </summary>
    public class D2DControlWrapper
    {
        private Form form = new TransparentForm();
        private Window owner;
        private FrameworkElement placementTarget;

        /// <summary>
        /// Initializes a new instance of the <see cref="D2DControlWrapper"/> class.
        /// </summary>
        /// <param name="placementTarget">The target to align position</param>
        public D2DControlWrapper(FrameworkElement placementTarget)
        {
            this.placementTarget = placementTarget;
            this.owner = Window.GetWindow(placementTarget);
        }

        private class TransparentForm : Form
        {
            public TransparentForm()
            {
                this.ShowInTaskbar = false;
                this.FormBorderStyle = FormBorderStyle.None;
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
            }
        }
    }
}
