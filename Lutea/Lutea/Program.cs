using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;

namespace Gageas.Lutea.Core
{
    public class IpcRemoteObject : MarshalByRefObject
    {
        public void Activate()
        {
            AppCore.ActivateUI();
        }
    }

    static class Program
    {
        private const string APP_NAME = "Gageas.Lutea";
        private const string APP_IF = "remoting";
        private static System.Threading.Mutex _mutex;
        private static IpcRemoteObject RemoteObject;

        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            _mutex = new System.Threading.Mutex(false, APP_NAME);
            if (_mutex.WaitOne(0, false) == false)
            {
                // クライアントチャンネルの生成
                IpcClientChannel cchannel = new IpcClientChannel();

                // チャンネルを登録
                ChannelServices.RegisterChannel(cchannel, true);

                // リモートオブジェクトを取得
                RemoteObject = Activator.GetObject(typeof(IpcRemoteObject), "ipc://" + APP_NAME + "/" + APP_IF) as IpcRemoteObject;
                RemoteObject.Activate();
                ChannelServices.UnregisterChannel(cchannel);
                return;
            }

            // サーバーチャンネルの生成
            IpcServerChannel channel = new IpcServerChannel(APP_NAME);

            // チャンネルを登録
            ChannelServices.RegisterChannel(channel, true);

            // リモートオブジェクトを生成して公開
            RemoteObject = new IpcRemoteObject();
            RemotingServices.Marshal(RemoteObject, APP_IF, typeof(IpcRemoteObject));
   
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
#if DEBUG

            var w = System.IO.File.CreateText(DateTime.Now.ToString().Replace("/","").Replace(":","-") + ".log");
            Logger.LogClient += new Logger.LogEventHandler(log =>
            {
                w.Write(log.ToString() + Environment.NewLine);
                w.Flush();
            });
#endif
            try
            {
                Logger.Debug("Start Application");
                Logger.Debug("Environment.OSVersion = " + Environment.OSVersion);
                Logger.Debug("Environment.Is64BitOperatingSystem = " + Environment.Is64BitOperatingSystem);
                Logger.Debug("Environment.Is64BitProcess = " + Environment.Is64BitProcess);
                Logger.Debug("Environment.Version = " + Environment.Version);
                var mainform = AppCore.Init();
                Logger.Debug("Application core initialized.");

                if (mainform != null)
                {
                    Logger.Debug("MainForm is " + mainform.GetType().ToString());
                    Application.Run(mainform);

                }
                else
                {
                    Logger.Debug("MainForm is null");
                    Application.Run();
                }
            }
            catch (Exception e) { Logger.Error(e); }
        }
    }
}
