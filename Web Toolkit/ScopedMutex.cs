using System;
using System.Threading;

namespace NeoSmart.Web
{
	public class ScopedMutex : IDisposable
	{
		private readonly Mutex _mutex;
		private bool _locked;
		public bool SafeWait { get; set; }

		public ScopedMutex(string name, bool initiallyOwned = true)
		{
            _mutex = new Mutex(false, name); // false here to avoid possible AbandonedMutexException
            _locked = false;
            SafeWait = true;

            if (initiallyOwned)
			{
				WaitOne();
			}
		}

		public bool WaitOne()
		{
			try
			{
				_locked = true; //Regardless of AbandonedMutexException
				_mutex.WaitOne();
			}
			catch (AbandonedMutexException)
			{
				if (!SafeWait)
				{
					throw;
				}
			}

			return true;
		}

		public void ReleaseMutex()
		{
            if (_locked)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch
                {
                    if (!SafeWait)
                    {
                        throw;
                    }
                }
            }
			_locked = false;
		}

		public void Dispose()
		{
			if (_locked)
			{
				ReleaseMutex();
			}
			_mutex.Dispose();
		}
	}
}
