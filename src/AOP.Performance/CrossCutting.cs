using System;
using System.Diagnostics;
using Castle.DynamicProxy;
using NUnit.Framework;

namespace AOP.Test
{
    public class CrossCutting
    {
        public virtual int Sum(int a, int b)
        {
            return a + b;
        }
    }

    [TestFixture]
    public class Runner
    {
        [Test]
        public void Run()
        {
            var gen = new ProxyGenerator();
            var obj = gen.CreateClassProxy<CrossCutting>(new ExceptionInterceptor(), new DebugInterceptor());
            Assert.That(obj.Sum(1, 1), Is.EqualTo(0));
        }
    }

    public class ExceptionInterceptor : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            try
            {
                invocation.Proceed();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    public class DebugInterceptor : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            Trace.WriteLine("Entering method " + invocation.Method);
            invocation.Proceed();
            Trace.WriteLine("Exiting method " + invocation.Method);
        }
    }
}