using System.Collections.Generic;
using Lykke.Cqrs.Routing;

namespace Lykke.Cqrs
{
    /// <summary>
    /// Interface for routes mapping collection.
    /// </summary>
    public interface IRouteMap : IEnumerable<Route>
    {
        /// <summary>
        /// Finds route by its name and creates one if not found.
        /// </summary>
        /// <param name="name">Route name.</param>
        /// <returns>Found or created route.</returns>
        Route this[string name] { get; }
    }
}