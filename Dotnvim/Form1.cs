// <copyright file="Form1.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Drawing;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Dotnvim.Controls;
    using Dotnvim.Events;
    using Dotnvim.NeovimClient.Events;
    using Dotnvim.Utilities;
    using SharpDX;
    using SharpDX.Direct2D1;
    using SharpDX.Mathematics.Interop;
    using static Dotnvim.NeovimClient.NeovimClient;

    /// <summary>
    /// The Mainform.
    /// </summary>
    public partial class Form1 : Form, IElement
    {
        private const float TitleBarHeight = 28;
        private const float BorderWidth = 6.5f;
        private const float DwmBorderSize = 1;

        private FormRenderer renderer;
        private int backgroundColor = -1;
        private NeovimClient.NeovimClient neovimClient;
        private VerticalLayout layout;
        private NeovimControl neovimControl;
        private LogoControl logoControl;
        private TitleControl titleControl;
        private ButtonControl settingsButton;
        private ButtonControl minimizeButton;
        private ButtonControl maximizeButton;
        private ButtonControl closeButton;

        private Size formerSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="Form1"/> class.
        /// </summary>
        public Form1()
        {
            this.InitializeComponent();

            while (true)
            {
                try
                {
                    this.neovimClient = new NeovimClient.NeovimClient(Properties.Settings.Default.NeovimPath);
                    break;
                }
                catch (Exception)
                {
                    var dialog = new Dotnvim.Dialogs.SettingsDialog("Please specify the path to Neovim");
                    dialog.ShowDialog();
                    if (dialog.CloseReason == Dotnvim.Dialogs.SettingsDialog.Result.Cancel)
                    {
                        Environment.Exit(0);
                    }
                }
            }
        }

        /// <inheritdoc />
        public Factory1 Factory => this.renderer.Factory;

        /// <inheritdoc />
        public Device Device => this.renderer.Device2D;

        /// <inheritdoc />
        Size2F IElement.Size { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <inheritdoc />
        RawVector2 IElement.Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <inheritdoc />
        protected override CreateParams CreateParams
        {
            get
            {
                var param = base.CreateParams;
                param.ExStyle |= 0x00200000;
                return param;
            }
        }

        /// <inheritdoc />
        protected override bool CanEnableIme => true;

        /// <summary>
        /// A control needs redrawing.
        /// </summary>
        /// <param name="control">The control that needs redrawing.</param>
        public void Invalidate(IElement control)
        {
            this.Invalidate();
        }

        /// <inheritdoc />
        void IElement.Draw(DeviceContext deviceContext)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IElement.Layout()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        bool IElement.HitTest(RawVector2 point)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IElement.OnMouseMove(MouseEvent e)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IElement.OnMouseEnter(MouseEvent e)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IElement.OnMouseLeave(MouseEvent e)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IElement.OnMouseClick(MouseEvent e)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            this.neovimClient.NeovimExited -= this.OnNeovimExited;
            this.neovimClient.Dispose();
            this.layout?.Dispose();
            this.renderer?.Dispose();
        }

        /// <inheritdoc />
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            KeyMapping.TryMap(e, out var text);
            if (!string.IsNullOrEmpty(text))
            {
                this.neovimClient.Input(text);
                e.Handled = true;
            }
        }

        /// <inheritdoc />
        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            this.OnResize();
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.InitializeControls();

            this.BlurBehind(Color.FromArgb(255, 255, 255, 255), Properties.Settings.Default.BackgroundOpacity, Properties.Settings.Default.BlurType);

            var dwmBorderSize = Helpers.GetPixelSize(new Size2F(DwmBorderSize, DwmBorderSize), this.renderer.Dpi);
            NativeInterop.Methods.ExtendFrame(this.Handle, dwmBorderSize.Width, dwmBorderSize.Height);

            Properties.Settings.Default.PropertyChanged += this.Default_PropertyChanged;

            this.OnResize();
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            var backgroundColor = Helpers.GetColor(this.backgroundColor);
            if (Helpers.BlurBehindEnabled())
            {
                backgroundColor.A = (float)Properties.Settings.Default.BackgroundOpacity;
            }

            this.renderer.Draw(new List<IElement>() { this.layout }, backgroundColor, 0);
        }

        /// <inheritdoc />
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x24: // WM_GETMINMAXINFO
                    NativeInterop.Methods.WmGetMinMaxInfo(this.Handle, m.LParam);
                    return;
                case 0x83: // WM_NCCALCSIZE
                    m.Result = (IntPtr)0xF0;
                    return;
                case 0x84: // WM_NCHITTEST
                    var titleBarSize = Helpers.GetPixelSize(new Size2F(1, TitleBarHeight), this.renderer.Factory.DesktopDpi);
                    var borderSize = Helpers.GetPixelSize(new Size2F(BorderWidth, BorderWidth), this.renderer.Factory.DesktopDpi);
                    m.Result = NativeInterop.Methods.NCHitTest(
                        this.Handle,
                        m.LParam,
                        borderSize.Width,
                        borderSize.Height,
                        this.WindowState == FormWindowState.Maximized ? titleBarSize.Height : titleBarSize.Height - borderSize.Height,
                        (int x, int y) =>
                        {
                            if (this.renderer != null)
                            {
                                var point = Helpers.GetDipPoint(x, y, this.renderer.Factory.DesktopDpi);
                                return this.settingsButton.HitTest(point)
                                    || this.minimizeButton.HitTest(point)
                                    || this.maximizeButton.HitTest(point)
                                    || this.closeButton.HitTest(point);
                            }

                            return false;
                        });

                    return;
                case 0x0286: // WM_IME_CHAR
                    char ch = (char)m.WParam.ToInt64();
                    this.neovimControl.Input(ch.ToString());
                    break;
            }

            base.WndProc(ref m);

            switch (m.Msg)
            {
                case 0x112: // WM_SYSCOMMAND
                    int wParam = m.WParam.ToInt32() & 0xFFF0;
                    if (wParam == 0xF030 || wParam == 0xF020 || wParam == 0xF120)
                    {
                        // wParam == 0xF000 || SC_MAXIMIZE || SC_MINIMIZE || SC_RESTORE
                        this.OnResize();
                    }

                    break;
                case 0xA3: // WM_NCLBUTTONDBLCLK
                    this.OnResize();
                    break;
            }
        }

        /// <inheritdoc />
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        /// <inheritdoc />
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            MouseEvent.Buttons button = MouseEvent.Buttons.None;
            switch (e.Button)
            {
                case MouseButtons.Left:
                    button = MouseEvent.Buttons.Left;
                    break;
                case MouseButtons.Right:
                    button = MouseEvent.Buttons.Right;
                    break;
            }

            var point = Helpers.GetDipPoint(e.X, e.Y, this.renderer.Factory.DesktopDpi);
            var mouseEvent = new MouseEvent(MouseEvent.Type.MouseMove, point, button);
            this.layout.OnMouseMove(mouseEvent);
        }

        /// <inheritdoc />
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            var point = Helpers.GetDipPoint(-1, -1, this.renderer.Factory.DesktopDpi);
            var mouseEvent = new MouseEvent(MouseEvent.Type.MouseMove, point, MouseEvent.Buttons.None);
            this.layout.OnMouseLeave(mouseEvent);
        }

        /// <inheritdoc />
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (e.Button == MouseButtons.Left)
            {
                var point = Helpers.GetDipPoint(e.X, e.Y, this.renderer.Factory.DesktopDpi);
                var mouseEvent = new MouseEvent(MouseEvent.Type.MouseClick, point, MouseEvent.Buttons.Left);
                this.layout.OnMouseClick(mouseEvent);
            }
        }

        private void OnNeovimExited(int exitCode)
        {
            this.BeginInvoke(new MethodInvoker(() =>
            {
                this.Close();
            }));
        }

        private void OnResize()
        {
            if (this.Size == this.formerSize)
            {
                return;
            }

            this.formerSize = this.Size;

            if (this.renderer != null)
            {
                this.renderer.Resize();

                if (this.WindowState == FormWindowState.Maximized)
                {
                    this.layout.Padding = 6.5f;
                }
                else
                {
                    this.layout.Padding = DwmBorderSize;
                }

                this.layout.Size = Helpers.GetDipSize(new Size2(this.Width, this.Height), this.renderer.Factory.DesktopDpi);
                this.layout.Layout();
                this.Invalidate();
            }
        }

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Properties.Settings.EnableBlurBehind):
                case nameof(Properties.Settings.Default.BlurType):
                    var color = Helpers.GetColor(this.backgroundColor);
                    this.BlurBehind(
                        Color.FromArgb((int)(color.A * 255), (int)(color.R * 255), (int)(color.G * 255), (int)(color.B * 255)),
                        Properties.Settings.Default.BackgroundOpacity,
                        Properties.Settings.Default.BlurType);
                    this.Invalidate();
                    break;
                case nameof(Properties.Settings.BackgroundOpacity):
                    this.Invalidate();
                    break;
                case nameof(Properties.Settings.EnableLigature):
                    this.neovimControl.EnableLigature = Properties.Settings.Default.EnableLigature;
                    this.Invalidate();
                    break;
            }
        }

        private void InitializeControls()
        {
            this.renderer = new FormRenderer(this);
            this.layout = new VerticalLayout(this)
            {
                Size = Helpers.GetDipSize(new Size2(this.Width, this.Height), this.renderer.Factory.DesktopDpi),
            };

            this.neovimClient.NeovimExited += this.OnNeovimExited;

            this.neovimControl = new NeovimControl(this.layout, this.neovimClient);
            this.neovimControl.EnableLigature = Properties.Settings.Default.EnableLigature;

            this.neovimClient.BackgroundColorChanged += (int intColor) =>
            {
                this.backgroundColor = intColor;
                this.Invoke(new MethodInvoker(() =>
                {
                    var color = Helpers.GetColor(intColor);
                    this.BlurBehind(
                        Color.FromArgb((int)(color.A * 255), (int)(color.R * 255), (int)(color.G * 255), (int)(color.B * 255)),
                        Properties.Settings.Default.BackgroundOpacity,
                        Properties.Settings.Default.BlurType);
                }));
            };

            var buttonSize = Helpers.GetDipSize(
                new Size2(SystemInformation.CaptionButtonSize.Width, SystemInformation.CaptionButtonSize.Height),
                this.renderer.Factory.DesktopDpi);

            var titleBar = new HorizontalLayout(this.layout)
            {
                Size = new Size2F(1, TitleBarHeight),
            };
            this.logoControl = new LogoControl(titleBar);
            this.titleControl = new TitleControl(titleBar);
            this.settingsButton = new ButtonControl(titleBar, "⚙", buttonSize)
            {
                Click = () =>
                {
                    var dialog = new Dotnvim.Dialogs.SettingsDialog();
                    dialog.ShowDialog();
                },
            };
            this.minimizeButton = new ButtonControl(titleBar, "🗕", buttonSize)
            {
                Click = () =>
                {
                    this.WindowState = FormWindowState.Minimized;
                    this.OnResize();
                },
            };
            this.maximizeButton = new ButtonControl(titleBar, "🗖", buttonSize)
            {
                Click = () =>
                {
                    this.WindowState =
                       this.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                    this.OnResize();
                },
            };
            this.closeButton = new ButtonControl(titleBar, "✕", buttonSize)
            {
                Click = this.Close,
            };
            titleBar.AddControl(this.logoControl);
            titleBar.AddControl(this.titleControl, true);
            titleBar.AddControl(this.settingsButton);
            titleBar.AddControl(this.minimizeButton);
            titleBar.AddControl(this.maximizeButton);
            titleBar.AddControl(this.closeButton);

            this.neovimClient.TitleChanged += (string title) =>
            {
                this.BeginInvoke(new MethodInvoker(() =>
                {
                    this.Text = title;
                }));
                this.titleControl.Text = title;
            };

            this.neovimClient.ForegroundColorChanged += (int intColor) =>
            {
                var color = Helpers.GetColor(intColor);
                this.titleControl.Color = color;
                this.settingsButton.ForegroundColor = color;
                this.minimizeButton.ForegroundColor = color;
                this.maximizeButton.ForegroundColor = color;
                this.closeButton.ForegroundColor = color;
            };

            this.neovimClient.BackgroundColorChanged += (int intColor) =>
            {
                var color = Helpers.GetColor(intColor);
                this.settingsButton.BackgroundColor = color;
                this.minimizeButton.BackgroundColor = color;
                this.maximizeButton.BackgroundColor = color;
                this.closeButton.BackgroundColor = color;
            };

            this.layout.AddControl(titleBar);
            this.layout.AddControl(this.neovimControl, true);
            this.layout.Layout();
        }
    }
}
