using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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
    ///
    /// A <c>ScopedMutex</c> is not reentrant and should not be created/locked recursively!
    /// </summary>
    public readonly struct ScopedMutex : IDisposable
    {
        sealed class CountedMutex : IDisposable
        {
            public SemaphoreSlim Mutex;
            public volatile int RefCount;

            public CountedMutex()
            {
                Mutex = new(0, 1);
                RefCount = 1;
            }

            public void Dispose()
            {
                if (Interlocked.Decrement(ref RefCount) == 0)
                {
                    Mutex.Dispose();
#if DEBUG
                    Debug.Assert(RefCount == 0, "Another thread obtained this mutex (incremented RefCount) after we set RefCount to zero!");
#endif
                }
            }
        }

        static readonly ConcurrentDictionary<string, CountedMutex> MutexMap = new();
        private readonly CountedMutex _mutex;
        private readonly string _name;

        private ScopedMutex(string name, CountedMutex mutex)
        {
            _name = name;
            _mutex = mutex;
        }

        private static ScopedMutex InnerCreate(string name, out bool owned)
        {
            CountedMutex mutex;

            bool newlyCreated = false;
            if (MutexMap.TryGetValue(name, out mutex))
            {
                newlyCreated = false;
                Interlocked.Increment(ref mutex.RefCount);
            }
            else
            {
                // We can't guarantee how many times the factory methods will be called under heavy contention,
                // so keep a reference to the newly created mutex around so we can dispose it in case it won't
                // be used (e.g. we created it because initially no matching mutex was found, but when we went
                // to insert it, someone had beat us to the punch - we end up using theirs but ours is still
                // alive).
                CountedMutex? newMutex = null;
                mutex = MutexMap.AddOrUpdate(name, _ =>
                {
                    // Initial create
                    newlyCreated = true;
                    newMutex ??= new CountedMutex();
                    return newMutex;
                }, (_, existing) =>
                {
                    // Race condition, another thread already created and inserted a new CountedMutex

                    // This is loop is required to prevent getting a ScopedMutex that is about to be disposed
                    // and removed from the map.
                    while (true)
                    {
                        var previousCount = existing.RefCount;
                        if (previousCount == 0)
                        {
                            // We obtained this after another thread both created *and* destroyed it.
                            newlyCreated = true;
                            newMutex ??= new CountedMutex();
                            return newMutex;
                        }
                        if (Interlocked.CompareExchange(ref existing.RefCount, previousCount + 1, previousCount) != previousCount)
                        {
                            continue;
                        }
                        break;
                    }

                    newlyCreated = false;
                    return existing;
                });

                if (!newlyCreated && newMutex is not null)
                {
                    // We created but didn't end up using a new mutex. Prevent a resource leak.
                    newMutex.Dispose();
                }
            }

            owned = newlyCreated;
            return new ScopedMutex(name, mutex);
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
            var mutex = InnerCreate(name, out var owned);
            if (!owned)
            {
                mutex.WaitOne();
            }
            return mutex;
        }

        /// <summary>
        /// Creates a new <c>ScopedMutex</c> with the name <paramref name="name"/>.
        /// This call will block if another instance of <c>ScopedMutex</c> exists in the same process
        /// with the same name, until it has been unlocked (disposed).
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static async Task<IDisposable> CreateAsync(string name)
        {
            var mutex = InnerCreate(name, out var owned);
            if (!owned)
            {
                await mutex.WaitOneAsync();
            }
            return mutex;
        }

        private void WaitOne()
        {
            _mutex.Mutex.Wait();
        }

        private Task WaitOneAsync()
        {
            return _mutex.Mutex.WaitAsync();
        }

        public void Dispose()
        {
            _mutex.Mutex.Release();
            _mutex.Dispose();

            if (_mutex.RefCount == 0)
            {
                if (!MutexMap.TryRemove(_name, out _))
                {
                    Debug.Assert(false, "Multiple threads disposed CountedMutex and tried to remove from MutexMap!");
                }
            }
        }
    }
}
