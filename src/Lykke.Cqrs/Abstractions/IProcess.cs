using System;

namespace Lykke.Cqrs
{
    /// <summary>
    /// Interface for cqrs process.
    /// </summary>
    public interface IProcess : IDisposable
    {
        /// <summary>
        /// Starts cqrs process.
        /// </summary>
        /// <param name="commandSender">Command semder.</param>
        /// <param name="eventPublisher">Event publisher.</param>
        void Start(ICommandSender commandSender, IEventPublisher eventPublisher);
    }
}
