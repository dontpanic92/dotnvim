// <copyright file="Settings.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf.ABC
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    /// Settings
    /// </summary>
    public class Settings
    {
        private Settings()
        {
        }

        /// <summary>
        /// Gets or sets the path to neovim
        /// </summary>
        public string NeovimPath { get; set; }

        /// <summary>
        /// Gets or sets the opacity of background
        /// </summary>
        public double BackgroundOpacity { get; set; } = 0.7;

        /// <summary>
        /// Load settings from config file
        /// </summary>
        /// <returns>Loaded settings</returns>
        public static Settings Load()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string configFile = Path.Combine(localAppData, "dotnvim", "settings.json");

            try
            {
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(configFile));
            }
            catch (Exception)
            {
                return new Settings();
            }
        }
    }
}
