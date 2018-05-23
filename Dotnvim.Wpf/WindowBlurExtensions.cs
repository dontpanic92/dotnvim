// <copyright file="WindowBlurExtensions.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

/*
 Reference: https://github.com/riverar/sample-win32-acrylicblur
 License: MIT
 */

namespace Dotnvim.Wpf
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;

    /// <summary>
    /// Extensions for blur windows
    /// </summary>
    [SuppressMessage("StyleCop", "SA1600", Justification = "Undocumented APIs")]
    [SuppressMessage("StyleCop", "SA1602", Justification = "Undocumented APIs")]
    public static class WindowBlurExtensions
    {
        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 5,
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19,
        }

        /// <summary>
        /// Try enable blur to a window
        /// </summary>
        /// <param name="window">this window</param>
        /// <param name="backgroundColor">background color</param>
        /// <param name="blurOpacity">opacity</param>
        public static void EnableBlur(this Window window, Color backgroundColor, double blurOpacity)
        {
            var windowHelper = new WindowInteropHelper(window);
            uint backgroundColorValue = ((uint)backgroundColor.B << 16) + ((uint)backgroundColor.G << 8) + ((uint)backgroundColor.R);
            uint blurOpacityValue = (uint)(blurOpacity * 255);

            var accent = new AccentPolicy()
            {
                AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                GradientColor = (blurOpacityValue << 24) | (backgroundColorValue & 0xFFFFFF),
            };

            var accentStructSize = Marshal.SizeOf(accent);

            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData()
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr,
            };

            SetWindowCompositionAttribute(windowHelper.Handle, ref data);

            Marshal.FreeHGlobal(accentPtr);
        }

        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public uint AccentFlags;
            public uint GradientColor;
            public uint AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }
    }
}
