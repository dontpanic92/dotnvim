// <copyright file="Neovim.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf
{
    using Dotnvim.NeovimClient;

    /// <summary>
    /// The default neovim client
    /// </summary>
    public class Neovim : Neovim<Events.IRedrawEvent>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Neovim"/> class.
        /// </summary>
        /// <param name="path">The path to neovim</param>
        public Neovim(string path)
            : base(path, new DefaultRedrawEventFactory())
        {
        }
    }
}
