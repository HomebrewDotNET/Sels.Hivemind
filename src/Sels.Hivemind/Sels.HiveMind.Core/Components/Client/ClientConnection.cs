using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
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
        private readonly IComponent<IStorage> _storage;
        private readonly ILogger _logger;
        private List<AsyncAction> _disposeActions;

        // Properties
        /// <summary>
        /// The storage that was used to open the current client connection.
        /// </summary>
        public IStorage Storage => _storage.Component;
        /// <summary>
        /// The connection that was opened from <see cref="Storage"/>.
        /// </summary>
        public IStorageConnection StorageConnection { get; }

        /// <inheritdoc cref="ClientStorageConnection"/>
        /// <param name="storage">The storage the connection is being created for</param>
        /// <param name="connection">The storage connection that was opened</param>
        /// <param name="logger">Optional logger for tracing</param>
        public ClientStorageConnection(IComponent<IStorage> storage, IStorageConnection connection, ILogger logger = null)
        {
            _storage = storage.ValidateArgument(nameof(storage));
            StorageConnection = connection.ValidateArgument(nameof(connection));
            _logger = logger;
        }

        /// <inheritdoc/>
        public string Environment => _storage.Name;
        /// <inheritdoc/>
        public bool HasTransaction => StorageConnection.HasTransaction;
        /// <inheritdoc/>
        public async Task BeginTransactionAsync(CancellationToken token = default) 
        {
            using var methodLogger = _logger.TraceMethod(this);

            await StorageConnection.BeginTransactionAsync(token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public async Task CommitAsync(CancellationToken token = default)
        {
            using var methodLogger = _logger.TraceMethod(this);

            await StorageConnection.CommitAsync(token).ConfigureAwait(false);
        }
        /// <inheritdoc/>
        public async Task AbortTransactionAsync(CancellationToken token = default)
        {
            using var methodLogger = _logger.TraceMethod(this);

            await StorageConnection.AbortTransactionAsync(token).ConfigureAwait(false);
        }

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
            using var methodLogger = _logger.TraceMethod(this);
            var exceptions = new List<Exception>();

            // First close connection
            try
            {
                await StorageConnection.DisposeAsync().ConfigureAwait(false);
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
