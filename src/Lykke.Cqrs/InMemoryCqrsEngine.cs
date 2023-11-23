using System;
using System.Collections.Generic;
using System.Linq;
using Lykke.Common.Log;
using Lykke.Messaging;
using Lykke.Cqrs.Configuration;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;

namespace Lykke.Cqrs
{
    public class InMemoryCqrsEngine : CqrsEngine
    {
        public InMemoryCqrsEngine(ILogFactory logFactory, params IRegistration[] registrations)
            : base(
                logFactory,
                new MessagingEngine(
                    logFactory,
                    new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
                new IRegistration[]{Register.DefaultEndpointResolver(new InMemoryEndpointResolver())}.Concat(registrations).ToArray()
            )
        {
        }
        public InMemoryCqrsEngine(
            ILogFactory logFactory,
            IDependencyResolver dependencyResolver,
            params IRegistration[] registrations)
            : base(
                logFactory,
                dependencyResolver,
                new MessagingEngine(
                    logFactory,
                    new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
                new DefaultEndpointProvider(),
                new  IRegistration[]{Register.DefaultEndpointResolver(new InMemoryEndpointResolver())}.Concat(registrations).ToArray()
            )
        {
        }

        [Obsolete("Please, take care of messaging engine disposal")]
        public InMemoryCqrsEngine(ILogFactory logFactory,
            IMessagingEngine messagingEngine,
            params IRegistration[] registrations) : base(logFactory, messagingEngine, registrations)
        {
        }

        [Obsolete("Please, take care of messaging engine disposal")]
        public InMemoryCqrsEngine(ILogFactory logFactory,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            params IRegistration[] registrations) : base(logFactory, messagingEngine, endpointProvider, registrations)
        {
        }

        [Obsolete("Please, take care of messaging engine disposal")]
        public InMemoryCqrsEngine(ILogFactory logFactory,
            IDependencyResolver dependencyResolver,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            params IRegistration[] registrations) : base(logFactory, dependencyResolver, messagingEngine,
            endpointProvider, registrations)
        {
        }

        [Obsolete("Please, take care of messaging engine disposal")]
        public InMemoryCqrsEngine(ILogFactory logFactory,
            IDependencyResolver dependencyResolver,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            bool createMissingEndpoints,
            params IRegistration[] registrations) : base(logFactory, dependencyResolver, messagingEngine,
            endpointProvider, createMissingEndpoints, registrations)
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                MessagingEngine.Dispose();
            }
        }
    }
}
