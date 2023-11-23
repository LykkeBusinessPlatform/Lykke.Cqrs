using Common;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Lykke.Cqrs.Middleware.Logging
{
    /// <summary>
    /// Default command logger.
    /// </summary>
    [PublicAPI]
    public sealed class DefaultCommandLogger : ICommandLogger
    {
        private readonly ILogger _logger;

        /// <summary>C-tor.</summary>
        public DefaultCommandLogger(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DefaultCommandLogger>();
        }

        /// <inheritdoc cref="ICommandLogger"/>
        public void Log(object handler, object command)
        {
            _logger.LogInformation("[{Process}]: {Info}, {Context}",
                handler.GetType().Name,
                command.GetType().Name,
                command.ToJson());
        }
    }
}
