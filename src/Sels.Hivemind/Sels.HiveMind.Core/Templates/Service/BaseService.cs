using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sels.Core.Delegates.Async;

namespace Sels.HiveMind.Templates.Service
{
    /// <summary>
    /// Base class for creating services. COntains common dependencies/methods
    /// </summary>
    public abstract class BaseService
    {
        // Fields
        /// <summary>
        /// Used to access the HiveMind options for each environment.
        /// </summary>
        protected readonly IOptionsMonitor<HiveMindOptions> _options;
        /// <summary>
        /// Used to cache generated expressions.
        /// </summary>
        protected readonly IMemoryCache _cache;
        /// <summary>
        /// Optional logger for tracing.
        /// </summary>
        protected readonly ILogger? _logger;

        /// <inheritdoc cref="BaseService"/>
        /// <param name="options"><inheritdoc cref="_options"/></param>
        /// <param name="cache"><inheritdoc cref="_cache"/></param>
        /// <param name="logger"><inheritdoc cref="_logger"/></param>
        protected BaseService(IOptionsMonitor<HiveMindOptions> options, IMemoryCache cache, ILogger? logger = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _cache = cache.ValidateArgument(nameof(cache));
            _logger = logger;
        }

        /// <summary>
        /// Makes sure <paramref name="action"/> is executed using a transaction started on <paramref name="connection"/>.
        /// </summary>
        /// <param name="connection">The connection to maintain the transaction on</param>
        /// <param name="action">The action to execute in the transaction scope</param>
        /// <param name="token">Optional token to cancel the request</param>
        protected async Task RunTransaction(IStorageConnection connection, AsyncAction action, CancellationToken token)
        {
            connection.ValidateArgument(nameof(connection));
            action.ValidateArgument(nameof(action));

            if (!connection.HasTransaction)
            {
                await connection.BeginTransactionAsync(token).ConfigureAwait(false);

                await action().ConfigureAwait(false);

                await connection.CommitAsync(token).ConfigureAwait(false);
            }
            else
            {
                await action().ConfigureAwait(false);
            }
        }
        /// <summary>
        /// Makes sure <paramref name="action"/> is executed using a transaction started on <paramref name="connection"/>.
        /// </summary>
        /// <param name="connection">The connection to maintain the transaction on</param>
        /// <param name="action">The action to execute in the transaction scope</param>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>The value returned by calling <paramref name="action"/></returns>
        protected async Task<T> RunTransaction<T>(IStorageConnection connection, AsyncFunc<T> action, CancellationToken token)
        {
            connection.ValidateArgument(nameof(connection));
            action.ValidateArgument(nameof(action));

            if (!connection.HasTransaction)
            {
                await connection.BeginTransactionAsync(token).ConfigureAwait(false);

                var result = await action().ConfigureAwait(false);

                await connection.CommitAsync(token).ConfigureAwait(false);
                return result;
            }
            else
            {
                return await action().ConfigureAwait(false);
            }
        }
    }
}
