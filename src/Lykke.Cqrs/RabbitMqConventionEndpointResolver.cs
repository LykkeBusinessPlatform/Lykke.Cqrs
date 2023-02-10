﻿using System;
using System.Collections.Generic;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Messaging.Serialization;
using Lykke.Cqrs.Routing;

namespace Lykke.Cqrs
{
    public class RabbitMqConventionEndpointResolver : IEndpointResolver
    {
        private readonly Dictionary<Tuple<string, RoutingKey>, Endpoint> _cache =
            new Dictionary<Tuple<string, RoutingKey>, Endpoint>();

        private readonly string _transport;
        private readonly SerializationFormat _serializationFormat;
        private readonly string _exclusiveQueuePostfix;
        private readonly string _environmentPrefix;
        private readonly string _commandsKeyword;
        private readonly string _eventsKeyword;

        public RabbitMqConventionEndpointResolver(
            string transport,
            SerializationFormat serializationFormat,
            string exclusiveQueuePostfix = null,
            string environment = null,
            string commandsKeyword = null,
            string eventsKeyword = null)
        {
            _environmentPrefix = environment != null ? $"{environment}." : string.Empty;
            _exclusiveQueuePostfix = $".{exclusiveQueuePostfix ?? "projections"}";
            _transport = transport;
            _serializationFormat = serializationFormat;
            _commandsKeyword = commandsKeyword;
            _eventsKeyword = eventsKeyword;
        }

        private string CreateQueueName(string queue, bool exclusive)
        {
            return $"{_environmentPrefix}{queue}{(exclusive ? _exclusiveQueuePostfix : string.Empty)}";
        }

        private string CreateExchangeName(string exchange)
        {
            return $"topic://{_environmentPrefix}{exchange}";
        }

        private Endpoint CreateEndpoint(string route, RoutingKey key)
        {
            var rmqRoutingKey = key.Priority == 0 ? key.MessageType.Name : key.MessageType.Name + "." + key.Priority;
            var queueName = key.Priority == 0 ? route : route + "." + key.Priority;
            if (key.RouteType == RouteType.Commands && key.CommunicationType == CommunicationType.Subscribe)
            {
                return new Endpoint(
                    _transport,
                    new Destination(
                        CreateExchangeName(
                            $"{key.LocalContext}.{GetKewordByRoutType(key.RouteType)}.exchange/{rmqRoutingKey}"),
                        CreateQueueName($"{key.LocalContext}.queue.{GetKewordByRoutType(key.RouteType)}.{queueName}",
                            key.Exclusive)),
                    true,
                    _serializationFormat);
            }

            if (key.RouteType == RouteType.Commands && key.CommunicationType == CommunicationType.Publish)
            {
                return new Endpoint(
                    _transport,
                    new Destination(
                        CreateExchangeName(
                            $"{key.RemoteBoundedContext}.{GetKewordByRoutType(key.RouteType)}.exchange/{rmqRoutingKey}"),
                        null),
                    true,
                    _serializationFormat);
            }

            if (key.RouteType == RouteType.Events && key.CommunicationType == CommunicationType.Subscribe)
            {
                return new Endpoint(_transport,
                    new Destination(
                        CreateExchangeName(
                            $"{key.RemoteBoundedContext}.{GetKewordByRoutType(key.RouteType)}.exchange/{key.MessageType.Name}"),
                        CreateQueueName(
                            $"{key.LocalContext}.queue.{key.RemoteBoundedContext}.{GetKewordByRoutType(key.RouteType)}.{route}",
                            key.Exclusive)),
                    true,
                    _serializationFormat);
            }

            if (key.RouteType == RouteType.Events && key.CommunicationType == CommunicationType.Publish)
            {
                return new Endpoint(_transport,
                    new Destination(
                        CreateExchangeName(
                            $"{key.LocalContext}.{GetKewordByRoutType(key.RouteType)}.exchange/{key.MessageType.Name}"),
                        null),
                    true,
                    _serializationFormat);
            }
            return default(Endpoint);
        }

        private string GetKewordByRoutType(RouteType routeType)
        {
            string keyword = null;
            switch (routeType)
            {
                case RouteType.Commands:
                    keyword = _commandsKeyword;
                    break;
                case RouteType.Events:
                    keyword = _eventsKeyword;
                    break;
            }
            return keyword ?? routeType.ToString().ToLower();
        }

        public Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(Tuple.Create(route, key), out var ep))
                    return ep;

                if (endpointProvider.Contains(route))
                {
                    ep = endpointProvider.Get(route);
                    _cache.Add(Tuple.Create(route, key), ep);
                    return ep;
                }

                ep = CreateEndpoint(route, key);
                _cache.Add(Tuple.Create(route, key), ep);
                return ep;
            }
        }
    }
}