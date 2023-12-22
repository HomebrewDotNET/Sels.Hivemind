using Microsoft.Extensions.DependencyInjection;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Threading;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// A <see cref="IActivator"/> that uses a <see cref="IServiceProvider"/> to activate types.
    /// </summary>
    public class ServiceProviderActivator : IActivator
    {
        /// <inheritdoc/>
        public Task<IActivatorScope> CreateActivatorScope(IServiceProvider serviceProvider)
        {
            serviceProvider.ValidateArgument(nameof(serviceProvider));

            return new ServiceProviderActivatorScope(serviceProvider.CreateAsyncScope()).ToTaskResult<IActivatorScope>();
        }
    }

    /// <summary>
    /// A <see cref="IActivatorScope"/> created by a <see cref="ServiceProviderActivator"/>.
    /// </summary>
    public class ServiceProviderActivatorScope : IActivatorScope
    {
        // Fields
        private readonly List<object> _activatedInstances = new List<object>();
        private readonly AsyncServiceScope _scope;

        /// <inheritdoc cref="ServiceProviderActivatorScope"/>
        /// <param name="scope">The scope that will be used to resolve instances</param>
        public ServiceProviderActivatorScope(AsyncServiceScope scope)
        {
            _scope = scope;
        }

        /// <inheritdoc/>
        public Task<object> Active(Type type)
        {
            type.ValidateArgument(nameof(type));

            // See if we can get instance from provider itself
            var instance = _scope.ServiceProvider.GetService(type);
            if(instance == null)
            {
                // Try and create instance using provider to resolve dependencies
                instance = ActivatorUtilities.CreateInstance(_scope.ServiceProvider, type);

                // Store activated type so we can dispose it later
                _activatedInstances.Add(instance);
            }

            return instance.ToTaskResult();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            var exceptions = new List<Exception>();

            // Dispose manually activated instances
            if (_activatedInstances.HasValue())
            {
                var instances = _activatedInstances.ToArray();
                _activatedInstances.Clear();

                foreach (var instance in instances)
                {
                    try
                    {
                        if(instance is IAsyncDisposable asyncDisposable)
                        {
                            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        }
                        else if (instance is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            }

            // Dispose scope
            try
            {
                await _scope.DisposeAsync().ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                exceptions.Add(ex);
            }

            if (exceptions.HasValue()) throw new AggregateException(exceptions);
        }
    }
}
