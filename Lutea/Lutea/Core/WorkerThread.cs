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
    public class WorkerThread
    {
        private Queue<Action> taskQueue;
        private Stack<Action> taskStack;
        private System.Collections.ICollection taskI;
        private Thread thisThread;
        private bool sleeping = false;
        private bool isLIFO;
        private bool requestTerminate = false;
        public ThreadPriority Priority
        {
            get { return thisThread.Priority; }
            set { thisThread.Priority = value; }
        }
        public bool Terminated
        {
            get;
            private set;
        }
        public WorkerThread(bool isLIFO = false)
        {
            this.isLIFO = isLIFO;
            if (isLIFO)
            {
                taskI = taskStack = new Stack<Action>();
            }
            else
            {
                taskI = taskQueue = new Queue<Action>();
            }
            thisThread = new Thread(workerProc);
            thisThread.Start();
        }
        private void workerProc()
        {
            while (true)
            {
                try
                {
                    Action task = null;
                    while (taskI.Count > 0)
                    {
                        lock (thisThread)
                        {
                            if (taskI.Count > 0)
                            {
                                task = isLIFO ? taskStack.Pop() : taskQueue.Dequeue();
                            }
                        }
                        if (task != null)
                        {
                            try
                            {
                                task();
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
                if (taskI.Count == 0 && requestTerminate) return;
            }
        }
        public void AddTask(Action delg)
        {
            if (Terminated) throw new ObjectDisposedException("Thread is terminated");
            lock (thisThread)
            {
                if(isLIFO){
                    taskStack.Push(delg);
                }else{
                    taskQueue.Enqueue(delg);
                }
                if (sleeping) thisThread.Interrupt();
            }
        }
        public void WaitDoneAllTask(int millisecondsTimeout = 0)
        {
            requestTerminate = true;
            if (sleeping) thisThread.Interrupt();
            thisThread.Join(millisecondsTimeout);
        }
        public void WaitDoneCurrentTaskAndCancelPending(int millisecondsTimeout = 0)
        {
            lock (thisThread)
            {
                if (taskQueue != null) taskQueue.Clear();
                if (taskStack != null) taskStack.Clear();
            }
            WaitDoneAllTask(millisecondsTimeout);
        }
    }
}
