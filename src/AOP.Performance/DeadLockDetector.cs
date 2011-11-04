using System;
using System.Threading;

namespace AOP.Test
{
    public interface ILocker
    {
        event Action<Thread, Thread> PotentialDeadlock;
        IDisposable Enter();
    }

    public class DeadLockDetector : ILocker
    {
        public event Action<Thread, Thread> PotentialDeadlock = delegate { };

        private Thread _owner;

        public int DeadLockThreshold { get; set; }

        public int TryTimeout { get; set; }

        public DeadLockDetector()
        {
            TryTimeout = 50;
            DeadLockThreshold = 500;
        }

        public IDisposable Enter()
        {
            var start = DateTime.UtcNow;
            while (!Monitor.TryEnter(this, TryTimeout))
            {
                if (DateTime.UtcNow - start > TimeSpan.FromMilliseconds(DeadLockThreshold))
                {
                    PotentialDeadlock(Thread.CurrentThread, _owner);
                }
            }

            _owner = Thread.CurrentThread;
            return new Unlocker(this);
        }

        private class Unlocker : IDisposable
        {
            private readonly DeadLockDetector _locker;

            public Unlocker(DeadLockDetector locker)
            {
                _locker = locker;
            }

            public void Dispose()
            {
                _locker.Unlock();
            }
        }

        private void Unlock()
        {
            _owner = null;
            Monitor.Exit(this);
        }
    }
}