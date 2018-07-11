// <copyright file="TextAnalysisSink.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Utilities
{
    using System;
    using System.Collections.Generic;
    using SharpDX;
    using DWrite = SharpDX.DirectWrite;

    /// <summary>
    /// Text analysis sink.
    /// </summary>
    public class TextAnalysisSink : DWrite.TextAnalysisSink
    {
        /// <inheritdoc />
        public IDisposable Shadow { get; set; }

        /// <summary>
        /// Gets the script analyses.
        /// </summary>
        public List<(int CodePointStart, int CodePointLength, DWrite.ScriptAnalysis ScriptAnalysis)> ScriptAnalyses { get; }
            = new List<(int, int, DWrite.ScriptAnalysis)>();

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
        public Result QueryInterface(ref Guid guid, out IntPtr comObject)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public int Release()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void SetBidiLevel(int textPosition, int textLength, byte explicitLevel, byte resolvedLevel)
        {
        }

        /// <inheritdoc />
        public void SetLineBreakpoints(int textPosition, int textLength, DWrite.LineBreakpoint[] lineBreakpoints)
        {
        }

        /// <inheritdoc />
        public void SetNumberSubstitution(int textPosition, int textLength, DWrite.NumberSubstitution numberSubstitution)
        {
        }

        /// <inheritdoc />
        public void SetScriptAnalysis(int textPosition, int textLength, DWrite.ScriptAnalysis scriptAnalysis)
        {
            this.ScriptAnalyses.Add((textPosition, textLength, scriptAnalysis));
        }
    }
}
