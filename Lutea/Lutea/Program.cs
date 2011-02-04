using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Gageas.Lutea.Core
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
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
