using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.HiveMind.Client;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sels.Core.Delegates.Async;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Wrapper around a storage connection opened from a client.
    /// </summary>
    public class ClientStorageConnection : IClientConnection
    {
        // Fields
        private readonly object _lock = new object();
        private readonly IEnvironmentComponent<IStorage> _storage;
        private List<AsyncAction> _disposeActions;

        // Properties
        /// <summary>
        /// The storage that was used to open the current client connection.
        /// </summary>
        public IStorage Storage => _storage.Component;
        /// <summary>
        /// The connection that was opened from <see cref="Storage"/>.
        /// </summary>
        public IStorageConnection Connection { get; }

        /// <inheritdoc cref="ClientStorageConnection"/>
        /// <param name="storage">The storage the connection is being created for</param>
        /// <param name="connection">The storage connection that was opened</param>
        public ClientStorageConnection(IEnvironmentComponent<IStorage> storage, IStorageConnection connection)
        {
            _storage = storage.ValidateArgument(nameof(storage));
            Connection = connection.ValidateArgument(nameof(connection));
        }

        /// <inheritdoc/>
        public string Environment => _storage.Environment;
        /// <inheritdoc/>
        public bool HasTransaction => Connection.HasTransaction;
        /// <inheritdoc/>
        public Task BeginTransactionAsync(CancellationToken token = default) => BeginTransactionAsync(token);

        /// <inheritdoc/>
        public Task CommitAsync(CancellationToken token = default) => Connection.CommitAsync(token);

        /// <summary>
        /// Registers <paramref name="action"/> that will be called when the current connection is disposed.
        /// </summary>
        /// <param name="action">The action to call on dispose</param>
        public void OnDispose(AsyncAction action)
        {
            action.ValidateArgument(nameof(action));
            lock (_lock)
            {
                _disposeActions ??= new List<AsyncAction>();
                _disposeActions.Add(action); 
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            var exceptions = new List<Exception>();

            // First close connection
            try
            {
                await Connection.DisposeAsync().ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                exceptions.Add(ex);
            }

            // Release storage
            try
            {
                await _storage.DisposeAsync().ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                exceptions.Add(ex);
            }

            // Call dispose action
            List<AsyncAction> disposeActions;
            lock (_lock)
            {
                disposeActions = _disposeActions;
                _disposeActions = null;
            }
            if (disposeActions.HasValue())
            {
                foreach(var action in disposeActions)
                {
                    try
                    {
                        await action().ConfigureAwait(false);
                    }
                    catch(Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            }

            if (exceptions.HasValue()) throw new AggregateException($"Could not properly close client connection", exceptions);
        }
    }
}
