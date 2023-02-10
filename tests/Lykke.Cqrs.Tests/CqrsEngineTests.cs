using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lykke.Messaging;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Messaging.Serialization;
using Lykke.Cqrs.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Lykke.Cqrs.Tests
{
    [TestFixture]
    internal class CqrsEngineTests
    {
        private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("ENV_INFO", "NUNIT");
        }

        [Test]
        public void ListenSameCommandOnDifferentEndpointsTest()
        {
            var commandHandler = new CommandsHandler();

            using (var messagingEngine = new MessagingEngine(
                _loggerFactory,
                new TransportInfoResolver(new Dictionary<string, TransportInfo>
                    {
                        {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                    })))
            {
                using (var engine = new InMemoryCqrsEngine(
                    _loggerFactory,
                        Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                        Register.BoundedContext("bc")
                           .PublishingEvents(typeof(int)).With("eventExchange")
                           .ListeningCommands(typeof(string)).On("exchange1")
                           .ListeningCommands(typeof(string)).On("exchange2")
                           .WithCommandsHandler(commandHandler)))
                {
                    engine.StartPublishers();
                    engine.StartSubscribers();
                    messagingEngine.Send("test1", new Endpoint("InMemory", new Destination("exchange1"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("test2", new Endpoint("InMemory", new Destination("exchange2"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("test3", new Endpoint("InMemory", new Destination("exchange3"), serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(10000);

                    Assert.That(commandHandler.HandledCommands, Is.EquivalentTo(new[] { "test1", "test2" }));
                }
            }
        }

        [Test]
        public void ContextUsesDefaultRouteForCommandPublishingIfItDoesNotHaveItsOwnTest()
        {
            var bcCommands = new Endpoint("InMemory", new Destination("bcCommands"), serializationFormat: SerializationFormat.Json);
            var defaultCommands = new Endpoint("InMemory", new Destination("defaultCommands"), serializationFormat: SerializationFormat.Json);
            using (var messagingEngine = new MessagingEngine(
                _loggerFactory,
                new TransportInfoResolver(new Dictionary<string, TransportInfo>
                    {
                        {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                    })))
            {
                using (var engine = new InMemoryCqrsEngine(
                    _loggerFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.BoundedContext("bc2")
                        .PublishingCommands(typeof(int)).To("bc1").With("bcCommands"),
                    Register.DefaultRouting
                        .PublishingCommands(typeof(string)).To("bc1").With("defaultCommands")
                        .PublishingCommands(typeof(int)).To("bc1").With("defaultCommands")))
                {
                    engine.StartPublishers();
                    var received = new AutoResetEvent(false);
                    using (messagingEngine.Subscribe(defaultCommands, o => received.Set(), s => { }, typeof(string)))
                    {
                        engine.SendCommand("test", "bc2", "bc1");
                        Assert.That(received.WaitOne(2000), Is.True, "not defined for context command was not routed with default route map");
                    }

                    using (messagingEngine.Subscribe(bcCommands, o => received.Set(), s => { }, typeof(int)))
                    {
                        engine.SendCommand(1, "bc2", "bc1");
                        Assert.That(received.WaitOne(2000), Is.True, "defined for context command was not routed with context route map");
                    }
                }
            }
        }

        [Test, Ignore("integration")]
        public void SagaTest()
        {
            var commandHandler = new CustomCommandsHandler();


            using (var engine = new InMemoryCqrsEngine(
                       _loggerFactory,
                       Register.DefaultEndpointResolver(
                           new RabbitMqConventionEndpointResolver("rmq", SerializationFormat.Json, environment: "dev")),
                       Register.BoundedContext("operations")
                           .PublishingCommands(typeof(CreateCashOutCommand)).To("lykke-wallet")
                           .With("operations-commands")
                           .ListeningEvents(typeof(CashOutCreatedEvent)).From("lykke-wallet").On("lykke-wallet-events"),

                       Register.BoundedContext("lykke-wallet")
                           .FailedCommandRetryDelay((long)TimeSpan.FromSeconds(2).TotalMilliseconds)
                           .ListeningCommands(typeof(CreateCashOutCommand)).On("operations-commands")
                           .PublishingEvents(typeof(CashOutCreatedEvent)).With("lykke-wallet-events")
                           .WithCommandsHandler(commandHandler),

                       Register.Saga<TestSaga>("swift-cashout")
                           .ListeningEvents(typeof(CashOutCreatedEvent)).From("lykke-wallet").On("lykke-wallet-events"),

                       Register.DefaultRouting.PublishingCommands(typeof(CreateCashOutCommand)).To("lykke-wallet")
                           .With("operations-commands"))
                  )
            {
                engine.StartPublishers();
                engine.StartSubscribers();
                engine.SendCommand(new CreateCashOutCommand { Payload = "test data" }, null, "lykke-wallet");

                Assert.True(TestSaga.Complete.WaitOne(2000), "Saga has not got events or failed to send command");
            }
        }

        [Test]
        public void FluentApiTest()
        {
            var endpointProvider = new Mock<IEndpointProvider>();
            endpointProvider.Setup(r => r.Get("high")).Returns(new Endpoint("InMemory", new Destination("high"), true, SerializationFormat.Json));
            endpointProvider.Setup(r => r.Get("low")).Returns(new Endpoint("InMemory", new Destination("low"), true, SerializationFormat.Json));
            endpointProvider.Setup(r => r.Get("medium")).Returns(new Endpoint("InMemory", new Destination("medium"), true, SerializationFormat.Json));

            using (var messagingEngine = new MessagingEngine(
                _loggerFactory,
                new TransportInfoResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")},
                    {"rmq", new TransportInfo("none", "none", "none", null, "InMemory")}
                })))
            {
                using (var engine = new InMemoryCqrsEngine(
                    _loggerFactory,
                    messagingEngine,
                    Register.BoundedContext("bc")
                        .PublishingCommands(typeof(string)).To("operations").With("operationsCommandsRoute")
                        .ListeningCommands(typeof(string)).On("commandsRoute")
                        //same as .PublishingCommands(typeof(string)).To("bc").With("selfCommandsRoute")  
                        .WithLoopback("selfCommandsRoute")
                        .PublishingEvents(typeof(int)).With("eventsRoute")

                        //explicit prioritization 
                        .ListeningCommands(typeof(string)).On("explicitlyPrioritizedCommandsRoute")
                        .Prioritized(lowestPriority: 2)
                        .WithEndpoint("high").For(key => key.Priority == 0)
                        .WithEndpoint("medium").For(key => key.Priority == 1)
                        .WithEndpoint("low").For(key => key.Priority == 2)

                        //resolver based prioritization 
                        .ListeningCommands(typeof(string)).On("prioritizedCommandsRoute")
                        .Prioritized(lowestPriority: 2)
                        .WithEndpointResolver(new InMemoryEndpointResolver())
                        .WithCommandsHandler(typeof(CommandsHandler))
                        .ProcessingOptions("explicitlyPrioritizedCommandsRoute").MultiThreaded(10)
                        .ProcessingOptions("prioritizedCommandsRoute").MultiThreaded(10).QueueCapacity(1024),
                    Register.Saga<TestSaga>("saga")
                        .PublishingCommands(typeof(string)).To("operations").With("operationsCommandsRoute")
                        .ListeningEvents(typeof(int)).From("operations").On("operationEventsRoute"),
                    Register.DefaultRouting
                        .PublishingCommands(typeof(string)).To("operations").With("defaultCommandsRoute")
                        .PublishingCommands(typeof(int)).To("operations").With("defaultCommandsRoute"),
                    Register.DefaultEndpointResolver(
                        new RabbitMqConventionEndpointResolver("rmq", SerializationFormat.Json))
                ))
                {
                    engine.StartPublishers();
                    engine.StartSubscribers();
                }
            }
        }

        [Test]
        public void PrioritizedCommandsProcessingTest()
        {
            var endpointProvider = new Mock<IEndpointProvider>();
            endpointProvider.Setup(r => r.Get("exchange1")).Returns(new Endpoint("InMemory", new Destination("bc.exchange1"), true, SerializationFormat.Json));
            endpointProvider.Setup(r => r.Get("exchange2")).Returns(new Endpoint("InMemory", new Destination("bc.exchange2"), true, SerializationFormat.Json));
            var commandHandler = new CommandsHandler(false, 100);

            using (var messagingEngine = new MessagingEngine(
                _loggerFactory,
                new TransportInfoResolver(new Dictionary<string, TransportInfo>
                    {
                        {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                    })))
            {
                using (var engine = new InMemoryCqrsEngine(
                    _loggerFactory,
                    messagingEngine,
                    Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                    Register.BoundedContext("bc")
                        .PublishingEvents(typeof(int)).With("eventExchange")//.WithLoopback("eventQueue")
                        .ListeningCommands(typeof(string)).On("commandsRoute")
                            .Prioritized(lowestPriority: 1)
                                .WithEndpoint("exchange1").For(key => key.Priority == 1)
                                .WithEndpoint("exchange2").For(key => key.Priority == 2)
                        .ProcessingOptions("commandsRoute").MultiThreaded(2)
                        .WithCommandsHandler(commandHandler)))
                {
                    engine.StartPublishers();
                    engine.StartSubscribers();
                    messagingEngine.Send("low1", new Endpoint("InMemory", new Destination("bc.exchange2"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low2", new Endpoint("InMemory", new Destination("bc.exchange2"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low3", new Endpoint("InMemory", new Destination("bc.exchange2"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low4", new Endpoint("InMemory", new Destination("bc.exchange2"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low5", new Endpoint("InMemory", new Destination("bc.exchange2"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low6", new Endpoint("InMemory", new Destination("bc.exchange2"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low7", new Endpoint("InMemory", new Destination("bc.exchange2"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low8", new Endpoint("InMemory", new Destination("bc.exchange2"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low9", new Endpoint("InMemory", new Destination("bc.exchange2"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("low10", new Endpoint("InMemory", new Destination("bc.exchange2"), serializationFormat: SerializationFormat.Json));
                    messagingEngine.Send("high", new Endpoint("InMemory", new Destination("bc.exchange1"), serializationFormat: SerializationFormat.Json));
                    Thread.Sleep(2000);

                    Assert.True(commandHandler.HandledCommands.Take(2).Any(c => (string)c == "high"));
                }
            }
        }

        [Test]
        public void ProcessTest()
        {
            var testProcess = new TestProcess();
            var commandHandler = new CommandsHandler();
            using (var engine = new InMemoryCqrsEngine(
                _loggerFactory,
                Register.BoundedContext("local")
                    .ListeningCommands(typeof(string)).On("commands1").WithLoopback()
                    .PublishingEvents(typeof(int)).With("events")
                    .WithCommandsHandler(commandHandler)
                    .WithProcess(testProcess)
            ))
            {
                engine.StartAll();
                Assert.True(testProcess.Started.WaitOne(1000), "process was not started");
                Thread.Sleep(1000);
            }

            Assert.True(testProcess.Disposed.WaitOne(1000), "process was not disposed on engine dispose");
            Assert.True(commandHandler.HandledCommands.Count > 0, "commands sent by process were not processed");
        }

        [Test]
        public void UnhandledListenedEventsTest()
        {
            using (var messagingEngine = new MessagingEngine(
                _loggerFactory,
                new TransportInfoResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                })))
            {
                Assert.Throws<InvalidOperationException>(
                    () => new InMemoryCqrsEngine(
                        _loggerFactory,
                        Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                        Register.Saga<TestSaga>("swift-cashout")
                            .ListeningEvents(GetType()).From("lykke-wallet").On("lykke-wallet-events")),
                    "Engine must throw exception if Saga doesn't handle listened events");
            }
        }

        [Test]
        public void UnhandledListenedCommandsTest()
        {
            using (var messagingEngine = new MessagingEngine(
                _loggerFactory,
                new TransportInfoResolver(new Dictionary<string, TransportInfo>
                {
                    {"InMemory", new TransportInfo("none", "none", "none", null, "InMemory")}
                })))
            {
                Assert.Throws<InvalidOperationException>(
                    () => new InMemoryCqrsEngine(
                        _loggerFactory,
                        Register.DefaultEndpointResolver(new InMemoryEndpointResolver()),
                        Register.BoundedContext("swift-cashout")
                            .ListeningCommands(GetType()).On("lykke-wallet")),
                    "Engine must throw exception if command handler doesn't handle listened commands");
            }
        }
    }
}