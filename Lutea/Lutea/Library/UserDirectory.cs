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
                        db.Exec(GetCreateSchema());
                        db.Exec(GetCreateIndexSchema());
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e.ToString());
                }
            }
            return lib;
        }

        public static String GetCreateSchema()
        {
            return "CREATE TABLE IF NOT EXISTS list(" + String.Join(" , ", Lutea.Core.Controller.Columns.Select(_ =>
            {
                switch (_.type)
                {
                    case LibraryColumnType.FileName:
                        return _.Name + " TEXT UNIQUE";
                    case LibraryColumnType.Integer:
                    case LibraryColumnType.Bitrate:
                    case LibraryColumnType.Rating:
                    case LibraryColumnType.Time:
                    case LibraryColumnType.FileSize:
                    case LibraryColumnType.Timestamp64:
                        return _.Name + " INTEGER DEFAULT 0";
                    case LibraryColumnType.Text:
                    case LibraryColumnType.TrackNumber:
                    default:
                        return _.Name + " TEXT";
                }
            }).ToArray()) +
            " , PRIMARY KEY(" + String.Join(",", Lutea.Core.Controller.Columns.Where(_ => _.PrimaryKey).Select(_ => _.Name).ToArray()) + "));";
        }

        public static String GetCreateIndexSchema()
        {
            return String.Join(" ", Lutea.Core.Controller.Columns.Where(_ => _.Name == LibraryDBColumnTextMinimum.rating || _.IsTextSearchTarget).Select(_ => "CREATE INDEX " + _.Name + "_index ON list(" + _.Name + ");").ToArray());
        }

        public UserDirectory():this(System.Environment.GetEnvironmentVariable("USERNAME"))
        {
        }
    }
}
