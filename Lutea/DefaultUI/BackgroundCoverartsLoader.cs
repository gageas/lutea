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
        private List<Thread> threads = new List<Thread>();
        private List<TaskEntry> tasks = new List<TaskEntry>();
        private Dictionary<string, GDI.GDIBitmap> coverArts = new Dictionary<string, GDI.GDIBitmap>();
        private List<Thread> wakeMeUp = new List<Thread>();

        public class TaskEntry
        {
            public TaskEntry(string key, string file_name, int callbackObjectId)
            {
                this.Key = key;
                this.file_name = file_name;
                this.callbackObjectIds = new List<int>() { callbackObjectId };
            }
            public bool Consuming = false;
            public string Key;
            public string file_name;
            public List<int> callbackObjectIds;
        }

        public BackgroundCoverartsLoader(int size)
        {
            this.size = size;
            for (int i = 0; i < 2; i++)
            {
                var thread = new Thread(threadProc);
                thread.Priority = ThreadPriority.Lowest;
                thread.Start();
                threads.Add(thread);
            }
        }

        public void Reset(int size)
        {
            this.size = size;
            ClearQueue();
            var oldCoverArts = coverArts;
            coverArts = new Dictionary<string, GDI.GDIBitmap>();
            foreach (var bmp in oldCoverArts)
            {
                if (bmp.Value == null) continue;
                if (bmp.Value != dummyEmptyBitmapGDI)
                {
                    bmp.Value.Dispose();
                }
            }
            oldCoverArts.Clear();
        }

        public void Interrupt()
        {
            lock (wakeMeUp)
            {
                foreach (var th in wakeMeUp)
                {
                    th.Interrupt();
                }
                wakeMeUp.Clear();
            }
        }

        public void Enqueue(string album, string file_name, int index)
        {
            lock (tasks)
            {
                if (IsCached(album)) return;
                var queued = tasks.FirstOrDefault((_) => _.Key == album);
                if (queued != null)
                {
                    queued.callbackObjectIds.Add(index);
                }
                else
                {
                    tasks.Add(new TaskEntry(album, file_name, index));
                    Interrupt();
                }
            }
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
                    lock (wakeMeUp)
                    {
                        wakeMeUp.Add(Thread.CurrentThread);
                    }
                    Thread.Sleep(Timeout.Infinite);
                }
                catch (ThreadInterruptedException)
                {
                    // nothing
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }
            }
        }

        private void consumeAllTasks()
        {
            bool evenOdd = false;
            while (true)
            {
                TaskEntry task;
                lock (tasks)
                {
                    // キューが空になったら無限ループを抜ける
                    if (tasks.Count(_ => !_.Consuming) == 0) break;
                    task = evenOdd ? tasks.First(_ => !_.Consuming) : tasks.Last(_ => !_.Consuming);
                    task.Consuming = true;
                    evenOdd = !evenOdd;
                }
                var resizedBitmap = consumeTask(task);
                if (resizedBitmap != null)
                {
                    if (tasks.Contains(task))
                    {
                        coverArts[task.Key] = resizedBitmap;
                        if (Complete != null)
                        {
                            Complete.Invoke(task.callbackObjectIds.Distinct());
                        }
                    }
                    else
                    {
                        if (resizedBitmap != dummyEmptyBitmapGDI)
                        {
                            resizedBitmap.Dispose();
                        }
                    }
                }
                lock (tasks)
                {
                    tasks.Remove(task);
                }
            }
        }

        private GDI.GDIBitmap consumeTask(TaskEntry task)
        {
            try
            {
                if (size == 0) return null;
                if (task.Key == null) return null;

                var album = task.Key;
                if (coverArts.ContainsKey(album)) return null;

                var orig = Controller.CoverArtImageForFile(task.file_name.Trim());
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
                    return new GDI.GDIBitmap(bordered);
                }
                else
                {
                    return dummyEmptyBitmapGDI;
                }

            }
            catch (Exception) { return null; }
        }
    }
}
