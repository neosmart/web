using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeoSmart.Web
{
    /// <summary>
    /// An async-awaitable named mutex, implementing <c>IDisposable</c> for RAII semantics.
    /// Instantiation obtains a unique mutex keyed by the given name. Since the mutex is atomically
    /// locked/obtained when a <c>ScopedMutex</c> is created, there is no public constructor exposed
    /// to prevent blocking the async loop. Instead, use <see cref="Create(string)"/> or <see cref="CreateAsync(string)"/>
    /// to instantiate. <c>ScopedMutex</c> instances cannot be manually locked or unlocked, the
    /// underlying semaphore is locked so long as the <c>ScopedMutex</c> has not been disposed.
    /// </summary>
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

        /// <summary>
        /// Creates a new <c>ScopedMutex</c> with the name <paramref name="name"/>.
        /// This call will block if another instance of <c>ScopedMutex</c> exists in the same process
        /// with the same name, until it has been unlocked (disposed).
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
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
#if DEBUG
                        System.Diagnostics.Debug.Assert(false);
#endif
                    }
                }
            }
        }
    }
}
