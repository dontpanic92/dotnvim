// <copyright file="KeyMapping.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Wpf.Input
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows.Input;

    /// <summary>
    /// Define the key mapping from a WPF key to vim input
    /// </summary>
    public static class KeyMapping
    {
        private static Dictionary<Key, string> specialKeys = new Dictionary<Key, string>()
        {
            { Key.Up, "Up" },
            { Key.Down, "Down" },
            { Key.Left, "Left" },
            { Key.Right, "Right" },
            { Key.Help, "Help" },
            { Key.Insert, "Insert" },
            { Key.Home, "Home" },
            { Key.End, "End" },
            { Key.PageUp, "PageUp" },
            { Key.PageDown, "PageDown" },
            { Key.Enter, "Enter" },
            { Key.Delete, "Del" },
            { Key.F1, "F1" },
            { Key.F2, "F2" },
            { Key.F3, "F3" },
            { Key.F4, "F4" },
            { Key.F5, "F5" },
            { Key.F6, "F6" },
            { Key.F7, "F7" },
            { Key.F8, "F8" },
            { Key.F9, "F9" },
            { Key.F10, "F10" },
            { Key.F11, "F11" },
            { Key.F12, "F12" },
        };

        /// <summary>
        /// Mapping a key to a vim recognizable text
        /// </summary>
        /// <param name="device">The keyboard device</param>
        /// <param name="key">The key</param>
        /// <param name="text">Converted text</param>
        /// <returns>Whether the key has a map</returns>
        public static bool TryMap(KeyboardDevice device, Key key, out string text)
        {
            text = string.Empty;
            if (!TryMapKey(key, out text, out var needEscape))
            {
                return false;
            }

            if (text == "<")
            {
                text = "lt";
                needEscape = true;
            }
            else if (text == "\\")
            {
                text = "Bslash";
                needEscape = true;
            }

            var modifierPrefix = GetModifierPrefix(device);

            if (needEscape || !string.IsNullOrEmpty(modifierPrefix))
            {
                text = "<" + modifierPrefix + text + ">";
            }

            return true;
        }

        private static string GetModifierPrefix(KeyboardDevice device)
        {
            var text = string.Empty;
            if (device.IsKeyDown(Key.LeftCtrl) || device.IsKeyDown(Key.RightCtrl))
            {
                text += "C-";
            }

            if (device.IsKeyDown(Key.LeftShift) || device.IsKeyDown(Key.RightShift))
            {
                text += "S-";
            }

            if (device.IsKeyDown(Key.LeftAlt) || device.IsKeyDown(Key.RightAlt))
            {
                text += "M-";
            }

            return text;
        }

        private static bool TryMapKey(Key key, out string text, out bool needEscape)
        {
            if (specialKeys.TryGetValue(key, out text))
            {
                needEscape = true;
                return true;
            }
            else
            {
                needEscape = false;
                int virtualKey = KeyInterop.VirtualKeyFromKey(key);
                text = NativeInterop.Methods.VirtualKeyToString(virtualKey);
                if (text == null)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
