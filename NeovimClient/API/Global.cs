// <copyright file="Global.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.NeovimClient.API
{
    using System.Collections.Generic;

    /// <summary>
    /// Global API functions
    /// </summary>
    public class Global
    {
        private readonly MsgPackRpc msgPackRpc;

        /// <summary>
        /// Initializes a new instance of the <see cref="Global"/> class.
        /// </summary>
        /// <param name="msgPackRpc">The RPC client</param>
        public Global(MsgPackRpc msgPackRpc)
        {
            this.msgPackRpc = msgPackRpc;
        }

        /// <summary>
        /// Input keys
        /// </summary>
        /// <param name="keys">String of keys</param>
        public void Input(string keys)
        {
            this.msgPackRpc.SendRequest("nvim_input", new List<object>() { keys });
        }
    }
}
