// <copyright file="MainWindow.xaml.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;
    using Dotnvim.Wpf;
    using Dotnvim.Wpf.Events;
    using Dotnvim.Wpf.Rendering;
    using SharpDX.Direct2D1;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // private D2D1Renderer renderer = new D2D1Renderer();
        private D2D1HwndRenderer renderer;
        private Neovim neovim;
        private ConcurrentQueue<IList<IRedrawEvent>> redrawEventQueue
            = new ConcurrentQueue<IList<IRedrawEvent>>();

        private bool neovimExited = false;
        private bool neovimAttached = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();

            string neovimPath = @"C:\Users\lishengq\Downloads\nvim-win64\Neovim\bin\nvim.exe";
            this.neovim = new Neovim(neovimPath);

            this.Closing += this.MainWindow_Closing;
            this.TextInput += this.MainWindow_TextInput;
            this.KeyDown += this.MainWindow_KeyDown;

            this.neovim.Redraw += this.OnNeovimRedraw;
            this.neovim.NeovimExited += (int exitCode) =>
            {
                this.neovimExited = true;
                Application.Current.Dispatcher.Invoke(this.Close);
            };

            this.renderer = new D2D1HwndRenderer(this.wfLabel.Handle);
            this.wfLabel.Resize += this.WfLabel_Resize;

            this.wfLabel.Paint += this.WfLabel_Paint;
        }

        private void WfLabel_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            this.renderer.Present();
        }

        private void ResizeFinished(object sender, ElapsedEventArgs e)
        {
            this.neovim.UI.TryResize(this.renderer.DesiredColCount, this.renderer.DesiredRowCount);
        }

        private void WfLabel_Resize(object sender, EventArgs e)
        {
            this.renderer.Resize();
            if (!this.neovimAttached)
            {
                this.neovim.UI.Attach(this.renderer.DesiredColCount, this.renderer.DesiredRowCount);
                this.neovimAttached = true;
            }
            else
            {
                this.neovim.UI.TryResize(this.renderer.DesiredColCount, this.renderer.DesiredRowCount);
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!this.neovimExited && MessageBox.Show(this, "Do you really want to TERMINATE neovim?", "dotnvim", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            this.neovim.Dispose();

            // Clear the queue
            this.redrawEventQueue = new ConcurrentQueue<IList<IRedrawEvent>>();
            this.renderer.Dispose();
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Input.KeyMapping.TryMap(e.KeyboardDevice, e.Key, out var text))
            {
                Debug.WriteLine("Mapped key input: " + text);
                this.neovim.Global.Input(text);
                e.Handled = true;
            }

            if (e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.LeftCtrl))
            {
                Debug.WriteLine("keydown: Ctrl + " + e.Key);
            }
            else
            {
                Debug.WriteLine("Key: " + e.Key + " SystemKey: " + e.SystemKey + " deadkey: " + e.DeadCharProcessedKey);
            }
        }

        private void MainWindow_TextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            this.neovim.Global.Input(e.Text);
        }

        private void OnNeovimRedraw(IList<IRedrawEvent> events)
        {
            this.redrawEventQueue.Enqueue(events);
            Application.Current.Dispatcher.Invoke(this.Render);
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
                                this.renderer.UpdateFg(e.Color);
                                break;
                            case UpdateBgEvent e:
                                this.renderer.UpdateBg(e.Color);
                                break;
                            case UpdateSpEvent e:
                                this.renderer.UpdateSp(e.Color);
                                break;
                        }
                    }
                }

                this.renderer.EndDraw();
                this.renderer.Present();
            }
        }
    }
}
