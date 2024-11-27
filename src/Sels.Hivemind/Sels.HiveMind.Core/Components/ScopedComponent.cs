using Microsoft.Extensions.DependencyInjection;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind
{
    /// <summary>
    /// <inheritdoc cref="IEnvironmentComponent{T}"/> that is scoped to a <see cref="AsyncServiceScope"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ScopedComponent<T> : IComponent<T> where T : class
    {
        // Fields
        private readonly AsyncServiceScope? _scope;
        private readonly bool _canDispose = true;

        // Properties
        /// <inheritdoc/>
        public string Name { get; }
        /// <inheritdoc/>
        public T Component { get; }

        /// <inheritdoc cref="ScopedComponent{T}"/>
        /// <param name="component"><inheritdoc cref="Component"/></param>
        /// <param name="scope">The service scope used to resolve <paramref name="component"/></param>
        /// <param name="canDispose">If <paramref name="component"/> can be disposed by this instance or not. Set to false dispose is handled externally or by <paramref name="scope"/></param>
        public ScopedComponent(string name, T component, AsyncServiceScope? scope, bool canDispose = true)
        {
            Name = Guard.IsNotNullOrWhitespace(name);
            _scope = scope;
            Component = component.ValidateArgument(nameof(component));
            _canDispose = canDispose;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            var exceptions = new List<Exception>();

            // Try dispose component
            if (_canDispose)
            {
                try
                {
                    if (Component is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }
                    else if (Component is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            // Try dispose scope
            try
            {
                if(_scope.HasValue) await _scope.Value.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            if (exceptions.Count > 1) throw new AggregateException(exceptions);
            else if (exceptions.Count > 0) exceptions.First().Rethrow();
        }
    }
}
