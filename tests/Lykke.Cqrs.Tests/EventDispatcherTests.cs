using System;
using System.Linq;
using System.Threading;
using Lykke.Common.Log;
using Lykke.Messaging.Contract;
using Lykke.Logs;
using NUnit.Framework;

namespace Lykke.Cqrs.Tests
{
    [TestFixture]
    public class EventDispatcherTests : IDisposable
    {
        private readonly ILogFactory _logFactory;

        public EventDispatcherTests()
        {
            _logFactory = LogFactory.Create();
        }

        public void Dispose()
        {
            _logFactory?.Dispose();
        }
        
        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("ENV_INFO", "NUNIT");
        }

        [Test]
        public void WireTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandler();
            var now = DateTime.UtcNow;
            bool ack1 = false;
            bool ack2 = false;
            bool ack3 = false;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { ack1 = acknowledge; });
            dispatcher.Dispatch("testBC", 1, (delay, acknowledge) => { ack2 = acknowledge; });
            dispatcher.Dispatch("testBC", now, (delay, acknowledge) => { ack3 = acknowledge; });

            Assert.That(handler.HandledEvents, Is.EquivalentTo(new object[] { "test", 1, now}), "Some events were not dispatched");
            Assert.That(ack1, Is.True, "Handled string command was not acknowledged");
            Assert.That(ack2, Is.True, "Handled int command was not acknowledged");
            Assert.That(ack3, Is.True, "Handled datetime command was not acknowledged");
        }

        [Test]
        public void MultipleHandlersDispatchTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler1 = new EventHandler();
            var handler2 = new EventHandler();
            bool ack = false;

            dispatcher.Wire("testBC", handler1);
            dispatcher.Wire("testBC", handler2);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { ack = acknowledge; });

            Assert.That(handler1.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
            Assert.That(handler2.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched");
            Assert.That(ack, Is.True, "Handled command was not acknowledged");
        }

        [Test]
        public void FailingHandlersDispatchTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler1 = new EventHandler();
            var handler2 = new EventHandler(true);
            Tuple<long, bool> result = null;

            dispatcher.Wire("testBC", handler1);
            dispatcher.Wire("testBC", handler2);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); });

            Assert.That(handler1.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched to first handler");
            Assert.That(handler2.HandledEvents, Is.EquivalentTo(new[] { "test" }), "Event was not dispatched to second handler");
            Assert.That(result, Is.Not.Null, "fail was not reported");
            Assert.That(EventDispatcher.FailedEventRetryDelay, Is.EqualTo(result.Item1), "fail was not reported");
            Assert.That(result.Item2, Is.False, "fail was not reported");
        }

        [Test]
        public void RetryingHandlersDispatchTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new ResultEventHandler(true, 100);
            Tuple<long, bool> result = null;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); });

            Assert.That(result, Is.Not.Null, "fail was not reported");
            Assert.That(100, Is.EqualTo(result.Item1), "fail was not reported");
            Assert.That(result.Item2, Is.False, "fail was not reported");
        }

        // Note: Strange logic in EventDispatcher - might need to be revised in the future.
        [Test]
        public void EventWithNoHandlerIsAcknowledgedTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            bool ack = false;

            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { ack = acknowledge; });

            Assert.That(ack, Is.True);
        }

        [Test]
        public void AsyncEventHadlerTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var asyncHandler = new EventAsyncHandler(false);
            bool ack = false;

            dispatcher.Wire("testBC", asyncHandler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { ack = acknowledge; });

            Assert.That(1, Is.EqualTo(asyncHandler.HandledEvents.Count));
            Assert.That(ack, Is.True, "Event handler was not processed properly");
        }

        [Test]
        public void ExceptionForAsyncEventHadlerTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventAsyncHandler(true);
            int failedCount = 0;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) =>
            {
                if (!acknowledge)
                    ++failedCount;
            });

            Assert.That(0, Is.EqualTo(handler.HandledEvents.Count));
            Assert.That(1, Is.EqualTo(failedCount), "Event handler was not processed properly");
        }

        [Test]
        public void AsyncResultEventHadlerTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventAsyncResultHandler(false);
            bool ack = false;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) => { ack = acknowledge; });

            Assert.That(1, Is.EqualTo(handler.HandledEvents.Count));
            Assert.That(ack, Is.True, "Event handler was not processed properly");
        }

        [Test]
        public void ExceptionForAsyncResultEventHadlerTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventAsyncResultHandler(true);
            int failedCount = 0;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", "test", (delay, acknowledge) =>
            {
                if (!acknowledge)
                    ++failedCount;
            });

            Assert.That(0, Is.EqualTo(handler.HandledEvents.Count));
            Assert.That(1, Is.EqualTo(failedCount), "Event handler was not processed properly");
        }

        [Test]
        public void BatchDispatchTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandler {FailOnce = true};
            Tuple<long, bool> result = null;
            bool ack2 = false;
            bool ack3 = false;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", new []
            {
                Tuple.Create<object, AcknowledgeDelegate>("a", (delay, acknowledge) => {result = Tuple.Create(delay, acknowledge); }),
                Tuple.Create<object, AcknowledgeDelegate>("b", (delay, acknowledge) => { ack2 = acknowledge; }),
                Tuple.Create<object, AcknowledgeDelegate>("с", (delay, acknowledge) => { ack3 = acknowledge; })
            });

            Assert.That(result, Is.Not.Null, "fail was not reported");
            Assert.That(result.Item2, Is.False, "fail was not reported");
            Assert.That(3, Is.EqualTo(handler.HandledEvents.Count), "not all events were handled (exception in first event handling prevented following events processing?)");
            Assert.That(ack2, Is.True);
            Assert.That(ack3, Is.True);
        }

        [Test]
        public void BatchDispatchWithBatchHandlerOkTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventBatchHandler(false);
            bool ack1 = false;
            bool ack2 = false;
            bool ack3 = false;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>("a", (delay, acknowledge) => { ack1 = acknowledge; }),
                Tuple.Create<object, AcknowledgeDelegate>("b", (delay, acknowledge) => { ack2 = acknowledge; }),
                Tuple.Create<object, AcknowledgeDelegate>("с", (delay, acknowledge) => { ack3 = acknowledge; })
            });

            Assert.That(3, Is.EqualTo(handler.HandledEvents.Count), "not all events were handled (exception in first event handling prevented following events processing?)");
            Assert.That(ack1, Is.True);
            Assert.That(ack2, Is.True);
            Assert.That(ack3, Is.True);
        }

        [Test]
        public void BatchDispatchWithBatchHandlerFailTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventBatchHandler(true);
            bool ack1 = true;
            bool ack2 = true;
            bool ack3 = true;

            dispatcher.Wire("testBC", handler);
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>("a", (delay, acknowledge) => { ack1 = acknowledge; }),
                Tuple.Create<object, AcknowledgeDelegate>("b", (delay, acknowledge) => { ack2 = acknowledge; }),
                Tuple.Create<object, AcknowledgeDelegate>("с", (delay, acknowledge) => { ack3 = acknowledge; })
            });

            Assert.That(0, Is.EqualTo(handler.HandledEvents.Count), "Some events were handled");
            Assert.That(ack1, Is.False);
            Assert.That(ack2, Is.False);
            Assert.That(ack3, Is.False);
        }

        [Test]
        public void BatchDispatchTriggeringBySizeTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandlerWithBatchSupport();

            dispatcher.Wire(
                "testBC",
                handler,
                3,
                0,
                typeof(FakeBatchContext),
                h => ((EventHandlerWithBatchSupport)h).OnBatchStart(),
                (h, c) => ((EventHandlerWithBatchSupport)h).OnBatchFinish((FakeBatchContext)c));
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,1), (delay, acknowledge) => { Tuple.Create(delay, acknowledge); }),
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,2), (delay, acknowledge) => { }),
            });

            Assert.That(0, Is.EqualTo(handler.HandledEvents.Count), "Events were delivered before batch is filled");

            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,3), (delay, acknowledge) => { })
            });

            Assert.That(3, Is.EqualTo(handler.HandledEvents.Count), "Not all events were delivered");
            Assert.That(handler.BatchStartReported, Is.True, "Batch start callback was not called");
            Assert.That(handler.BatchFinishReported, Is.True, "Batch after apply  callback was not called");
            Assert.That(
                handler.HandledEvents.Select(t=>t.Item2),
                Is.EqualTo(new object[]{handler.LastCreatedBatchContext,handler.LastCreatedBatchContext,handler.LastCreatedBatchContext}),
                "Batch context was not the same for all evants in the batch");
        }

        [Test]
        public void BatchDispatchTriggeringByTimeoutTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandlerWithBatchSupport();

            dispatcher.Wire(
                "testBC",
                handler,
                3,
                1,
                typeof(FakeBatchContext),
                h => ((EventHandlerWithBatchSupport)h).OnBatchStart(),
                (h, c) => ((EventHandlerWithBatchSupport)h).OnBatchFinish((FakeBatchContext)c));
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,1), (delay, acknowledge) => { Tuple.Create(delay, acknowledge); }),
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,2), (delay, acknowledge) => { })
            });

            Assert.That(0, Is.EqualTo(handler.HandledEvents.Count), "Events were delivered before batch apply timeoout");

            Thread.Sleep(2000);

            Assert.That(2, Is.EqualTo(handler.HandledEvents.Count), "Not all events were delivered");
            Assert.That(handler.BatchStartReported, Is.True, "Batch start callback was not called");
            Assert.That(handler.BatchFinishReported, Is.True, "Batch after apply  callback was not called");
            Assert.That(
                handler.HandledEvents.Select(t => t.Item2),
                Is.EqualTo(new object[] { handler.LastCreatedBatchContext, handler.LastCreatedBatchContext }),
                "Batch context was not the same for all evants in the batch");
        }

        [Test]
        public void BatchDispatchUnackTest()
        {
            var dispatcher = new EventDispatcher(_logFactory, "testBC");
            var handler = new EventHandlerWithBatchSupport(1);
            Tuple<long, bool> result = null;

            dispatcher.Wire(
                "testBC",
                handler,
                3,
                0,
                typeof(FakeBatchContext),
                h => ((EventHandlerWithBatchSupport)h).OnBatchStart(),
                (h, c) => ((EventHandlerWithBatchSupport)h).OnBatchFinish((FakeBatchContext)c));
            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,1), (delay, acknowledge) => { result = Tuple.Create(delay, acknowledge); }),
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,2), (delay, acknowledge) => { }),
            });

            Assert.That(0, Is.EqualTo(handler.HandledEvents.Count), "Events were delivered before batch is filled");

            dispatcher.Dispatch("testBC", new[]
            {
                Tuple.Create<object, AcknowledgeDelegate>(new DateTime(2016,3,3), (delay, acknowledge) => { })
            });

            Assert.That(3, Is.EqualTo(handler.HandledEvents.Count), "Not all events were delivered");
            Assert.That(handler.BatchStartReported, Is.True, "Batch start callback was not called");
            Assert.That(handler.BatchFinishReported, Is.True, "Batch after apply  callback was not called");
            Assert.That(
                handler.HandledEvents.Select(t => t.Item2),
                Is.EqualTo(new object[] { handler.LastCreatedBatchContext, handler.LastCreatedBatchContext, handler.LastCreatedBatchContext }),
                "Batch context was not the same for all evants in the batch");
            Assert.That(result.Item2, Is.False,"failed event was acked");
            Assert.That(10, Is.EqualTo(result.Item1),"failed event retry timeout was wrong");
        }
    }
}
