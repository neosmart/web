using System;
using System.Threading;

namespace NeoSmart.Web
{
	public class ScopedMutex : IDisposable
	{
		private readonly EventWaitHandle _mutex;
		private bool _locked;
        private bool _disposed = false;
		public bool SafeWait { get; set; }

		public ScopedMutex(string name)
		{
            //_mutex = new Semaphore(1, 1, name); // false here to avoid possible AbandonedMutexException
            _mutex = new EventWaitHandle(true, EventResetMode.AutoReset, name);
            _locked = true;
            SafeWait = true;
		}

		public bool WaitOne()
		{
            _mutex.WaitOne();

			return true;
		}

		public void ReleaseMutex()
		{
                _mutex.Set();
                _locked = false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _mutex.Set();
                _disposed = true;
                _mutex.Dispose();
            }
		}
	}
}
