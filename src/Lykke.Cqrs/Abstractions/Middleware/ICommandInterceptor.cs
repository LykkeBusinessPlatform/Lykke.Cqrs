using System.Threading.Tasks;
using Lykke.Cqrs.Middleware;

namespace Lykke.Cqrs.Abstractions.Middleware
{
    /// <summary>
    /// Interface for cqrs commands processing middleware.
    /// </summary>
    public interface ICommandInterceptor
    {
        /// <summary>
        /// Command processing call to middleware with provided inited context.
        /// </summary>
        /// <param name="context">Middleware processing context - <see cref="ICommandInterceptionContext"/></param>
        /// <returns>Task with command processing result - <see cref="CommandHandlingResult"/></returns>
        Task<CommandHandlingResult> InterceptAsync(ICommandInterceptionContext context);
    }
}
