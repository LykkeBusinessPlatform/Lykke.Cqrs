﻿using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Messaging.Serialization;
using Lykke.Cqrs.Routing;

namespace Lykke.Cqrs
{
    public class InMemoryEndpointResolver : IEndpointResolver
    {
        public Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider)
        {
            if(key.Priority == 0)
                return new Endpoint(
                    "InMemory",
                    new Destination(route),
                    true,
                    SerializationFormat.Json);
            return new Endpoint(
                "InMemory",
                new Destination(route + "." + key.Priority),
                true,
                SerializationFormat.Json);
        }
    }
}