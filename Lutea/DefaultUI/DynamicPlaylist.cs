using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.DefaultUI
{
    public abstract class PlaylistEntry
    {
        public abstract void Delete();
        public abstract void Rename(string newName);
        public abstract string Name { get; }
        public abstract string Path { get; }
        protected string replaceInvalidChar(string src)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                src = src.Replace(c, '_');
            }
            return src;
        }
    }
    public class VirtualPlaylistEntry : PlaylistEntry
    {
        public VirtualPlaylistEntry(string sql, int sortBy, int sortOrder)
        {
            this.sql = sql;
            this.sortBy = sortBy;
            this.sortOrder = sortOrder;
        }
        public string sql;
        public int sortBy = 0;
        public int sortOrder = 0;
        public override string Path
        {
            get { return ""; }
        }
        public override void Rename(string newName)
        {
        }
        public override string Name
        {
            get { return ""; }
        }
        public override void Delete()
        {
        }
    }
    public class PlaylistEntryDirectory : PlaylistEntry
    {
        private string path;
        public PlaylistEntryDirectory(string path)
        {
            this.path = path;
        }
        public override string Path
        {
            get { return this.path; }
        }
        public override void Delete()
        {
            System.IO.Directory.Delete(path, true);
        }
        public override void Rename(string newName)
        {
            System.IO.Directory.Move(path, System.IO.Path.GetDirectoryName(path) + System.IO.Path.DirectorySeparatorChar + replaceInvalidChar(newName));
        }
        public override string Name
        {
            get { return System.IO.Path.GetFileName(path); }
        }
    }
    public class PlaylistEntryFile : PlaylistEntry
    {
        public PlaylistEntryFile(string directory, string name, string sql, int sortBy, int sortOrder)
        {
            this.directory = directory;
            this.name = replaceInvalidChar(name);
            this.sql = sql;
            this.sortBy = sortBy;
            this.sortOrder = sortOrder;
        }
        public string directory;
        public string sql;
        public int sortBy = 0;
        public int sortOrder = 0;
        private string name;
        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[Query]");
            sb.AppendLine("SQL=" + sql);
            sb.AppendLine("SortBy=" + sortBy);
            sb.AppendLine("SortOrder=" + sortOrder);
            try
            {
                System.IO.File.WriteAllText(Path, sb.ToString(), Encoding.Default);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
        public override void Delete()
        {
            if (System.IO.File.Exists(Path))
            {
                System.IO.File.Delete(Path);
            }
        }
        public override void Rename(string newName)
        {
            System.IO.File.Move(Path, directory + System.IO.Path.DirectorySeparatorChar + replaceInvalidChar(newName) + ".q");
        }
        public override string Name
        {
            get { return this.name; }
        }
        public override string Path
        {
            get
            {
                return this.directory + System.IO.Path.DirectorySeparatorChar + this.name + ".q";
            }
        }
    }
    class DynamicPlaylist<T>
    {
        public delegate T AddFolderNode(T parent, string path);
        public delegate void AddQueryNode(T parent, string path, string name, string sql, int sortBy, int sortOrder);

        public static void Load(string appPath, T parent, AddFolderNode addFolderNode, AddQueryNode addQueryNode)
        {
            String[] subdirs = System.IO.Directory.GetDirectories(appPath);
            foreach (string dir in subdirs)
            {
                var dirtree = addFolderNode(parent, dir);
                Load(dir, dirtree, addFolderNode, addQueryNode);
            }

            String[] qFiles = System.IO.Directory.GetFiles(appPath, "*.q");
            foreach (string filename in qFiles)
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(filename, Encoding.Default);
                    if (lines.Length > 0)
                    {
                        string sql = lines[1].Replace("SQL=", "").Replace(@"\n", "\n");
                        int sortBy = int.Parse(lines[2].Replace("SortBy=", ""));
                        int sortOrder = int.Parse(lines[3].Replace("SortOrder=", ""));
                        addQueryNode(parent, appPath, filename, sql, sortBy, sortOrder);
                    }
                }
                catch { }
            }
        }
    }
}
