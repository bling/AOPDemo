using System;
using System.Runtime.Remoting.Activation;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Remoting.Messaging;

namespace AOP.Test
{
    /// <summary>
    /// An attribute for ContextBoundObjects that declares the bare minimum to enable
    /// interception without actually doing anything before or after the invocation.
    /// </summary>
    public class NoOpAttribute : ContextAttribute
    {
        public NoOpAttribute()
            : base(typeof(NoOpAttribute).FullName)
        {
        }

        public override void GetPropertiesForNewContext(IConstructionCallMessage ctorMsg)
        {
            ctorMsg.ContextProperties.Add(new NoOpContextProperty());
        }

        private class NoOpContextProperty : IContextProperty, IContributeObjectSink
        {
            public bool IsNewContextOK(Context newCtx)
            {
                return true;
            }

            public void Freeze(Context newContext)
            {
            }

            public string Name
            {
                get { return GetType().FullName; }
            }

            public IMessageSink GetObjectSink(MarshalByRefObject obj, IMessageSink nextSink)
            {
                return new LogMethodMessageSink(nextSink);
            }
        }

        private class LogMethodMessageSink : IMessageSink
        {
            private readonly IMessageSink _nextSink;

            public LogMethodMessageSink(IMessageSink nextSink)
            {
                _nextSink = nextSink;
            }

            public IMessage SyncProcessMessage(IMessage msg)
            {
                return _nextSink.SyncProcessMessage(msg);
            }

            public IMessageCtrl AsyncProcessMessage(IMessage msg, IMessageSink replySink)
            {
                return _nextSink.AsyncProcessMessage(msg, replySink);
            }

            public IMessageSink NextSink
            {
                get { return _nextSink; }
            }
        }
    }
}