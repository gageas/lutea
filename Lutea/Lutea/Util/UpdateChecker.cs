using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Net;
using System.Web;
using System.Security.Cryptography;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.Util
{
    /// <summary>
    /// 更新チェッカ（仮）
    /// </summary>
    public static class UpdateChecker
    {
        public class UpdateInfo
        {
            public double LuteaVersion;
            public DateTime ReleaseDate;
        }

        private class Versino1
        {
            private const int SIGN_LEN = 64;
            private static readonly byte[] pub = new byte[]{
	            0x45, 0x43, 0x53, 0x31, 0x20, 0x00, 0x00, 0x00, 
	            0xF8, 0xF8, 0x78, 0xC5, 0x97, 0x84, 0x30, 0xCC, 
	            0xF7, 0x53, 0xA8, 0xFB, 0x4E, 0xC2, 0x60, 0x83, 
	            0xE1, 0x68, 0x5C, 0x2E, 0x90, 0x0B, 0x95, 0x04, 
	            0xC8, 0x0F, 0xD6, 0xE0, 0xB2, 0x3A, 0xE3, 0x53, 
	            0x65, 0x61, 0x78, 0x24, 0xA2, 0x2F, 0xEE, 0x29, 
	            0x75, 0x4E, 0x6F, 0x6A, 0x2C, 0xFB, 0x6B, 0x60, 
	            0xC6, 0xD1, 0x58, 0x50, 0x28, 0x2B, 0x90, 0xDF, 
	            0xBA, 0xD5, 0x17, 0xAE, 0xB5, 0xF1, 0x3F, 0x20
            };

            public static UpdateInfo Verify(byte[] data)
            {
                var payload = data.Skip(1).Take(data.Length - 1 - SIGN_LEN).ToArray();
                var sign = data.Skip(1 + payload.Length).ToArray();
                var ver = new ECDsaCng(CngKey.Import(pub, CngKeyBlobFormat.EccPublicBlob));
                if (!ver.VerifyData(payload, sign))
                {
                    Logger.Log("署名が不正でした");
                    return null;
                }
                var infoText = Encoding.UTF8.GetString(payload).Split(',');
                UpdateInfo info = new UpdateInfo();
                info.LuteaVersion = double.Parse(infoText[0]);
                info.ReleaseDate = DateTime.Parse(infoText[1]);
                return info;
            }
        }

        public static UpdateInfo CheckNewVersion()
        {
            try
            {
                LuteaComponentInfo currentVerInfo = (LuteaComponentInfo)typeof(CoreComponent).GetCustomAttributes(typeof(LuteaComponentInfo), false).First();
                var uri = new UriBuilder("http", "lutea.gageas.com", 80, "/files/lutea.update", "?via=UpdateChecker&lutea=" + currentVerInfo.version).Uri;
                using (var client = new WebClient())
                {
                    client.Proxy = WebRequest.GetSystemWebProxy();
                    var updateInfoBytes = client.DownloadData(uri);
                    UpdateInfo updateInfo = null;
                    switch (updateInfoBytes[0])
                    {
                        case 1:
                            updateInfo = Versino1.Verify(updateInfoBytes);
                            break;
                    }
                    if (updateInfo == null) return null;
                    if (updateInfo.LuteaVersion > currentVerInfo.version)
                    {
                        return updateInfo;
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
