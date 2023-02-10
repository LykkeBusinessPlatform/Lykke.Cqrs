namespace Lykke.Cqrs.Configuration.Routing
{
    /// <summary>
    /// Fluent API interface for route name specification.
    /// </summary>
    public interface IListeningRouteDescriptor<out T> : IDescriptor<Context>
    {
        /// <summary>
        /// Specifies route name for messages listening.
        /// </summary>
        /// <param name="route">Route name.</param>
        /// <returns>Parent fluent API interface.</returns>
        T On(string route);
    }
}