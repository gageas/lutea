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
        private Queue<Controller.VOIDVOID> taskQueue;
        private Stack<Controller.VOIDVOID> taskStack;
        private System.Collections.ICollection taskI;
        private Thread thisThread;
        private bool sleeping = false;
        private bool isLIFO;
        public ThreadPriority Priority
        {
            get { return thisThread.Priority; }
            set { thisThread.Priority = value; }
        }
        public WorkerThread(bool isLIFO = false)
        {
            this.isLIFO = isLIFO;
            if (isLIFO)
            {
                taskI = taskStack = new Stack<Controller.VOIDVOID>();
            }
            else
            {
                taskI = taskQueue = new Queue<Controller.VOIDVOID>();
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
                    Controller.VOIDVOID task = null;
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
        public void AddTask(Controller.VOIDVOID delg)
        {
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
    }
}
