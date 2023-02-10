using System;
using Autofac;

namespace Lykke.Cqrs
{
    /// <summary>
    /// Dependency resolver for Autofac container.
    /// </summary>
    public class AutofacDependencyResolver : IDependencyResolver
    {
        private readonly IComponentContext _context;

        /// <summary>
        /// C-tor
        /// </summary>
        /// <param name="context">Autofac component context.</param>
        public AutofacDependencyResolver(IComponentContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc cref="IDependencyResolver"/>>
        public object GetService(Type type)
        {
            return _context.Resolve(type);
        }

        /// <inheritdoc cref="IDependencyResolver"/>>
        public bool HasService(Type type)
        {
            return _context.IsRegistered(type);
        }
    }
}
