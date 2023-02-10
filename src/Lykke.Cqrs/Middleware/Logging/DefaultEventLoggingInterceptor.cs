using System.Threading.Tasks;
using Lykke.Cqrs.Abstractions.Middleware;
using Microsoft.Extensions.Logging;

namespace Lykke.Cqrs.Middleware.Logging
{
    /// <summary>
    /// Default event logging interceptor.
    /// </summary>
    public sealed class DefaultEventLoggingInterceptor : IEventInterceptor
    {
        private readonly IEventLogger _eventLogger;

        /// <summary>C-tor.</summary>
        public DefaultEventLoggingInterceptor(ILoggerFactory loggerFactory)
            : this(new DefaultEventLogger(loggerFactory))
        {
        }

        /// <summary>C-tor.</summary>
        public DefaultEventLoggingInterceptor(IEventLogger eventLogger)
        {
            _eventLogger = eventLogger;
        }

        /// <inheritdoc cref="IEventInterceptor"/>
        public Task<CommandHandlingResult> InterceptAsync(IEventInterceptionContext context)
        {
            _eventLogger.Log(context.HandlerObject, context.Event);

            return context.InvokeNextAsync();
        }
    }
}
