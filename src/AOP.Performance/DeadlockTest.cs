using System.Collections.Generic;
using System.Threading;
using Castle.DynamicProxy;
using NUnit.Framework;

namespace AOP.Test
{
    [TestFixture]
    public class DeadlockTest
    {
        private readonly ProxyGenerator _proxyGenerator = new ProxyGenerator();

        [Test]
        public void Event_Triggered_After_Time_Elapsed()
        {
            Thread a = null, b = null;
            var instance = Create();
            instance.PotentialDeadlock += (thread1, thread2) =>
            {
                a = thread1;
                b = thread2;
            };

            new Thread(() => instance.Enter()).Start(); // since enter isn't disposed, this keeps the lock
            Thread.Sleep(100);

            new Thread(() => instance.Enter()).Start();
            Thread.Sleep(1000);

            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreNotEqual(a.ManagedThreadId, b.ManagedThreadId);
        }

        private ILockableDictionary<int, string> Create()
        {
            var options = new ProxyGenerationOptions();
            options.AddMixinInstance(new DeadLockDetector());

            return (ILockableDictionary<int, string>)_proxyGenerator.CreateInterfaceProxyWithTarget(
                typeof(IDictionary<int, string>), // the normal interface
                new[] { typeof(ILockableDictionary<int, string>) }, // an array of additional interfaces
                new Dictionary<int, string>(), // concrete instance
                options); // options, which contains the mixin
        }
    }

    public interface ILockableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, ILocker
    {
    }
}