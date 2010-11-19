using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace Gageas.Lutea.Library
{
    class UserDirectory
    {
        private static char sep = System.IO.Path.DirectorySeparatorChar;
        public readonly string ApplicationDir = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
        public string ComponentDir
        {
            get
            {
                return ApplicationDir + sep + "components";
            }
        }
        public string PluginDir
        {
            get
            {
                return ApplicationDir + sep + "plugin";
            }
        }
        private string userName;
        public string UserDir
        {
            get
            {
                return ApplicationDir + sep + "users" + sep + userName;
            }
        }

        public string LibraryDBPath
        {
            get
            {
                return UserDir + sep + "Library.db";
            }
        }

/*        public string QueryDir
        {
            get
            {
                return UserDir + sep + "query";
            }
        }*/

        public UserDirectory(string username)
        {
            userName = username;
            if (!Directory.Exists(ApplicationDir + sep + "users"))
            {
                Directory.CreateDirectory(ApplicationDir + sep + "users");
            }

            if (!Directory.Exists(UserDir))
            {
                Directory.CreateDirectory(UserDir);
            }

            if (!Directory.Exists(UserDir))
            {
                Directory.CreateDirectory(UserDir);
            }

/*            if (!Directory.Exists(QueryDir))
            {
                Directory.CreateDirectory(QueryDir);
                // TODO: デフォルトのqファイルを書きだし
            }*/
        }

        public H2k6Library OpenLibrary(ICollection<string> customColumns){
            bool isNew = !File.Exists(LibraryDBPath);
            var lib = new H2k6Library(customColumns, LibraryDBPath);
            if (isNew)
            {
                try
                {
                    using (var db = lib.Connect(true))
                    {
                        db.Exec(Library.DefaultSchema);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e.ToString());
                }
            }
            return lib;
        }

        public UserDirectory():this(System.Environment.GetEnvironmentVariable("USERNAME"))
        {
        }
    }
}
