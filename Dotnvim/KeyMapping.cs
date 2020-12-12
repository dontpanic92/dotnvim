// <copyright file="KeyMapping.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows.Forms;
    using System.Windows.Input;

    /// <summary>
    /// Define the key mapping from a Winforms key to vim input.
    /// </summary>
    public static class KeyMapping
    {
        private static Dictionary<Keys, string> specialKeys = new Dictionary<Keys, string>()
        {
            { Keys.Back, "Bs" },
            { Keys.Tab, "Tab" },
            { Keys.LineFeed, "NL" },
            { Keys.Return, "CR" },
            { Keys.Escape, "Esc" },
            { Keys.Space, "Space" },
            { Keys.OemBackslash, "Bslash" },
            { Keys.Delete, "Del" },
            { Keys.Up, "Up" },
            { Keys.Down, "Down" },
            { Keys.Left, "Left" },
            { Keys.Right, "Right" },
            { Keys.Help, "Help" },
            { Keys.Insert, "Insert" },
            { Keys.Home, "Home" },
            { Keys.End, "End" },
            { Keys.PageUp, "PageUp" },
            { Keys.PageDown, "PageDown" },
            { Keys.F1, "F1" },
            { Keys.F2, "F2" },
            { Keys.F3, "F3" },
            { Keys.F4, "F4" },
            { Keys.F5, "F5" },
            { Keys.F6, "F6" },
            { Keys.F7, "F7" },
            { Keys.F8, "F8" },
            { Keys.F9, "F9" },
            { Keys.F10, "F10" },
            { Keys.F11, "F11" },
            { Keys.F12, "F12" },
        };

        /// <summary>
        /// Mapping a key to a vim recognizable text.
        /// </summary>
        /// <param name="e">The key event.</param>
        /// <param name="text">Converted text.</param>
        /// <returns>Whether the key has a map.</returns>
        public static bool TryMap(System.Windows.Forms.KeyEventArgs e, out string text)
        {
            if (specialKeys.TryGetValue(e.KeyCode, out text))
            {
                text = NativeInterop.Methods.DecorateInput(text, e.Control, e.Shift, e.Alt);
            }
            else
            {
               text = NativeInterop.Methods.VirtualKeyToString((int)e.KeyCode);
            }

            return text != null;
        }
    }
}
