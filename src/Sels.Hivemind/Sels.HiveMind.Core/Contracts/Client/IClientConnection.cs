using System;
using System.Collections.Generic;
using System.Text;
using static Sels.Core.Delegates.Async;
using System.Threading.Tasks;
using System.Threading;
using Sels.HiveMind.Storage;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Connection opened from a client to interaction with a certain HiveMind environment.
    /// </summary>
    public interface IClientConnection : IAsyncDisposable
    {
        /// <summary>
        /// The name of the environment the connection is created for.
        /// </summary>
        public string Environment { get; }
        /// <summary>
        /// If a transaction is opened.
        /// </summary>
        public bool HasTransaction { get; }
        /// <summary>
        /// The storage connection associated with this connection.
        /// </summary>
        public IStorageConnection  StorageConnection { get; }

        /// <summary>
        /// Begins a transaction for the current connection if one has not been opened yet.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task BeginTransactionAsync(CancellationToken token = default);
        /// <summary>
        /// Commits the current transaction if one is opened.
        /// </summary>
        /// <param name="token">Optional token to cancel the request</param>
        /// <returns>Task containing the execution state</returns>
        Task CommitAsync(CancellationToken token = default);
    }
}
