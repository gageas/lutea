using System;
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
    class ViaAppLastfmClient : AbstractLastfmClient
    {
        private const string PLUGIN_ID = "lutea";
        private const string PIPE_NAME = "lastfm_scrobsub_";
        private const int RESPONSE_BUFFER_SIZE = 1024;
        private const int PIPE_TIMEOUT = 3000; // ms
        private NamedPipeClientStream pipe;
        private WorkerThread wthread;

        public ViaAppLastfmClient()
        {
            wthread = new WorkerThread();
            wthread.AddTask(() =>
            {
                LaunchClient();
                var name = PIPE_NAME + System.Security.Principal.WindowsIdentity.GetCurrent().User.ToString();
                pipe = new NamedPipeClientStream(".", name);
                SendToAS("INIT", new Dictionary<string, string> { 
                    { "f", Environment.CommandLine }
                });
            });
        }

        ~ViaAppLastfmClient()
        {
            Dispose();
        }

        public override void Dispose()
        {
            if (pipe != null)
            {
                try
                {
                    SendToAS("STOP");
                    SendToAS("TERM");
                    wthread.WaitDoneAllTask(PIPE_TIMEOUT);
                    pipe.WaitForPipeDrain();
                    pipe.Dispose();
                }
                catch { }
                pipe = null;
            }
        }

        public override void OnTrackChange(int i)
        {
            SendToAS("STOP");
            if (i != -1)
            {
                var param = new Dictionary<string, string>();

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

        public override void OnExceedThresh()
        {
            // nothing
        }

        public override void OnPause()
        {
            SendToAS("PAUSE");
        }

        public override void OnResume()
        {
            SendToAS("RESUME");
        }

        private string SendToASIntl(byte[] fullCommand)
        {
            lock (this)
            {
                if (!pipe.IsConnected) pipe.Connect(PIPE_TIMEOUT);
                if (!pipe.IsConnected) throw new Exception("");
                var inbuffer = new byte[RESPONSE_BUFFER_SIZE];
                pipe.Write(fullCommand, 0, fullCommand.Length);
                pipe.Read(inbuffer, 0, inbuffer.Length);
                return Encoding.UTF8.GetString(inbuffer);
            }
        }

        private void SendToAS(byte[] fullCommand)
        {
            wthread.AddTask(() => {
                try
                {
                    var ret = SendToASIntl(fullCommand);
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
            var commandAndPID = command + " c=" + PLUGIN_ID;
            var fullCommand = Encoding.UTF8.GetBytes(commandAndPID
                + ((param == null)
                    ? ""
                    : ("&" + param.Select(_ => _.Key + "=" + _.Value.Replace("\n", "; ").Replace("&", "&&")).Aggregate((x, y) => x + "&" + y)))
                + "\n");
            SendToAS(fullCommand);
        }

        private void LaunchClient()
        {
            string exePath = ReadClientAppPathFromRegistry(Registry.CurrentUser);
            if (exePath == null)
            {
                exePath = ReadClientAppPathFromRegistry(Registry.LocalMachine);
            }
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
