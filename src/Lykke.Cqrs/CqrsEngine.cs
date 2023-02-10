using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;
using Lykke.Cqrs.Configuration;
using Lykke.Cqrs.Middleware;
using Lykke.Cqrs.Utils;
using Microsoft.Extensions.Logging;

namespace Lykke.Cqrs
{
    public abstract class CqrsEngine : ICqrsEngine, IDisposable
    {
        private readonly CompositeDisposable _subscription = new CompositeDisposable();
        private readonly IEndpointProvider _endpointProvider;
        private readonly bool _createMissingEndpoints;

        private readonly List<Action<IDictionary<string, string>>> _readHeadersActions =
            new List<Action<IDictionary<string, string>>>();

        private readonly List<Func<IDictionary<string, string>>> _writeHeadersFuncList =
            new List<Func<IDictionary<string, string>>>();

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<CqrsEngine> _logger;

        protected IMessagingEngine MessagingEngine { get; }

        internal IEndpointResolver EndpointResolver { get; set; }
        internal List<Context> Contexts { get; }
        internal CommandInterceptorsQueue CommandInterceptorsQueue { get; }
        internal EventInterceptorsQueue EventInterceptorsQueue { get; }
        internal IDependencyResolver DependencyResolver { get; }

        public RouteMap DefaultRouteMap { get; }

        public CqrsEngine(
            ILoggerFactory loggerFactory,
            IMessagingEngine messagingEngine,
            params IRegistration[] registrations)
            : this(
                loggerFactory,
                new DefaultDependencyResolver(),
                messagingEngine,
                new DefaultEndpointProvider(),
                false,
                registrations)
        {
        }

        public CqrsEngine(
            ILoggerFactory loggerFactory,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            params IRegistration[] registrations)
            : this(
                loggerFactory,
                new DefaultDependencyResolver(),
                messagingEngine,
                endpointProvider,
                false,
                registrations)
        {
        }

        public CqrsEngine(
            ILoggerFactory loggerFactory,
            IDependencyResolver dependencyResolver,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            params IRegistration[] registrations)
            : this(
                loggerFactory,
                dependencyResolver,
                messagingEngine,
                endpointProvider,
                false,
                registrations)
        {
        }

        public CqrsEngine(
            ILoggerFactory loggerFactory,
            IDependencyResolver dependencyResolver,
            IMessagingEngine messagingEngine,
            IEndpointProvider endpointProvider,
            bool createMissingEndpoints,
            params IRegistration[] registrations)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<CqrsEngine>();
            _createMissingEndpoints = createMissingEndpoints;
            DependencyResolver = dependencyResolver;
            EndpointResolver = new DefaultEndpointResolver();
            MessagingEngine = messagingEngine;
            _endpointProvider = endpointProvider;
            Contexts = new List<Context>();
            DefaultRouteMap = new RouteMap("default");
            CommandInterceptorsQueue = new CommandInterceptorsQueue();
            EventInterceptorsQueue = new EventInterceptorsQueue();

            InitRegistrations(registrations);
        }

        internal CommandDispatcher CreateCommandsDispatcher(string name, long failedCommandRetryDelay)
        {
            return new CommandDispatcher(
                _loggerFactory,
                name,
                CommandInterceptorsQueue,
                failedCommandRetryDelay);
        }

        internal EventDispatcher CreateEventsDispatcher(string name)
        {
            return new EventDispatcher(
                _loggerFactory,
                name,
                EventInterceptorsQueue);
        }

        public void StartPublishers()
        {
            EnsureEndpoints(CommunicationType.Publish);
        }

        public void StartSubscribers()
        {
            EnsureEndpoints(CommunicationType.Subscribe);

            InitSubscriptions();
        }

        public void StartProcesses()
        {
            foreach (var boundedContext in Contexts)
            {
                boundedContext.Processes.ForEach(p => p.Start(boundedContext, boundedContext.EventsPublisher));
            }
        }

        public void StartAll()
        {
            StartPublishers();
            StartSubscribers();
            StartProcesses();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            Contexts
                .Where(b => b?.Processes != null)
                .SelectMany(p => p.Processes)
                .ToList()
                .ForEach(process => process.Dispose());
            Contexts
                .Where(b => b != null)
                .ToList()
                .ForEach(context => context.Dispose());

            _subscription?.Dispose();
        }

        public void SendCommand<T>(T command, string boundedContext, string remoteBoundedContext, uint priority = 0)
        {
            if (!SendMessage(typeof(T), command, RouteType.Commands, boundedContext, priority, remoteBoundedContext))
            {
                if (boundedContext != null)
                    throw new InvalidOperationException(
                        $"Bound context '{boundedContext}' does not support command '{typeof(T)}' with priority {priority}");
                throw new InvalidOperationException(
                    $"Default route map does not contain rout for command '{typeof(T)}' with priority {priority}");
            }
        }

        public void PublishEvent(object @event, string boundedContext)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));
            if (!SendMessage(@event.GetType(), @event, RouteType.Events, boundedContext, 0))
                throw new InvalidOperationException(
                    $"Bound context '{boundedContext}' does not support event '{@event.GetType()}'");
        }

        public void SetReadHeadersAction(Action<IDictionary<string, string>> action)
        {
            if (action != null)
            {
                _readHeadersActions.Add(action);
            }
        }

        public void SetWriteHeadersFunc(Func<IDictionary<string, string>> func)
        {
            if (func != null)
            {
                _writeHeadersFuncList.Add(func);
            }
        }

        private bool SendMessage(Type type, object message, RouteType routeType, string context, uint priority,
            string remoteBoundedContext = null)
        {
            RouteMap routeMap = DefaultRouteMap;
            if (context != null)
            {
                routeMap = Contexts.FirstOrDefault(bc => bc.Name == context);
                if (routeMap == null)
                    throw new ArgumentException($"Bound context {context} not found", nameof(context));
            }

            var telemtryOperation = TelemetryHelper.InitTelemetryOperation(
                routeType == RouteType.Commands ? "Cqrs send command" : "Cqrs publish event",
                type.Name,
                context,
                remoteBoundedContext);
            var headers = GetMessageHeaders();
            try
            {
                var published = routeMap.PublishMessage(
                    MessagingEngine,
                    type,
                    message,
                    routeType,
                    priority,
                    remoteBoundedContext,
                    headers);
                if (!published && routeType == RouteType.Commands)
                    published = DefaultRouteMap.PublishMessage(
                        MessagingEngine,
                        type,
                        message,
                        routeType,
                        priority,
                        remoteBoundedContext,
                        headers);
                return published;
            }
            catch (Exception e)
            {
                TelemetryHelper.SubmitException(telemtryOperation, e);
                throw;
            }
            finally
            {
                TelemetryHelper.SubmitOperationResult(telemtryOperation);
            }
        }

        private Dictionary<string, string> GetMessageHeaders()
        {
            var result = new Dictionary<string, string>();

            var keyValuePairs = _writeHeadersFuncList
                .Select(x => x())
                .Where(x => x != null && x.Any())
                .SelectMany(x => x);

            if (keyValuePairs.Any())
            {
                foreach (var keyValuePair in keyValuePairs)
                {
                    if (result.ContainsKey(keyValuePair.Key))
                    {
                        _logger.LogError(
                            "Header with key '{Key}' already exists. Discarded value is '{Value}'. Please, use unique headers only.",
                            keyValuePair.Key,
                            keyValuePair.Value);
                    }
                    else
                    {
                        result.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                }
            }

            return result;
        }

        private void InitRegistrations(IEnumerable<IRegistration> registrations)
        {
            foreach (var registration in registrations)
            {
                registration.Create(this);
            }

            foreach (var registration in registrations)
            {
                registration.Process(this);
            }

            foreach (var routeMap in new List<RouteMap> {DefaultRouteMap}.Concat(Contexts))
            {
                foreach (var route in routeMap)
                {
                    MessagingEngine.AddProcessingGroup(route.ProcessingGroupName, route.ProcessingGroup);
                }

                routeMap.ResolveRoutes(_endpointProvider);
            }
        }

        private void EnsureEndpoints(CommunicationType processingCommunicationType)
        {
            var allEndpointsAreValid = true;
            var errorMessage = new StringBuilder("Some endpoints are not valid:").AppendLine();
            var endpointMessagesDict = new Dictionary<Endpoint, string>();

            _logger.LogInformation(
                "{Method}: Endpoints verification for {ProcessingCommunicationType}",
                nameof(EnsureEndpoints),
                processingCommunicationType);

            foreach (var routeMap in new List<RouteMap> {DefaultRouteMap}.Concat(Contexts))
            {
                foreach (var route in routeMap)
                {
                    foreach (var messageRoute in route.MessageRoutes)
                    {
                        var routingKey = messageRoute.Key;
                        if (routingKey.CommunicationType != processingCommunicationType)
                            continue;

                        var endpoint = messageRoute.Value;
                        endpointMessagesDict[endpoint] =
                            $"Context {routeMap.Name}: "
                            + (processingCommunicationType == CommunicationType.Publish
                                ? $"publishing '{routingKey.MessageType.Name}' to"
                                : $"subscribing '{routingKey.MessageType.Name}' on")
                            + $" {endpoint}\t{{0}}";
                    }
                }
            }

            var endpointsErrorsDict = MessagingEngine.VerifyEndpoints(
                processingCommunicationType == CommunicationType.Publish
                    ? EndpointUsage.Publish
                    : EndpointUsage.Subscribe,
                endpointMessagesDict.Keys,
                _createMissingEndpoints);
            foreach (var endpointError in endpointsErrorsDict)
            {
                string messagePattern = endpointMessagesDict[endpointError.Key];
                if (string.IsNullOrWhiteSpace(endpointError.Value))
                    _logger.LogInformation("{Method}: {Message}",
                        nameof(EnsureEndpoints),
                        string.Format(messagePattern, "OK"));
                else
                {
                    var message = string.Format(messagePattern, $"ERROR: {endpointError.Value}");
                    _logger.LogError(
                        new InvalidOperationException(message),
                        "{Method}: {Message}",
                        nameof(EnsureEndpoints),
                        message);
                }
            }

            if (!allEndpointsAreValid)
                throw new InvalidOperationException(errorMessage.ToString());
        }

        private void InitSubscriptions()
        {
            foreach (var boundedContext in Contexts)
            {
                foreach (var route in boundedContext.Routes)
                {
                    Context context = boundedContext;
                    var subscriptions = route.MessageRoutes
                        .Where(r => r.Key.CommunicationType == CommunicationType.Subscribe)
                        .Select(r => new
                        {
                            type = r.Key.MessageType,
                            priority = r.Key.Priority,
                            remoteBoundedContext = r.Key.RemoteBoundedContext,
                            endpoint = new Endpoint(
                                r.Value.TransportId,
                                new Destination(null, r.Value.Destination.Subscribe),
                                r.Value.SharedDestination,
                                r.Value.SerializationFormat)
                        })
                        .GroupBy(x => Tuple.Create(x.endpoint, x.priority, x.remoteBoundedContext))
                        .Select(g => new
                        {
                            endpoint = g.Key.Item1,
                            priority = g.Key.Item2,
                            remoteBoundedContext = g.Key.Item3,
                            types = g.Select(x => x.type).ToArray()
                        });

                    foreach (var subscription in subscriptions)
                    {
                        var processingGroup = route.ProcessingGroupName;
                        var routeName = route.Name;
                        var endpoint = subscription.endpoint;
                        var remoteBoundedContext = subscription.remoteBoundedContext;
                        CallbackDelegate<object> callback = null;
                        string messageTypeName = null;
                        switch (route.Type)
                        {
                            case RouteType.Events:
                                callback = (@event, acknowledge, headers) =>
                                {
                                    _readHeadersActions.ForEach(x => x(headers));
                                    context.EventDispatcher.Dispatch(remoteBoundedContext,
                                        new[] {Tuple.Create(@event, acknowledge)});
                                };
                                messageTypeName = "event";
                                break;
                            case RouteType.Commands:
                                callback = (command, acknowledge, headers) =>
                                {
                                    _readHeadersActions.ForEach(x => x(headers));
                                    context.CommandDispatcher.Dispatch(command, acknowledge, endpoint, routeName);
                                };
                                messageTypeName = "command";
                                break;
                        }

                        _subscription.Add(MessagingEngine.Subscribe(
                            endpoint,
                            callback,
                            (type, acknowledge) =>
                                throw new InvalidOperationException($"Unknown {messageTypeName} received: {type}"),
                            processingGroup,
                            (int) subscription.priority,
                            subscription.types));
                    }
                }
            }
        }
    }
}