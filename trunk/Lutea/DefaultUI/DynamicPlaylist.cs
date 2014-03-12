using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

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
    class DynamicPlaylist
    {
        // 再帰的にTreeNodeに読み込む
        public static void Load(string appPath, TreeNode parent, ContextMenuStrip folderContextMenuStrip)
        {
            String[] subdirs = System.IO.Directory.GetDirectories(appPath);
            foreach (string dir in subdirs)
            {
                TreeNode dirtree = new TreeNode(System.IO.Path.GetFileNameWithoutExtension(dir));
                dirtree.ContextMenuStrip = folderContextMenuStrip;
                dirtree.Tag = new PlaylistEntryDirectory(dir);
                Load(dir, dirtree, folderContextMenuStrip);
                parent.Nodes.Add(dirtree);
            }

            String[] qFiles = System.IO.Directory.GetFiles(appPath, "*.q");
            foreach (string filename in qFiles)
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(filename, Encoding.Default);
                    string sql = lines[1].Replace("SQL=", "").Replace(@"\n", "\n");
                    int sortBy = int.Parse(lines[2].Replace("SortBy=", ""));
                    int sortOrder = int.Parse(lines[3].Replace("SortOrder=", ""));
                    if (lines.Length > 0)
                    {
                        string playlistname = System.IO.Path.GetFileNameWithoutExtension(filename);
                        TreeNode ent = new TreeNode(playlistname);
                        ent.Tag = new PlaylistEntryFile(appPath, playlistname, sql, sortBy, sortOrder);
                        ent.ImageIndex = 1;
                        ent.SelectedImageIndex = 1;
                        parent.Nodes.Add(ent);
                    }
                }
                catch { }
            }
        }
    }
}
