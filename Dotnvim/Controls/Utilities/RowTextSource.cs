﻿// <copyright file="RowTextSource.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SharpDX;
    using static Dotnvim.NeovimClient.NeovimClient;
    using DWrite = SharpDX.DirectWrite;

    /// <summary>
    /// The text source stored a row.
    /// </summary>
    public class RowTextSource : DWrite.TextAnalysisSource
    {
        private readonly DWrite.Factory factory;
        private readonly List<string> text = new List<string>();
        private readonly List<int> codePoints = new List<int>();
        private readonly int row;
        private readonly int columnCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="RowTextSource"/> class.
        /// </summary>
        /// <param name="factory">The directwrite factory.</param>
        /// <param name="screen">The cells.</param>
        /// <param name="row">Row index.</param>
        /// <param name="rangeStart">The start index of the range.</param>
        /// <param name="rangeEnd">the end index of the range.</param>
        public RowTextSource(DWrite.Factory factory, Cell[,] screen, int row, int rangeStart, int rangeEnd)
        {
            this.factory = factory;

            for (int j = rangeStart; j < rangeEnd; j++)
            {
                if (screen[row, j].Character != null)
                {
                    int codePoint = screen[row, j].Character.Value;
                    this.text.Add(char.ConvertFromUtf32(codePoint));
                    this.codePoints.Add(codePoint);
                }
            }

            this.row = row;
            this.columnCount = rangeEnd - rangeStart;
        }

        /// <summary>
        /// Gets the length.
        /// </summary>
        public int Length { get => this.text.Count; }

        /// <inheritdoc />
        public DWrite.ReadingDirection ReadingDirection => DWrite.ReadingDirection.LeftToRight;

        /// <inheritdoc />
        public IDisposable Shadow { get; set; }

        /// <summary>
        /// Gets the code point in the specific index.
        /// </summary>
        /// <param name="index">Index.</param>
        /// <returns>The code point.</returns>
        public int GetCodePoint(int index)
        {
            return this.codePoints[index];
        }

        /// <summary>
        /// Gets a substring.
        /// </summary>
        /// <param name="codePointStart">Start index.</param>
        /// <param name="codePointLength">End index.</param>
        /// <returns>The substring.</returns>
        public string GetSubString(int codePointStart, int codePointLength)
        {
            return string.Join(string.Empty, this.text.Skip(codePointStart).Take(codePointLength));
        }

        /// <inheritdoc />
        public int AddReference()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Shadow?.Dispose();
        }

        /// <inheritdoc />
        public string GetLocaleName(int textPosition, out int textLength)
        {
            textLength = this.text.Count;
            return System.Threading.Thread.CurrentThread.CurrentCulture.Name;
        }

        /// <inheritdoc />
        public DWrite.NumberSubstitution GetNumberSubstitution(int textPosition, out int textLength)
        {
            textLength = this.text.Count;
            return new DWrite.NumberSubstitution(this.factory, DWrite.NumberSubstitutionMethod.None, null, true);
        }

        /// <inheritdoc />
        public string GetTextAtPosition(int textPosition)
        {
            return string.Join(string.Empty, this.text.Skip(textPosition));
        }

        /// <inheritdoc />
        public string GetTextBeforePosition(int textPosition)
        {
            return string.Join(string.Empty, this.text.Take(textPosition - 1));
        }

        /// <inheritdoc />
        public Result QueryInterface(ref Guid guid, out IntPtr comObject)
        {
            comObject = IntPtr.Zero;
            return Result.False;
        }

        /// <inheritdoc />
        public int Release()
        {
            throw new NotImplementedException();
        }
    }
}
