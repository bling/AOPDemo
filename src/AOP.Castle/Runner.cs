using System;
using Castle.DynamicProxy;
using NUnit.Framework;

namespace AOP.Castle
{
    [TestFixture]
    public class Runner
    {
        [Test]
        public void Mixin()
        {
            var gen = new ProxyGenerator();
            var opts = new ProxyGenerationOptions();
            opts.AddMixinInstance(new HelloWorld());
            opts.AddMixinInstance(new HelloWorld2());
            var poco = (IUser)gen.CreateClassProxy(typeof(Poco), new[] { typeof(IUser) }, opts);
            poco.Hello();
            poco.Hello2();
            poco.Hi();
        }
    }

    public interface IUser : IHelloWorld, IHelloWorld2, IPoco
    {
    }

    public interface IHelloWorld
    {
        void Hello();
    }

    public class HelloWorld : IHelloWorld
    {
        public void Hello()
        {
            Console.WriteLine("Hello World!");
        }
    }

    public interface IHelloWorld2
    {
        void Hello2();
    }

    public class HelloWorld2 : IHelloWorld2
    {
        public void Hello2()
        {
            Console.WriteLine("Hello World 2!");
        }
    }

    public interface IPoco
    {
        void Hi();
    }

    public class Poco : IPoco
    {
        public void Hi()
        {
            Console.WriteLine("Hi");
        }
    }
}