using System.Threading;
using NUnit.Framework;
using Yxy.Core;

namespace Yxy.Core.Tests
{
    [TestFixture]
    public sealed class WorkNiceTests
    {
        [Test]
        public void Enter_LowersThenRestoresPriority()
        {
            var before = Thread.CurrentThread.Priority;
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.Normal;
                using (WorkNice.Enter())
                {
                    Assert.That(Thread.CurrentThread.Priority, Is.EqualTo(ThreadPriority.BelowNormal));
                }

                Assert.That(Thread.CurrentThread.Priority, Is.EqualTo(ThreadPriority.Normal));
            }
            finally
            {
                Thread.CurrentThread.Priority = before;
            }
        }
    }
}
