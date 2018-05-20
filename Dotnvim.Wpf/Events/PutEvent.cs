﻿// <copyright file="PutEvent.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf.Events
{
    using System.Collections.Generic;

    /// <summary>
    /// The Put event
    /// </summary>
    public class PutEvent : IRedrawEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PutEvent"/> class.
        /// </summary>
        /// <param name="text">The text to be rendered</param>
        public PutEvent(IList<string> text)
        {
            this.Text = text;
        }

        /// <summary>
        /// Gets the text
        /// </summary>
        public IList<string> Text { get; }
    }
}
