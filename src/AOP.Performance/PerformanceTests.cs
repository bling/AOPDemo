#define NOOP

using System;
using System.Diagnostics;
using Castle.DynamicProxy;
using NUnit.Framework;

namespace AOP.Test
{
    [TestFixture]
    public class PerformanceTests
    {
        private const int Iterations = 500000;

        private readonly ProxyGenerator _proxyGenerator = new ProxyGenerator();

        public PerformanceTests()
        {
            // preload to prevent skewed results from being run first
            new Model();
            new CBO();
        }

        [Test]
        public void RunCreationTests()
        {
            var time = Run("new", TimeSpan.Zero, () => new Model());
            Run("activator", time, () => Activator.CreateInstance<Model>());
            Run("castle", time, () => _proxyGenerator.CreateClassProxy<Model>());
            Run("castle.interface", time, () => _proxyGenerator.CreateInterfaceProxyWithTarget<IHello>(new Model()));
            Run("cob", time, () => new CBO());
        }

        [Test]
        public void RunInvocationTests()
        {
            var time = Run("new", TimeSpan.Zero, new Model());
            Run("activator", time, Activator.CreateInstance<Model>());
            Run("dynamicproxy", time, _proxyGenerator.CreateClassProxy<Model>());
            Run("dynamicproxy.interface", time, _proxyGenerator.CreateInterfaceProxyWithTarget<IHello>(new Model()));
            Run("cob", time, new CBO());
        }

        private static TimeSpan Run(string name, TimeSpan baseTime, IHello hello)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                hello.Hello();
            }
            var time = sw.Elapsed;
            Console.WriteLine(name + ": " + time + ", slowdown: " + time.TotalMilliseconds / baseTime.TotalMilliseconds);
            return time;
        }

        private static TimeSpan Run(string name, TimeSpan baseTime, Action action)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++)
            {
                action();
            }
            var time = sw.Elapsed;
            Console.WriteLine(name + ": " + time + ", slowdown: " + time.TotalMilliseconds / baseTime.TotalMilliseconds);
            return time;
        }
    }

    public class Model : IHello
    {
        public Model()
        {
#if !NOOP
            var s = Environment.MachineName;
#endif
        }

        public virtual void Hello()
        {
#if !NOOP
            var s = Environment.MachineName;
#endif
        }
    }

    [NoOp]
    public class CBO : ContextBoundObject, IHello
    {
        public CBO()
        {
#if !NOOP
            var s = Environment.MachineName;
#endif
        }

        public void Hello()
        {
#if !NOOP
            var s = Environment.MachineName;
#endif
        }
    }

    public interface IHello
    {
        void Hello();
    }
}