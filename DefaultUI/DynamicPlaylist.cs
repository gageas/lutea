using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gageas.Lutea.DefaultUI
{
    class PlaylistEntry
    {
        
        public PlaylistEntry(string name, string sql)
        {
            this.name = name;
            this.sql = sql;
        }
        public string sql;
        public string name;
    }
    class DynamicPlaylist
    {
        // 再帰的にTreeNodeに読み込む
        public static void Load(string appPath, TreeNode parent, ContextMenuStrip folderContextMenuStrip)
        {
//            List<PlaylistEntry> list = new List<PlaylistEntry>();
            char sep = System.IO.Path.DirectorySeparatorChar;

            String[] subdirs = System.IO.Directory.GetDirectories(appPath);
            foreach (string dir in subdirs)
            {
                TreeNode dirtree = new TreeNode(System.IO.Path.GetFileNameWithoutExtension(dir));
                dirtree.ContextMenuStrip = folderContextMenuStrip;
                Load(dir, dirtree, folderContextMenuStrip);
                parent.Nodes.Add(dirtree);
            }

            String[] qFiles = System.IO.Directory.GetFiles(appPath, "*.q");
            foreach (string filename in qFiles)
            {
                string[] lines = System.IO.File.ReadAllLines(filename, Encoding.Default);
                string sql = lines[1].Replace("SQL=", "");
                Logger.Debug(sql);
                if (lines.Length > 0)
                {
                    string playlistname = System.IO.Path.GetFileNameWithoutExtension(filename);
                    TreeNode ent = new TreeNode(playlistname);
                    ent.Tag = new PlaylistEntry(playlistname,sql);
                    parent.Nodes.Add(ent);
                }
            }
        }
    }
}
