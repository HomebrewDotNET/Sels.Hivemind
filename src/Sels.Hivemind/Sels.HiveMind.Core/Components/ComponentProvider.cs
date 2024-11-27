using Microsoft.Extensions.DependencyInjection;
using Sels.Core.Extensions.Conversion;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind
{
    /// <summary>
    /// Provider that uses a <see cref="IServiceProvider"/> and <see cref="IComponentFactory{T}"/>(s) to create new named instances.
    /// </summary>
    /// <typeparam name="T">The type of the component to resolve</typeparam>
    public class ComponentProvider<T> : IComponentProvider<T> where T : class
    {

        // Fields
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly IComponentFactory<T>[] _factories;

        /// <inheritdoc cref="ComponentProvider{T}"/>
        /// <param name="serviceProvider">Used by the factories to resolve any dependencies</param>
        /// <param name="factories">Any available factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        public ComponentProvider(IServiceProvider serviceProvider, IEnumerable<IComponentFactory<T>> factories, ILogger<ComponentProvider<T>> logger = null) : this(serviceProvider, factories, logger.CastTo<ILogger>())
        {
            
        }

        /// <inheritdoc cref="ComponentProvider{T}"/>
        /// <param name="serviceProvider">Used by the factories to resolve any dependencies</param>
        /// <param name="factories">Any available factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        protected ComponentProvider(IServiceProvider serviceProvider, IEnumerable<IComponentFactory<T>> factories, ILogger logger = null)
        {
            _serviceProvider = Guard.IsNotNull(serviceProvider);
            _logger = logger;
            _factories = Guard.IsNotNull(factories).ToArray();
        }

        /// <inheritdoc/>
        public virtual async Task<IComponent<T>> CreateAsync(string name, CancellationToken token = default)
        {
            name = Guard.IsNotNullOrWhitespace(name);

            _logger.Log($"Creating new component using implementation <{name}>");

            var factory = _factories.LastOrDefault(x => name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
            if (factory == null) throw new NotSupportedException($"No factory has been registered that is able to create components of type <{typeof(T)}> using implementation <{name}>");

            var scope = _serviceProvider.CreateAsyncScope();
            try
            {
                var component = await factory.CreateAsync(scope.ServiceProvider, token).ConfigureAwait(false) ?? throw new InvalidOperationException($"Factory for components of type <{typeof(T)}> using implementation <{name}> returned null");
                _logger.Log($"Created new component <{component}> using implementation <{name}>");
                return new ScopedComponent<T>(name, component, scope);
            }
            catch (Exception)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }

    /// <summary>
    /// Provider that uses a <see cref="IServiceProvider"/> and <see cref="IComponentFactory{T}"/>(s) to create new named instances using arguments of type <typeparamref name="TArgs"/>.
    /// </summary>
    /// <typeparam name="T">The type of the component to resolve</typeparam>
    /// <typeparam name="TArgs">THe type of arguments that are used to create new instances</typeparam>
    public class ComponentProvider<T, TArgs> : IComponentProvider<T, TArgs> where T : class
    {

        // Fields
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly IComponentFactory<T, TArgs>[] _factories;

        /// <inheritdoc cref="ComponentProvider{T}"/>
        /// <param name="serviceProvider">Used by the factories to resolve any dependencies</param>
        /// <param name="factories">Any available factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        public ComponentProvider(IServiceProvider serviceProvider, IEnumerable<IComponentFactory<T, TArgs>> factories, ILogger<ComponentProvider<T>> logger = null) : this(serviceProvider, factories, logger.CastTo<ILogger>())
        {

        }

        /// <inheritdoc cref="ComponentProvider{T}"/>
        /// <param name="serviceProvider">Used by the factories to resolve any dependencies</param>
        /// <param name="factories">Any available factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        protected ComponentProvider(IServiceProvider serviceProvider, IEnumerable<IComponentFactory<T, TArgs>> factories, ILogger logger = null)
        {
            _serviceProvider = Guard.IsNotNull(serviceProvider);
            _logger = logger;
            _factories = Guard.IsNotNull(factories).ToArray();
        }

        /// <inheritdoc/>
        public virtual async Task<IComponent<T>> CreateAsync(string name, TArgs args, CancellationToken token = default)
        {
            name = Guard.IsNotNullOrWhitespace(name);
            args = Guard.IsNotNull(args);

            _logger.Log($"Creating new component using implementation <{name}> and arguments <{args}>");

            var factory = _factories.LastOrDefault(x => name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
            if (factory == null) throw new NotSupportedException($"No factory has been registered that is able to create components of type <{typeof(T)}> using implementation <{name}>");

            var scope = _serviceProvider.CreateAsyncScope();
            try
            {
                var component = await factory.CreateAsync(scope.ServiceProvider, args, token).ConfigureAwait(false) ?? throw new InvalidOperationException($"Factory for components of type <{typeof(T)}> using implementation <{name}> returned null");
                _logger.Log($"Created new component <{component}> using implementation <{name}>");
                return new ScopedComponent<T>(name, component, scope);
            }
            catch (Exception)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
