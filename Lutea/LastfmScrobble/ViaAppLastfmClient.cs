﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO.Pipes;
using Microsoft.Win32;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.LastfmScrobble
{
    /* Copyright 205-2009, Last.fm Ltd. <client@last.fm>                       
     * All rights reserved.
     *
     * Redistribution and use in source and binary forms, with or without
     * modification, are permitted provided that the following conditions are met:
     *     * Redistributions of source code must retain the above copyright
     *       notice, this list of conditions and the following disclaimer.
     *     * Redistributions in binary form must reproduce the above copyright
     *       notice, this list of conditions and the following disclaimer in the
     *       documentation and/or other materials provided with the distribution.
     *     * Neither the name of the <organization> nor the
     *       names of its contributors may be used to endorse or promote products
     *       derived from this software without specific prior written permission.
     *
     * THIS SOFTWARE IS PROVIDED BY <copyright holder> ''AS IS'' AND ANY
     * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
     * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
     * DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
     * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
     * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
     * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
     * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
     * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
     * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
     */
    /// <summary>
    /// last.fm scrobbler(via last.fm desktop application).
    /// This class is re-implemented from ScrobSubmitter class at https://github.com/lastfm/lastfm-desktop/tree/HEAD/plugins/scrobsub .
    /// </summary>
    class ViaAppLastfmClient : ILastfmClient
    {
        private const string PLUGIN_ID = "lutea";
        private const string PIPE_NAME = "lastfm_scrobsub_";
        private const int RESPONSE_BUFFER_SIZE = 1024;
        private const int PIPE_TIMEOUT = 3000; // ms
        private NamedPipeClientStream Pipe;
        private WorkerThread WThread;

        public ViaAppLastfmClient()
        {
            WThread = new WorkerThread();
            WThread.AddTask(() =>
            {
                LaunchClient();
                var name = PIPE_NAME + System.Security.Principal.WindowsIdentity.GetCurrent().User.ToString();
                Pipe = new NamedPipeClientStream(".", name);
                SendToAS("INIT", new Dictionary<string, string> { 
                    { "f", Environment.CommandLine }
                });
            });
        }

        ~ViaAppLastfmClient()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (Pipe != null)
            {
                try
                {
                    SendToAS("STOP");
                    SendToAS("TERM");
                    WThread.WaitDoneAllTask(PIPE_TIMEOUT);
                    Pipe.WaitForPipeDrain();
                    Pipe.Dispose();
                }
                catch { }
                Pipe = null;
            }
        }

        public void OnTrackChange(int i)
        {
            SendToAS("STOP");
            if (i != -1)
            {
                SendToAS("START", new Dictionary<string, string> { 
                    { "a", Controller.Current.MetaData("tagArtist") },
                    { "d", Controller.Current.MetaData("tagAlbumArtist") },
                    { "t", Controller.Current.MetaData("tagTitle") },
                    { "b", Controller.Current.MetaData("tagAlbum") },
                    { "l", ((int)Controller.Current.Length).ToString() },
                    { "p", Controller.Current.MetaData("file_name") },
                });
            }
        }

        public void OnExceedThresh()
        {
            // nothing
        }

        public void OnPause()
        {
            SendToAS("PAUSE");
        }

        public void OnResume()
        {
            SendToAS("RESUME");
        }

        private string SendToASRawIntl(byte[] fullCommand)
        {
            lock (this)
            {
                if (!Pipe.IsConnected) Pipe.Connect(PIPE_TIMEOUT);
                if (!Pipe.IsConnected) throw new Exception("");
                var inbuffer = new byte[RESPONSE_BUFFER_SIZE];
                Pipe.Write(fullCommand, 0, fullCommand.Length);
                Pipe.Read(inbuffer, 0, inbuffer.Length);
                return Encoding.UTF8.GetString(inbuffer.TakeWhile(_ => _ != '\0').ToArray()).Trim();
            }
        }

        private void SendToASRaw(byte[] fullCommand)
        {
            WThread.AddTask(() => {
                try
                {
                    var ret = SendToASRawIntl(fullCommand);
                    if (ret != "OK")
                    {
                        Logger.Warn("Last.fm app returned other than OK: " + ret);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            });
        }

        private void SendToAS(string command, IDictionary<string, string> param = null)
        {
            var paramsList = new List<string>();
            paramsList.Add("c=" + PLUGIN_ID);
            if (param != null)
            {
                paramsList.AddRange(param.Select(_ => _.Key + "=" + _.Value.Replace("\n", "; ").Replace("&", "&&")));
            }
            var fullCommand = String.Format("{0} {1}\n", command, string.Join("&", paramsList));
            SendToASRaw(Encoding.UTF8.GetBytes(fullCommand));
        }

        private void LaunchClient()
        {
            string exePath = 
                ReadClientAppPathFromRegistry(Registry.CurrentUser) ??
                ReadClientAppPathFromRegistry(Registry.LocalMachine);

            if (exePath == null)
            {
                throw new Exception("last.fm application not found");
            }

            try
            {
                var result = System.Diagnostics.Process.Start(exePath, "--tray");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                throw new Exception("could no launch last.fm app");
            }
        }

        private string ReadClientAppPathFromRegistry(RegistryKey parentKey)
        {
            var regkey = parentKey.OpenSubKey(@"Software\Last.fm\Client");
            if (regkey == null) return null;
            var value = regkey.GetValue("Path");
            if (!(value is string)) return null;
            return (string)value;
        }
    }
}
