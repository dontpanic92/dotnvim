// <copyright file="SingleCharTextSource.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Utilities
{
    using System;
    using SharpDX;
    using DWrite = SharpDX.DirectWrite;

    /// <summary>
    /// The text source that holds a single char.
    /// </summary>
    public class SingleCharTextSource : DWrite.TextAnalysisSource
    {
        private readonly DWrite.Factory factory;
        private readonly int codePoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="SingleCharTextSource"/> class.
        /// </summary>
        /// <param name="factory">The directwrite factory.</param>
        /// <param name="codePoint">The codepoint.</param>
        public SingleCharTextSource(DWrite.Factory factory, int codePoint)
        {
            this.factory = factory;
            this.codePoint = codePoint;
        }

        /// <inheritdoc />
        public DWrite.ReadingDirection ReadingDirection => DWrite.ReadingDirection.LeftToRight;

        /// <inheritdoc />
        public IDisposable Shadow { get; set; }

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
            textLength = 1;
            return System.Threading.Thread.CurrentThread.CurrentCulture.Name;
        }

        /// <inheritdoc />
        public DWrite.NumberSubstitution GetNumberSubstitution(int textPosition, out int textLength)
        {
            textLength = 1;
            return new DWrite.NumberSubstitution(this.factory, DWrite.NumberSubstitutionMethod.None, null, true);
        }

        /// <inheritdoc />
        public string GetTextAtPosition(int textPosition)
        {
            // if (this.screen[this.row, textPosition].Character == null)
            if (textPosition != 0)
            {
                return string.Empty;
            }

            // return char.ConvertFromUtf32(this.screen[this.row, textPosition].Character.Value);
            return char.ConvertFromUtf32(this.codePoint);
        }

        /// <inheritdoc />
        public string GetTextBeforePosition(int textPosition)
        {
            // if (this.screen[this.row, textPosition - 1].Character == null)
            if (textPosition != 0)
            {
                return string.Empty;
            }

            // return char.ConvertFromUtf32(this.screen[this.row, textPosition - 1].Character.Value);
            return char.ConvertFromUtf32(this.codePoint);
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
