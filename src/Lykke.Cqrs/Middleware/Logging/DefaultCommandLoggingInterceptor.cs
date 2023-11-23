using System.Threading.Tasks;
using JetBrains.Annotations;
using Lykke.Cqrs.Abstractions.Middleware;
using Microsoft.Extensions.Logging;

namespace Lykke.Cqrs.Middleware.Logging
{
    /// <summary>
    /// Default command logging interceptor.
    /// </summary>
    [PublicAPI]
    public sealed class DefaultCommandLoggingInterceptor : ICommandInterceptor
    {
        private readonly ICommandLogger _commandLogger;

        /// <summary>C-tor.</summary>
        public DefaultCommandLoggingInterceptor(ILoggerFactory loggerFactory)
            : this(new DefaultCommandLogger(loggerFactory))
        {
        }

        /// <summary>C-tor.</summary>
        public DefaultCommandLoggingInterceptor(ICommandLogger commandLogger)
        {
            _commandLogger = commandLogger;
        }

        /// <inheritdoc cref="ICommandInterceptor"/>
        public Task<CommandHandlingResult> InterceptAsync(ICommandInterceptionContext context)
        {
            _commandLogger.Log(context.HandlerObject, context.Command);

            return context.InvokeNextAsync();
        }
    }
}
