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
        }

        public MusicLibrary OpenLibrary(){
            var lib = new MusicLibrary(LibraryDBPath);
            return lib;
        }

        public UserDirectory():this(System.Environment.GetEnvironmentVariable("USERNAME"))
        {
        }
    }
}
