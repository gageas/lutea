using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Gageas.Lutea.Core
{
    /// <summary>
    /// タスクのキューを特定のスレッドで処理するクラス
    /// </summary>
    class WorkerThread
    {
        private Queue<Controller.VOIDVOID> taskQueue = new Queue<Controller.VOIDVOID>();
        private Thread thisThread;
        private bool sleeping = false;
        public WorkerThread()
        {
            thisThread = new Thread(workerProc);
            thisThread.Start();
        }
        private void workerProc()
        {
            while (true)
            {
                try
                {
                    Controller.VOIDVOID task = null;
                    while (taskQueue.Count > 0)
                    {
                        lock (thisThread)
                        {
                            if (taskQueue.Count > 0)
                            {
                                task = taskQueue.Dequeue();
                            }
                        }
                        if (task != null)
                        {
                            try
                            {
                                task.DynamicInvoke(null);
                            }
                            catch { }
                        }
                    }
                    sleeping = true;
                    Thread.Sleep(Timeout.Infinite);
                }
                catch (ThreadInterruptedException)
                {
                    sleeping = false;
                }
            }
        }
        public void CoreEnqueue(Controller.VOIDVOID delg)
        {
            lock (thisThread)
            {
                taskQueue.Enqueue(delg);
                if (sleeping) thisThread.Interrupt();
            }
        }
    }
}
