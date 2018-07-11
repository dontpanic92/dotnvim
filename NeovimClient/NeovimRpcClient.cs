﻿// <copyright file="NeovimRpcClient.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.NeovimClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Lowlevel neovim client that is responsible for communicate with Neovim.
    /// </summary>
    /// <typeparam name="TRedrawEvent">The base redraw event.</typeparam>
    public class NeovimRpcClient<TRedrawEvent> : IDisposable
    {
        private readonly MsgPackRpc msgPackRpc;
        private readonly IRedrawEventFactory<TRedrawEvent> factory;
        private readonly Process process;
        private bool disposedValue = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="NeovimRpcClient{TRedrawEventFactory}"/> class.
        /// </summary>
        /// <param name="path">The path to neovim executable.</param>
        /// <param name="factory">The painter used for drawing UI.</param>
        public NeovimRpcClient(string path, IRedrawEventFactory<TRedrawEvent> factory)
        {
            this.factory = factory;

            this.process = new Process
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = path,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = Path.GetDirectoryName(path),
                    Arguments = "--headless --embed",
                },

                EnableRaisingEvents = true,
            };
            this.process.Exited += this.Process_Exited;
            this.process.Start();

            this.msgPackRpc = new MsgPackRpc(
                this.process.StandardInput.BaseStream,
                this.process.StandardOutput.BaseStream,
                this.NotificationDispatcher);

            this.UI = new API.UI(this.msgPackRpc);
            this.Global = new API.Global(this.msgPackRpc);
        }

        /// <summary>
        /// A delegate type that indicates Neovim exits.
        /// </summary>
        /// <param name="code">The exit code.</param>
        public delegate void NeovimExitedEventHandler(int code);

        /// <summary>
        /// A delegate type that indicates Neovim needs redrawing.
        /// </summary>
        /// <param name="events">The list of redraw event.</param>
        public delegate void RedrawHandler(IList<TRedrawEvent> events);

        /// <summary>
        /// Gets or sets the callback functions that will be called when Neovim crashs.
        /// </summary>
        public NeovimExitedEventHandler NeovimExited { get; set; }

        /// <summary>
        /// Gets the apis of UI part.
        /// </summary>
        public API.UI UI { get; }

        /// <summary>
        /// Gets the apis of Global.
        /// </summary>
        public API.Global Global { get; }

        /// <summary>
        /// Gets or sets the Redraw handlers.
        /// </summary>
        public RedrawHandler Redraw { get; set; }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Is Dispose called.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.msgPackRpc?.Dispose();
                }

                this.disposedValue = true;
            }
        }

        private static MsgPack.MessagePackObject? TryGetValueFromDictionary(MsgPack.MessagePackObjectDictionary dict, string key)
        {
            if (dict.TryGetValue(key, out var value))
            {
                return value;
            }

            return null;
        }

        private void NotificationDispatcher(string name, IList<MsgPack.MessagePackObject> rawEvents)
        {
            if (name != "redraw")
            {
                Trace.WriteLine("Unexpected notification received" + name);
                return;
            }

            List<TRedrawEvent> events = new List<TRedrawEvent>();
            foreach (var rawEvent in rawEvents)
            {
                var cmd = rawEvent.AsList();
                var eventName = cmd[0].AsString();

                // Debug.WriteLine("event: " + string.Join(" ", cmd));
                switch (eventName)
                {
                    case "set_title":
                        {
                            var title = cmd[1].AsList()[0].AsStringUtf8();
                            events.Add(this.factory.CreateSetTitleEvent(title));
                            break;
                        }

                    case "set_icon":
                        {
                            var title = cmd[1].AsList()[0].AsStringUtf8();
                            events.Add(this.factory.CreateSetIconTitleEvent(title));
                            break;
                        }

                    case "mode_info_set":
                        {
                            /*var args = cmd[1].AsList();
                            var cursorStyleEnabled = args[0].AsBoolean();
                            var mode = args[1].AsList().Select(
                                item => (IDictionary<string, string>)item.AsDictionary().ToDictionary(
                                    k => k.Key.AsStringUtf8(),
                                    v => v.Value.AsStringUtf8())).ToList();
                            events.Add(this.factory.CreateModeInfoSetEvent(cursorStyleEnabled, mode));*/
                            break;
                        }

                    case "cursor_goto":
                        {
                            var list = cmd[1].AsList();
                            uint row = list[0].AsUInt32();
                            uint col = list[1].AsUInt32();
                            events.Add(this.factory.CreateCursorGotoEvent(row, col));
                            break;
                        }

                    case "put":
                        {
                            IList<int?> result = new List<int?>();
                            for (int i = 1; i < cmd.Count; i++)
                            {
                                var ch = string.Join(string.Empty, cmd[i].AsEnumerable().Select(t => t.AsString()));
                                int? codepoint = null;
                                if (ch != string.Empty)
                                {
                                    codepoint = char.ConvertToUtf32(ch, 0);
                                }

                                result.Add(codepoint);
                            }

                            events.Add(this.factory.CreatePutEvent(result));
                            break;
                        }

                    case "clear":
                        {
                            events.Add(this.factory.CreateClearEvent());
                            break;
                        }

                    case "eol_clear":
                        {
                            events.Add(this.factory.CreateEolClearEvent());
                            break;
                        }

                    case "resize":
                        {
                            var list = cmd[1].AsList();
                            uint col = list[0].AsUInt32();
                            uint row = list[1].AsUInt32();
                            events.Add(this.factory.CreateResizeEvent(row, col));
                            break;
                        }

                    case "highlight_set":
                        {
                            var dict = cmd[1].AsList()[0].AsDictionary();
                            int? foreground = TryGetValueFromDictionary(dict, "foreground")?.AsInt32();
                            int? background = TryGetValueFromDictionary(dict, "background")?.AsInt32();
                            int? special = TryGetValueFromDictionary(dict, "special")?.AsInt32();
                            bool reverse = TryGetValueFromDictionary(dict, "reverse")?.AsBoolean() == true;
                            bool italic = TryGetValueFromDictionary(dict, "italic")?.AsBoolean() == true;
                            bool bold = TryGetValueFromDictionary(dict, "bold")?.AsBoolean() == true;
                            bool underline = TryGetValueFromDictionary(dict, "underline")?.AsBoolean() == true;
                            bool undercurl = TryGetValueFromDictionary(dict, "undercurl")?.AsBoolean() == true;

                            events.Add(this.factory.CreateHightlightSetEvent(
                                foreground,
                                background,
                                special,
                                reverse,
                                italic,
                                bold,
                                underline,
                                undercurl));

                            break;
                        }

                    case "update_fg":
                        {
                            var color = cmd[1].AsList()[0].AsInt32();
                            events.Add(this.factory.CreateUpdateFgEvent(color));
                            break;
                        }

                    case "update_bg":
                        {
                            var color = cmd[1].AsList()[0].AsInt32();
                            events.Add(this.factory.CreateUpdateBgEvent(color));
                            break;
                        }

                    case "update_sp":
                        {
                            var color = cmd[1].AsList()[0].AsInt32();
                            events.Add(this.factory.CreateUpdateSpEvent(color));
                            break;
                        }

                    case "set_scroll_region":
                        {
                            var list = cmd[1].AsList();
                            events.Add(this.factory.CreateSetScrollRegionEvent(
                                list[0].AsInt32(),
                                list[1].AsInt32(),
                                list[2].AsInt32(),
                                list[3].AsInt32()));
                            break;
                        }

                    case "scroll":
                        {
                            int count = cmd[1].AsList()[0].AsInt32();
                            events.Add(this.factory.CreateScrollEvent(count));
                            break;
                        }
                }
            }

            this.Redraw?.Invoke(events);
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            this.NeovimExited?.Invoke(this.process.ExitCode);
        }
    }
}
