// <copyright file="WindowBlurExtensions.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

/*
 Reference: https://github.com/riverar/sample-win32-acrylicblur
 License: MIT
 */

namespace Dotnvim
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Drawing;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;
    using Dotnvim.Utilities;

    /// <summary>
    /// Extensions for blur windows.
    /// </summary>
    [SuppressMessage("StyleCop", "SA1600", Justification = "Undocumented APIs")]
    [SuppressMessage("StyleCop", "SA1602", Justification = "Undocumented APIs")]
    public static class WindowBlurExtensions
    {
        /// <summary>
        /// The type of blur.
        /// </summary>
        public enum BlurType
        {
            /// <summary>
            /// Disable
            /// </summary>
            Disabled = -1,

            /// <summary>
            /// Gaussian Blur
            /// </summary>
            GaussianBlur = 0,

            /// <summary>
            /// AcrylicBlur
            /// </summary>
            AcrylicBlur = 1,
        }

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
        /// Try enable blur to a window.
        /// </summary>
        /// <param name="window">this window.</param>
        /// <param name="backgroundColor">background color.</param>
        /// <param name="blurOpacity">opacity.</param>
        /// <param name="intBlurType">the blur type.</param>
        public static void BlurBehind(this Form window, Color backgroundColor, double blurOpacity, int intBlurType)
        {
            if (!Helpers.BlurBehindAvailable())
            {
                return;
            }

            BlurBehindInternal(window, backgroundColor, blurOpacity, BlurType.Disabled);

            if (!Helpers.BlurBehindEnabled())
            {
                return;
            }

            BlurBehindInternal(window, backgroundColor, blurOpacity, (BlurType)intBlurType);
        }

        private static void BlurBehindInternal(Form window, Color backgroundColor, double blurOpacity, BlurType intBlurType)
        {
            var blurType = (BlurType)intBlurType;

            if (blurType == BlurType.AcrylicBlur && !Helpers.AcrylicBlurAvailable())
            {
                return;
            }

            var accentState = AccentState.ACCENT_DISABLED;
            switch (blurType)
            {
                case BlurType.GaussianBlur:
                    accentState = AccentState.ACCENT_ENABLE_BLURBEHIND;
                    break;
                case BlurType.AcrylicBlur:
                    accentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND;
                    break;
                default:
                    accentState = AccentState.ACCENT_DISABLED;
                    break;
            }

            uint backgroundColorValue = ((uint)backgroundColor.B << 16) + ((uint)backgroundColor.G << 8) + ((uint)backgroundColor.R);
            uint blurOpacityValue = 0; // (uint)(blurOpacity * 255);

            var accent = new AccentPolicy()
            {
                AccentState = accentState,
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

            SetWindowCompositionAttribute(window.Handle, ref data);

            Marshal.FreeHGlobal(accentPtr);
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public uint AccentFlags;
            public uint GradientColor;
            public uint AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }
    }
}
