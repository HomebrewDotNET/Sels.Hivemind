using Microsoft.Extensions.DependencyInjection;
using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Components
{
    /// <summary>
    /// <inheritdoc cref="IEnvironmentComponent{T}"/> that is scoped to a <see cref="AsyncServiceScope"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ScopedEnvironmentComponent<T> : IEnvironmentComponent<T> where T : class
    {
        // Fields
        private readonly AsyncServiceScope _scope;

        // Properties
        /// <inheritdoc/>
        public string Environment { get; }
        /// <inheritdoc/>
        public T Component { get; }

        /// <inheritdoc cref="ScopedEnvironmentComponent{T}"/>
        /// <param name="environment"><inheritdoc cref="Environment"/></param>
        /// <param name="component"><inheritdoc cref="Component"/></param>
        /// <param name="scope">The service scope used to resolve <paramref name="component"/></param>
        public ScopedEnvironmentComponent(string environment, T component, AsyncServiceScope scope)
        {
            _scope = scope;
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
            Component = component.ValidateArgument(nameof(component));
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => _scope.DisposeAsync();
    }
}
