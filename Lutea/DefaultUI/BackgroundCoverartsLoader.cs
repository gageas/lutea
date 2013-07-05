using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using Gageas.Lutea.Util;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.DefaultUI
{
    class BackgroundCoverartsLoader
    {
        public delegate void LoadComplete(IEnumerable<int> indexes);
        public event LoadComplete Complete = null;

        private readonly GDI.GDIBitmap dummyEmptyBitmapGDI = new GDI.GDIBitmap(new Bitmap(1, 1));
        private int size;
        private Thread thread = null;
        private Stack<TaskEntry> tasks = new Stack<TaskEntry>(); // LIFOで処理するのでStackを使う
        private Dictionary<string, GDI.GDIBitmap> coverArts = new Dictionary<string, GDI.GDIBitmap>();
        private bool isInSleep = false;

        public class TaskEntry
        {
            public TaskEntry(string key, string file_name, int callbackObjectId)
            {
                this.Key = key;
                this.file_name = file_name;
                this.callbackObjectIds = new List<int>() { callbackObjectId };
            }
            public string Key;
            public string file_name;
            public List<int> callbackObjectIds;
        }

        public BackgroundCoverartsLoader(int size)
        {
            this.size = size;
            thread = new Thread(threadProc);
            thread.Priority = ThreadPriority.Lowest;
            thread.Start();
        }

        public void Reset(int size)
        {
            this.size = size;
            ClearQueue();
            lock (coverArts)
            {
                foreach (var bmp in coverArts)
                {
                    if (bmp.Value != dummyEmptyBitmapGDI)
                    {
                        bmp.Value.Dispose();
                    }
                }
                coverArts.Clear();
            }
        }

        public void Interrupt()
        {
            if (isInSleep)
            {
                thread.Interrupt();
            }
        }

        public void Enqueue(string album, string file_name, int index)
        {
            lock (tasks)
            {
                var queued = tasks.FirstOrDefault((_) => _.Key == album);
                if (queued != null)
                {
                    queued.callbackObjectIds.Add(index);
                }
                else
                {
                    tasks.Push(new TaskEntry(album, file_name, index));
                }
            }
            Interrupt();
        }

        public int QueueCount
        {
            get
            {
                return tasks.Count;
            }
        }

        public void ClearQueue()
        {
            lock (tasks)
            {
                tasks.Clear();
            }
        }

        public bool IsCached(string album)
        {
            return coverArts.ContainsKey(album);
        }

        public GDI.GDIBitmap GetCache(string album)
        {
            return coverArts[album];
        }

        private void threadProc()
        {
            while (true)
            {
                try
                {
                    consumeAllTasks();
                    isInSleep = true;
                    Thread.Sleep(Timeout.Infinite);
                }
                catch (ThreadInterruptedException)
                {
                    isInSleep = false;
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }
            }
        }

        private void consumeAllTasks()
        {
            while (true)
            {
                TaskEntry task;
                lock (tasks)
                {
                    // キューが空になったら無限ループを抜ける
                    if (tasks.Count == 0) break;
                    task = tasks.Pop();
                }
                consumeTask(task);
            }
        }

        private void consumeTask(TaskEntry tasks)
        {
            try
            {
                if (size == 0) return;
                if (tasks.Key == null) return;

                var album = tasks.Key;
                if (coverArts.ContainsKey(album)) return;

                var orig = Controller.CoverArtImageForFile(tasks.file_name.Trim());
                if (orig != null)
                {
                    var resize = ImageUtil.GetResizedImageWithoutPadding(orig, size, size);
                    var w = resize.Width;
                    var h = resize.Height;
                    var bordered = new Bitmap(w + 3, h + 3);
                    using (var gg = Graphics.FromImage(bordered))
                    {
                        // ここでアルファ使うと描画が重くなる
                        gg.FillRectangle(Brushes.Silver, new Rectangle(3, 3, w, h));
                        gg.DrawImage(resize, 1, 1);
                        gg.DrawRectangle(Pens.Gray, new Rectangle(0, 0, w + 1, h + 1));
                    }
                    coverArts[album] = new GDI.GDIBitmap(bordered);
                }
                else
                {
                    coverArts[album] = dummyEmptyBitmapGDI;
                }
            }
            finally
            {
                if (Complete != null)
                {
                    Complete.Invoke(tasks.callbackObjectIds.Distinct());
                }
            }
        }
    }
}
