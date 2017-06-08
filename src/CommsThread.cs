using SocketFactory.Environment;
using System;
using System.Threading;

namespace SocketFactory {

    public class CommsThread
    {
        public delegate void OnExceptionLog(string message);

        private Thread _thread;
        private object _runningLock = new object();
        private bool _isRunning = true;
        private bool IsRunning
        {
            set
            {
                lock (_runningLock)
                {
                    _isRunning = value;
                }
            }
            get
            {
                lock (_runningLock)
                {
                    return _isRunning;
                }
            }
        }
        private OnExceptionLog _onExceptionLog;
        private ThreadStart _process;
        private long _milliSleep;
        private DateTimeEnvironment _dtProcess;

        public CommsThread(ThreadStart process, 
                           OnExceptionLog onExceptionLog, 
                           long milliSleep)
        {
            _process = process ?? throw new ArgumentNullException("process cannot be null.");
            _onExceptionLog = onExceptionLog;
            if (milliSleep < 20)
            {
                milliSleep = 20;
            }
            _milliSleep = milliSleep;
            _dtProcess = DateTimeEnvironment.Now;
            _thread = new Thread(new ThreadStart(Run));
            _thread.Start();
        }

        private void Run()
        {
            while (IsRunning)
            {
                try
                {
                    TimeSpanEnvironment ts = DateTimeEnvironment.Now.Subtract(_dtProcess);
                    if (ts.TotalMilliseconds > _milliSleep)
                    {
                        _dtProcess = DateTimeEnvironment.Now;
                        _process();
                    }
                }
                catch (Exception ex)
                {
                    _onExceptionLog?.Invoke("CommsThread.Run(): " + ex.Message + "," + ex.StackTrace);
                }
                finally
                {
                    Thread.Sleep(20);
                }
            }
        }

        public void Stop()
        {
            if (_thread == null) return;
            IsRunning = false;
            _thread.Join();
            while (_thread.IsAlive) ;
            _thread = null;
        }
    }
}