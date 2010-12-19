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
                var mainform = AppCore.Init();
                if (mainform != null)
                {
                    Application.Run(mainform);
                }
                else
                {
                    Application.Run();
                }
            }
            catch (Exception e) { Logger.Error(e); }
        }
    }
}
