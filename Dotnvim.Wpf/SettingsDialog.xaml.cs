// <copyright file="SettingsDialog.xaml.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf
{
    using System.Windows;

    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsDialog"/> class.
        /// </summary>
        /// <param name="promptText">Text for prompt</param>
        public SettingsDialog(string promptText = null)
        {
            this.InitializeComponent();

            if (!string.IsNullOrWhiteSpace(promptText))
            {
                this.PromptLabel.Content = promptText;
                this.PromptLabel.Visibility = Visibility.Visible;
            }

            this.Closing += this.SettingsDialog_Closing;
        }

        /// <summary>
        /// Reason of closing the window
        /// </summary>
        public enum Result
        {
            /// <summary>
            /// Window is not closed yet
            /// </summary>
            NotClosed,

            /// <summary>
            /// Window closed due to Ok button clicked
            /// </summary>
            Ok,

            /// <summary>
            /// Window closed due to Cancel button clicked
            /// </summary>
            Cancel,
        }

        /// <summary>
        /// Gets the reason of closing the window
        /// </summary>
        public Result CloseReason { get; private set; } = Result.NotClosed;

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            this.CloseReason = Result.Ok;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.CloseReason = Result.Cancel;
            this.Close();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files(*.*)|*.*",
                RestoreDirectory = true,
                Multiselect = false,
            };

            if (openFileDialog.ShowDialog() == true)
            {
                this.NeovimPath.Text = Properties.Settings.Default.NeovimPath = openFileDialog.FileName;
            }
        }

        private void SettingsDialog_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            switch (this.CloseReason)
            {
                case Result.Ok:
                    Properties.Settings.Default.Save();
                    break;
                case Result.Cancel:
                    Properties.Settings.Default.Reload();
                    break;
                case Result.NotClosed:
                    this.CloseReason = Result.Cancel;
                    Properties.Settings.Default.Reload();
                    break;
            }
        }
    }
}
