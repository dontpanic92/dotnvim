﻿// <copyright file="CursorBlinking.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.NeovimClient.Utilities
{
    /// <summary>
    /// The blinking status for the cursor.
    /// </summary>
    public enum CursorBlinking
    {
        /// <summary>
        /// BlinkWait.
        /// </summary>
        BlinkWait,

        /// <summary>
        /// BlinkOn.
        /// </summary>
        BlinkOn,

        /// <summary>
        /// BlinkOff.
        /// </summary>
        BlinkOff,
    }
}
