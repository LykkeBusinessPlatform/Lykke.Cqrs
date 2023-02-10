using System;
using System.Collections.Generic;

namespace Lykke.Cqrs.Configuration
{
    /// <summary>
    /// Base interface for cqrs registrations.
    /// </summary>
    public interface IRegistration
    {
        /// <summary>
        /// Collection of registration dependencies' types.
        /// </summary>
        IEnumerable<Type> Dependencies { get; }

        /// <summary>
        /// Creates registration builder based on registration fluent API calls sequence.
        /// </summary>
        /// <param name="cqrsEngine">Cqrs engine.</param>
        void Create(CqrsEngine cqrsEngine);

        /// <summary>
        /// Builds registration for cqrs engine.
        /// </summary>
        /// <param name="cqrsEngine">Cqrs engine.</param>
        void Process(CqrsEngine cqrsEngine);
    }
}