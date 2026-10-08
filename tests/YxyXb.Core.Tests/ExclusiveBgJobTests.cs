using System.Threading;
using NUnit.Framework;
using YxyXb.Core;

namespace YxyXb.Core.Tests
{
    [TestFixture]
    public sealed class ExclusiveBgJobTests
    {
        [Test]
        public void RejectsOverlapAndRunsOffCaller()
        {
            var job = new ExclusiveBgJob();
            var started = new ManualResetEvent(false);
            var hold = new ManualResetEvent(false);
            var done = new ManualResetEvent(false);
            var caller = Thread.CurrentThread.ManagedThreadId;
            var worker = 0;
            string result = null;

            Assert.That(job.TryStart(delegate
            {
                worker = Thread.CurrentThread.ManagedThreadId;
                started.Set();
                hold.WaitOne(5000);
                return "ok";
            }, delegate(string text, System.Exception ex)
            {
                result = text;
                done.Set();
            }), Is.True);

            Assert.That(started.WaitOne(5000), Is.True);
            Assert.That(job.TryStart(delegate { return "no"; }, delegate(string t, System.Exception e) { }), Is.False);
            hold.Set();
            Assert.That(done.WaitOne(5000), Is.True);
            Assert.That(result, Is.EqualTo("ok"));
            Assert.That(worker, Is.Not.EqualTo(caller));
            Assert.That(worker, Is.Not.EqualTo(0));
        }
    }
}
