﻿using System.Collections.Generic;
using System.Linq;
using Lykke.Messaging;
using Lykke.Cqrs.Configuration;
using Microsoft.Extensions.Logging;

namespace Lykke.Cqrs
{
    public class InMemoryCqrsEngine : CqrsEngine
    {
        public InMemoryCqrsEngine(ILoggerFactory loggerFactory, params IRegistration[] registrations)
            : base(
                loggerFactory,
                new MessagingEngine(
                    loggerFactory,
                    new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
                new IRegistration[]{Register.DefaultEndpointResolver(new InMemoryEndpointResolver())}.Concat(registrations).ToArray()
            )
        {
        }
        public InMemoryCqrsEngine(
            ILoggerFactory loggerFactory,
            IDependencyResolver dependencyResolver,
            params IRegistration[] registrations)
            : base(
                loggerFactory,
                dependencyResolver,
                new MessagingEngine(
                    loggerFactory,
                    new TransportResolver(new Dictionary<string, TransportInfo> { { "InMemory", new TransportInfo("none", "none", "none", null, "InMemory") } })),
                new DefaultEndpointProvider(),
                new  IRegistration[]{Register.DefaultEndpointResolver(new InMemoryEndpointResolver())}.Concat(registrations).ToArray()
            )
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
