// <copyright file="MainWindow2.xaml.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;
    using Dotnvim.Wpf.Events;
    using Rendering;

    /// <summary>
    /// Interaction logic for MainWindow2.xaml
    /// </summary>
    public partial class MainWindow2 : Window
    {
        /// <summary>
        /// Foreground color property
        /// </summary>
        public static readonly DependencyProperty NeovimForegroundProperty
            = DependencyProperty.Register("NeovimForeground", typeof(Color), typeof(MainWindow2));

        /// <summary>
        /// Background color property
        /// </summary>
        public static readonly DependencyProperty NeovimBackgroundProperty
            = DependencyProperty.Register("NeovimBackground", typeof(Color), typeof(MainWindow2));

        private const int DefaultBackgroundColor = 0xFFFFFF;
        private const int DefaultForegroundColor = 0x000000;
        private const int DefaultSpecialColor = 0x000000;

        private D2D1Renderer renderer = new D2D1Renderer(true);
        private Neovim neovim;
        private ConcurrentQueue<IList<IRedrawEvent>> redrawEventQueue
            = new ConcurrentQueue<IList<IRedrawEvent>>();

        private bool neovimExited = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow2"/> class.
        /// </summary>
        public MainWindow2()
        {
            this.InitializeComponent();

            // string neovimPath = @"C:\Users\lishengq\Downloads\nvim-win64\Neovim\bin\nvim.exe";
            while (true)
            {
                try
                {
                    this.neovim = new Neovim(Properties.Settings.Default.NeovimPath);
                    break;
                }
                catch (Exception)
                {
                    var dialog = new SettingsDialog("Please specify the path to Neovim");
                    dialog.ShowDialog();
                    if (dialog.CloseReason == SettingsDialog.Result.Cancel)
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                }
            }

            this.Closing += this.MainWindow_Closing;
            this.TextInput += this.MainWindow_TextInput;
            this.KeyDown += this.MainWindow_KeyDown;

            this.neovim.Redraw += this.OnNeovimRedraw;
            this.neovim.NeovimExited += (int exitCode) =>
            {
                this.neovimExited = true;
                Application.Current.Dispatcher.Invoke(this.Close);
            };

            this.Host.Loaded += this.Host_Loaded;
            this.Host.SizeChanged += this.Host_SizeChanged;

            this.Loaded += this.MainWindow2_Loaded;

            Properties.Settings.Default.PropertyChanged += this.Default_PropertyChanged;
        }

        /// <summary>
        /// Gets or sets the foreground color
        /// </summary>
        public Color NeovimForeground
        {
            get
            {
                return (Color)this.GetValue(NeovimForegroundProperty);
            }

            set
            {
                this.SetValue(NeovimForegroundProperty, value);
            }
        }

        /// <summary>
        /// Gets or sets the foreground color
        /// </summary>
        public Color NeovimBackground
        {
            get
            {
                return (Color)this.GetValue(NeovimBackgroundProperty);
            }

            set
            {
                this.EnableBlur(value, Properties.Settings.Default.BackgroundOpacity);
                this.SetValue(NeovimBackgroundProperty, value);
            }
        }

        private void MainWindow2_Loaded(object sender, RoutedEventArgs e)
        {
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(this.HwndProcHook);
            this.DropShadow();
        }

        private void Host_Loaded(object sender, RoutedEventArgs e)
        {
            this.InteropImage.WindowOwner = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            this.InteropImage.OnRender += this.Present;
            this.neovim.UI.Attach(this.renderer.DesiredColCount, this.renderer.DesiredRowCount);
            this.InteropImage.RequestRender();
        }

        private void Host_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double dpiScale = 1.0;
            if (PresentationSource.FromVisual(this).CompositionTarget is HwndTarget hwndTarget)
            {
                dpiScale = hwndTarget.TransformToDevice.M11;
            }

            int surfWidth = (int)(this.Host.ActualWidth < 0 ? 0 : Math.Ceiling(this.Host.ActualWidth * dpiScale));
            int surfHeight = (int)(this.Host.ActualHeight < 0 ? 0 : Math.Ceiling(this.Host.ActualHeight * dpiScale));
            this.InteropImage.SetPixelSize(surfWidth, surfHeight);
            this.renderer.Resize(new SharpDX.Size2F((float)this.Host.ActualWidth, (float)this.Host.ActualHeight));

            if (!this.Host.IsLoaded)
            {
                return;
            }

            this.neovim.UI.TryResize(this.renderer.DesiredColCount, this.renderer.DesiredRowCount);
            this.InteropImage.RequestRender();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (Input.KeyMapping.TryMap(e.KeyboardDevice, e.Key, out var text))
            {
                Debug.WriteLine("Mapped key input: " + text);
                this.neovim.Global.Input(text);
                e.Handled = true;
            }
        }

        private void MainWindow_TextInput(object sender, TextCompositionEventArgs e)
        {
            this.neovim.Global.Input(e.Text);
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!this.neovimExited && MessageBox.Show(this, "Do you want to TERMINATE neovim?", "dotnvim", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            this.neovim.Dispose();

            // Clear the queue
            this.redrawEventQueue = new ConcurrentQueue<IList<IRedrawEvent>>();
            this.renderer.Dispose();
        }

        private void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Properties.Settings.Default.BackgroundOpacity))
            {
                this.EnableBlur(this.NeovimBackground, Properties.Settings.Default.BackgroundOpacity);
            }
        }

        private void OnNeovimRedraw(IList<IRedrawEvent> events)
        {
            this.redrawEventQueue.Enqueue(events);
            Application.Current.Dispatcher.Invoke(this.Render);
        }

        private void Present(System.IntPtr ptr, bool surfaceChanged)
        {
            if (surfaceChanged)
            {
                this.renderer.RenderBufferPtr = ptr;
            }

            this.renderer.Present();
        }

        private void Render()
        {
            if (!this.redrawEventQueue.IsEmpty)
            {
                this.renderer.BeginDraw();
                while (this.redrawEventQueue.TryDequeue(out var events))
                {
                    HighlightSetEvent highlightSetEvent = new HighlightSetEvent();
                    foreach (var ev in events)
                    {
                        switch (ev)
                        {
                            case ResizeEvent e:
                                this.renderer.Resize(0, 0);
                                break;
                            case ClearEvent e:
                                this.renderer.Clear();
                                break;
                            case EolClearEvent e:
                                this.renderer.EolClear();
                                break;
                            case CursorGotoEvent e:
                                this.renderer.CursorGoto((int)e.Row, (int)e.Col);
                                break;
                            case SetTitleEvent e:
                                Application.Current.MainWindow.Title = e.Title;
                                break;
                            case SetIconTitleEvent e:
                                Application.Current.MainWindow.Title = e.Title;
                                break;
                            case PutEvent e:
                                this.renderer.Put(
                                    e.Text,
                                    highlightSetEvent.Foreground,
                                    highlightSetEvent.Background,
                                    highlightSetEvent.Special,
                                    highlightSetEvent.Reverse,
                                    highlightSetEvent.Italic,
                                    highlightSetEvent.Bold,
                                    highlightSetEvent.Underline,
                                    highlightSetEvent.Undercurl);
                                break;
                            case HighlightSetEvent e:
                                highlightSetEvent = e;
                                break;
                            case UpdateFgEvent e:
                                {
                                    int color = e.Color == -1 ? DefaultForegroundColor : e.Color;
                                    this.renderer.UpdateFg(color);

                                    this.NeovimForeground = this.GetColor(color);
                                    break;
                                }

                            case UpdateBgEvent e:
                                {
                                    int color = e.Color == -1 ? DefaultBackgroundColor : e.Color;
                                    this.renderer.UpdateBg(color);

                                    this.NeovimBackground = this.GetColor(color);
                                    break;
                                }

                            case UpdateSpEvent e:
                                {
                                    int color = e.Color == -1 ? DefaultSpecialColor : e.Color;
                                    this.renderer.UpdateSp(color);
                                    break;
                                }

                            case SetScrollRegionEvent e:
                                this.renderer.SetScrollRegion(e.Top, e.Bottom, e.Left, e.Right);
                                break;
                            case ScrollEvent e:
                                this.renderer.Scroll(e.Count);
                                break;
                        }
                    }
                }

                this.renderer.EndDraw();
                this.InteropImage.RequestRender();
            }
        }

        /// <summary>
        /// CloseButton_Clicked
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// MaximizedButton_Clicked
        /// </summary>
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.ToggleMaximize();
        }

        /// <summary>
        /// Minimized Button_Clicked
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsDialog = new SettingsDialog();
            settingsDialog.ShowDialog();
        }

        private void ToggleMaximize()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private Color GetColor(int color)
        {
            byte b = (byte)(color % 256);
            color /= 256;
            byte g = (byte)(color % 256);
            color /= 256;
            byte r = (byte)(color % 256);

            return Color.FromRgb(r, g, b);
        }

        private Color Lighten(Color color, float factor = 0.25f)
        {
            return Color.FromRgb(
                (byte)(color.R + ((255 - color.R) * factor)),
                (byte)(color.G + ((255 - color.G) * factor)),
                (byte)(color.B + ((255 - color.B) * factor)));
        }

        private IntPtr HwndProcHook(System.IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case 0x0024: /* WM_GETMINMAXINFO */
                    NativeInterop.Methods.WmGetMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }

            return IntPtr.Zero;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.neovim.UI.TryResize(80, 25);
        }
    }
}
