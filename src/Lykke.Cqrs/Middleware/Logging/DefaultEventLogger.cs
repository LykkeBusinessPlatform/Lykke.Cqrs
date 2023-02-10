using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Lykke.Cqrs.Middleware.Logging
{
    /// <summary>
    /// Default event logger.
    /// </summary>
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
            var eventJson = JsonConvert.SerializeObject(@event);
            
            _logger.LogInformation("[{Process}]: {Info}, {Context}", 
                handler.GetType().Name, 
                @event.GetType().Name,
                eventJson);
        }
    }
}
