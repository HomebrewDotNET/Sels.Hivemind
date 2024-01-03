using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind.Client;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Templates.Client
{
    /// <summary>
    /// Base class for implementing clients.
    /// </summary>
    public class BaseClient : IClient
    {
        // Fields
        private readonly IStorageProvider _storageProvider;
        /// <summary>
        /// Optional logger for tracing.
        /// </summary>
        protected readonly ILogger _logger;

        /// <inheritdoc cref="BaseClient"/>
        /// <param name="hiveService">Service used to get the storage connections</param>
        /// <param name="logger"><inheritdoc cref="_logger"/></param>
        public BaseClient(IStorageProvider storageProvider, ILogger logger = null)
        {
            _storageProvider = storageProvider.ValidateArgument(nameof(storageProvider));
            _logger = logger;
        }

        /// <summary>
        /// Parses <paramref name="connection"/> as <see cref="ClientStorageConnection"/>.
        /// </summary>
        /// <param name="connection">The connection to parse</param>
        /// <returns>The connection parsed from <paramref name="connection"/></returns>
        /// <exception cref="InvalidOperationException"></exception>
        protected ClientStorageConnection GetClientStorageConnection(IClientConnection connection)
        {
            connection.ValidateArgument(nameof(connection));

            if(connection is ClientStorageConnection clientStorageConnection)
            {
                return clientStorageConnection;
            }

            throw new InvalidOperationException($"Expected connection to be of type <{typeof(ClientStorageConnection)}> but got <{connection}>");
        }

        /// <inheritdoc/>
        public async Task<IClientConnection> OpenConnectionAsync(string environment, bool startTransaction = true, CancellationToken token = default)
        {
            environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));


            IEnvironmentComponent<IStorage> storage = null;
            IStorageConnection storageConnection = null;

            _logger.Log($"Opening new connection to environment <{HiveLog.Environment}>", environment);

            try
            {
                storage = await _storageProvider.GetStorageAsync(environment, token).ConfigureAwait(false);
                storageConnection = await storage.Component.OpenConnectionAsync(startTransaction, token).ConfigureAwait(false);
                return new ClientStorageConnection(storage, storageConnection);
            }
            catch (Exception ex)
            {
                var exceptions = new List<Exception>();

                // Close connection if it exists
                if (storageConnection != null)
                {
                    try
                    {
                        await storageConnection.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception innerEx)
                    {
                        exceptions.Add(innerEx);
                    }
                }
                // Release storage if it exists
                if (storage != null)
                {
                    try
                    {
                        await storage.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception innerEx)
                    {
                        exceptions.Add(innerEx);
                    }
                }

                if (exceptions.HasValue())
                {
                    exceptions.Insert(0, ex);
                    throw new AggregateException($"Client could not open a storage connection to environment <{environment}>", exceptions);
                }
                throw;
            }
        }
    }
}
