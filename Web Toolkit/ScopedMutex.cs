using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeoSmart.Web
{
    public struct ScopedMutex : IDisposable
    {
        class CountedMutex
        {
            public SemaphoreSlim Mutex;
            public int Count;
        }

        static Dictionary<string, CountedMutex> MutexMap = new Dictionary<string, CountedMutex>();
        private readonly CountedMutex _mutex;
        private readonly string _name;
        private bool _owned;

        private ScopedMutex(string name, CountedMutex mutex, bool owned)
        {
            _name = name;
            _mutex = mutex;
            _owned = owned;
        }

        public static async Task<ScopedMutex> CreateAsync(string name)
        {
            bool owned = false;
            CountedMutex mutex = null;
            lock (MutexMap)
            {
                if (MutexMap.TryGetValue(name, out mutex))
                {
                    mutex.Count++;
                }
                else
                {
                    mutex = new CountedMutex()
                    {
                        Count = 1,
                        Mutex = new SemaphoreSlim(0, 1),
                    };
                    MutexMap.Add(name, mutex);
                    owned = true;
                }
            }

            var result = new ScopedMutex(name, mutex, owned);
            if (!owned)
            {
                await result.WaitOne();
            }
            return result;
        }

        public async Task WaitOne()
        {
            if (!_owned)
            {
                await _mutex.Mutex.WaitAsync();
                _owned = true;
            }
        }

        public void ReleaseMutex()
        {
            if (_owned)
            {
                _mutex.Mutex.Release();
                _owned = false;
            }
        }

        public void Dispose()
        {
            ReleaseMutex();
            lock (MutexMap)
            {
                if (--_mutex.Count == 0)
                {
                    _mutex.Mutex.Dispose();
                    MutexMap.Remove(_name);
                }
            }
        }
    }
}
