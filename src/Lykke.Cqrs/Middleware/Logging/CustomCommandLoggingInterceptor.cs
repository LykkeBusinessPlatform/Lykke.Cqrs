using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Cqrs.Abstractions.Middleware;
using Microsoft.Extensions.Logging;

namespace Lykke.Cqrs.Middleware.Logging
{
    public delegate void CommandLoggingDelegate(ICommandLogger defaultLogger, object handlerObject, object command);

    /// <summary>
    /// Command interceptor for custom logging.
    /// </summary>
    public sealed class CustomCommandLoggingInterceptor : ICommandInterceptor
    {
        private readonly ICommandLogger _defaultLogger;
        private readonly Dictionary<Type, CommandLoggingDelegate> _customLoggingActionsMap;

        /// <summary>C-tor.</summary>
        /// <param name="loggerFactory">ILogFactory implementation.</param>
        /// <param name="customLoggingActionsMap">Custom logging actions map.</param>
        public CustomCommandLoggingInterceptor(ILoggerFactory loggerFactory,
            Dictionary<Type, CommandLoggingDelegate> customLoggingActionsMap)
            : this(new DefaultCommandLogger(loggerFactory), customLoggingActionsMap)
        {
        }

        /// <summary>C-tor.</summary>
        /// <param name="defaultLogger">Command logger for default logging.</param>
        /// <param name="customLoggingActionsMap">Custom logging actions map.</param>
        public CustomCommandLoggingInterceptor(ICommandLogger defaultLogger,
            Dictionary<Type, CommandLoggingDelegate> customLoggingActionsMap)
        {
            _defaultLogger = defaultLogger;
            _customLoggingActionsMap = customLoggingActionsMap ??
                                       throw new ArgumentNullException(nameof(customLoggingActionsMap));
        }

        /// <inheritdoc cref="ICommandInterceptor"/>
        public Task<CommandHandlingResult> InterceptAsync(ICommandInterceptionContext context)
        {
            if (_customLoggingActionsMap.TryGetValue(context.Command.GetType(), out var customLoggingAction))
                customLoggingAction?.Invoke(_defaultLogger, context.HandlerObject, context.Command);
            else
                _defaultLogger.Log(context.HandlerObject, context.Command);

            return context.InvokeNextAsync();
        }
    }
}
