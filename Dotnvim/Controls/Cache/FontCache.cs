﻿// <copyright file="FontCache.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Cache
{
    using System;
    using System.Collections.Generic;
    using Dotnvim.Controls.Utilities;
    using D2D = SharpDX.Direct2D1;
    using DWrite = SharpDX.DirectWrite;

    /// <summary>
    /// Font Cache.
    /// </summary>
    public sealed class FontCache : IDisposable
    {
        private const string DefaultFontFamilyName = "Consolas";

        private readonly DWrite.Factory factory;
        private readonly DWrite.FontCollection fontCollection;

        private readonly DWrite.FontFallback systemFontFallback;
        private readonly List<FontFamilyCacheItem> fontFamilies = new List<FontFamilyCacheItem>();

        private string baseFontFamilyName;

        /// <summary>
        /// Initializes a new instance of the <see cref="FontCache"/> class.
        /// </summary>
        /// <param name="factory">DirectWrite factory.</param>
        public FontCache(DWrite.Factory factory)
        {
            this.factory = factory;
            using (var factoryDWrite = factory.QueryInterface<DWrite.Factory2>())
            {
                this.fontCollection = factoryDWrite.GetSystemFontCollection(false);
                this.systemFontFallback = factoryDWrite.SystemFontFallback;
                this.SetPrimaryFontFamily(DefaultFontFamilyName);
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            this.ClearFontCache();
            this.fontCollection.Dispose();
        }

        /// <summary>
        /// Set the primary font family.
        /// </summary>
        /// <param name="fontFamilyName">Font family name.</param>
        /// <returns>True if the name is valid, otherwise false.</returns>
        public bool SetPrimaryFontFamily(string fontFamilyName)
        {
            if (FontFamilyCacheItem.TryCreate(this.fontCollection, fontFamilyName, out var item))
            {
                this.ClearFontCache();
                this.baseFontFamilyName = fontFamilyName;
                this.fontFamilies.Add(item);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get font face from primary font family using specified weight and style.
        /// </summary>
        /// <param name="weight">Font weight.</param>
        /// <param name="style">Font style.</param>
        /// <returns>Selected font face.</returns>
        public DWrite.FontFace GetPrimaryFontFace(DWrite.FontWeight weight, DWrite.FontStyle style)
        {
            return this.fontFamilies[0].GetFontFace(weight, style);
        }

        /// <summary>
        /// Get font face for specified codepoint, weight and style.
        /// </summary>
        /// <param name="codePoint">The code point.</param>
        /// <param name="weight">The font weight.</param>
        /// <param name="style">The font style.</param>
        /// <returns>Selected font face.</returns>
        public DWrite.FontFace GetFontFace(int codePoint, DWrite.FontWeight weight, DWrite.FontStyle style)
        {
            foreach (var f in this.fontFamilies)
            {
                var face = f.GetFontFace(codePoint, weight, style);
                if (face != null)
                {
                    return face;
                }
            }

            using (var source = new SingleCharTextSource(this.factory, codePoint))
            {
                this.systemFontFallback.MapCharacters(
                    source,
                    0,
                    1,
                    this.fontCollection,
                    this.baseFontFamilyName,
                    weight,
                    style,
                    DWrite.FontStretch.Normal,
                    out var mappedLength,
                    out var font,
                    out var scale);

                if (font != null)
                {
                    FontFamilyCacheItem.TryCreate(this.fontCollection, font.FontFamily.FamilyNames.GetString(0), out var familyCache);
                    this.fontFamilies.Add(familyCache);
                    font.Dispose();

                    return familyCache.GetFontFace(codePoint, weight, style);
                }
            }

            // OK we don't have the glyph for this codepoint in the fallbacks.
            // Use primary font to show something like '?'.
            return this.fontFamilies[0].GetFontFace(weight, style);
        }

        private void ClearFontCache()
        {
            foreach (var f in this.fontFamilies)
            {
                f.Dispose();
            }

            this.fontFamilies.Clear();
        }

        private sealed class FontFamilyCacheItem : IDisposable
        {
            private readonly DWrite.FontFamily fontFamily;
            private readonly Dictionary<(DWrite.FontWeight weight, DWrite.FontStyle style), (DWrite.Font font, DWrite.FontFace fontFace)> fontCache
                = new Dictionary<(DWrite.FontWeight, DWrite.FontStyle), (DWrite.Font, DWrite.FontFace)>();

            private FontFamilyCacheItem(DWrite.FontFamily fontFamily)
            {
                this.fontFamily = fontFamily;
            }

            public static bool TryCreate(DWrite.FontCollection collection, string fontFamilyName, out FontFamilyCacheItem item)
            {
                if (collection.FindFamilyName(fontFamilyName, out var index))
                {
                    var fontFamily = collection.GetFontFamily(index);
                    item = new FontFamilyCacheItem(fontFamily);
                    return true;
                }
                else
                {
                    item = null;
                    return false;
                }
            }

            public void Dispose()
            {
                this.fontFamily.Dispose();
                foreach (var (font, fontFace) in this.fontCache.Values)
                {
                    font.Dispose();
                    fontFace.Dispose();
                }
            }

            public DWrite.FontFace GetFontFace(DWrite.FontWeight weight, DWrite.FontStyle style)
            {
                return this.GetOrAdd(weight, style).fontFace;
            }

            public DWrite.FontFace GetFontFace(int codePoint, DWrite.FontWeight weight, DWrite.FontStyle style)
            {
                var (font, fontFace) = this.GetOrAdd(weight, style);

                if (font.HasCharacter(codePoint))
                {
                    return fontFace;
                }

                return null;
            }

            private (DWrite.Font font, DWrite.FontFace fontFace) GetOrAdd(DWrite.FontWeight weight, DWrite.FontStyle style)
            {
                if (!this.fontCache.TryGetValue((weight, style), out var v))
                {
                    var font = this.fontFamily.GetFirstMatchingFont(weight, DWrite.FontStretch.Normal, style);
                    var fontFace = new DWrite.FontFace(font);
                    v = (font, fontFace);
                    this.fontCache.Add((weight, style), v);
                }

                return v;
            }
        }
    }
}
