using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeoSmart.Web
{
    public readonly struct ScopedMutex : IDisposable
    {
        class CountedMutex
        {
            public SemaphoreSlim Mutex;
            public int RefCount;
        }

        static Dictionary<string, CountedMutex> MutexMap = new Dictionary<string, CountedMutex>();
        private readonly CountedMutex _mutex;
        private readonly string _name;

        private ScopedMutex(string name, CountedMutex mutex)
        {
            _name = name;
            _mutex = mutex;
        }

        public static IDisposable Create(string name)
        {
            bool owned;
            CountedMutex mutex;

            lock (MutexMap)
            {
                if (MutexMap.TryGetValue(name, out mutex))
                {
                    owned = false;
                    mutex.RefCount++;
                }
                else
                {
                    owned = true;
                    mutex = new CountedMutex()
                    {
                        RefCount = 1,
                        Mutex = new SemaphoreSlim(0, 1),
                    };
                    MutexMap.Add(name, mutex);
                }
            }

            var result = new ScopedMutex(name, mutex);
            if (!owned)
            {
                result.WaitOne();
            }
            return result;
        }

        public static async Task<IDisposable> CreateAsync(string name)
        {
            bool owned;
            CountedMutex mutex;

            lock (MutexMap)
            {
                if (MutexMap.TryGetValue(name, out mutex))
                {
                    owned = false;
                    mutex.RefCount++;
                }
                else
                {
                    owned = true;
                    mutex = new CountedMutex()
                    {
                        RefCount = 1,
                        Mutex = new SemaphoreSlim(0, 1),
                    };
                    MutexMap.Add(name, mutex);
                }
            }

            var result = new ScopedMutex(name, mutex);
            if (!owned)
            {
                await result.WaitOneAsync();
            }
            return result;
        }

        private void WaitOne()
        {
            _mutex.Mutex.Wait();
        }

        private Task WaitOneAsync()
        {
            return _mutex.Mutex.WaitAsync();
        }

        private void Release()
        {
            _mutex.Mutex.Release();
        }

        public void Dispose()
        {
            Release();
            lock (MutexMap)
            {
                if (--_mutex.RefCount == 0)
                {
                    _mutex.Mutex.Dispose();
                    if (MutexMap.ContainsKey(_name))
                    {
                        MutexMap.Remove(_name);
                    }
                    else
                    {
                        System.Diagnostics.Debugger.Break();
                    }
                }
            }
        }
    }
}
