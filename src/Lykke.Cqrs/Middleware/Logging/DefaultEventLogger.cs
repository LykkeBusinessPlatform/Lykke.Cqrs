using Common;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Lykke.Cqrs.Middleware.Logging
{
    /// <summary>
    /// Default event logger.
    /// </summary>
    [PublicAPI]
    public sealed class DefaultEventLogger: IEventLogger
    {
        private readonly ILogger _logger;

        /// <summary>C-tor.</summary>
        public DefaultEventLogger(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DefaultEventLogger>();
        }

        /// <inheritdoc cref="IEventLogger"/>
        public void Log(object handler, object @event)
        {
            _logger.LogInformation("[{Process}]: {Info}, {Context}", 
                handler.GetType().Name, 
                @event.GetType().Name,
                @event.ToJson());
        }
    }
}
