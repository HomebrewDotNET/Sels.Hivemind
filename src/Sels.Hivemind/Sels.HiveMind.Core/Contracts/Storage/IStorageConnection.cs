using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sels.Core.Delegates.Async;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// An open connection to a storage with Optionally a transaction.
    /// Disposing will close the connection and abort the transaction if one is opened.
    /// </summary>
    public interface IStorageConnection : IAsyncDisposable
    {
        /// <summary>
        /// The storage that was used to open the current connection.
        /// </summary>
        public IStorage Storage { get; }
        /// <summary>
        /// The name of the environment the connection is created for.
        /// </summary>
        public string Environment { get; }
        /// <summary>
        /// If a transaction is opened.
        /// </summary>
        public bool HasTransaction { get; }

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

        /// <summary>
        /// Registers <paramref name="action"/> that will be executed when <see cref="CommitAsync(CancellationToken)"/> is called. <paramref name="action"/> still runs in the transaction.
        /// </summary>
        /// <param name="action">Delegate that will be called when the current transaction is commiting</param>
        /// <exception cref="InvalidOperationException"></exception>
        void OnCommitting(AsyncAction<CancellationToken> action);
        /// <summary>
        /// Registers <paramref name="action"/> that will be executed when <see cref="CommitAsync(CancellationToken)"/> is called successfully.
        /// </summary>
        /// <param name="action">Delegate that will be called when the current transaction is commited</param>
        /// <exception cref="InvalidOperationException"></exception>
        void OnCommitted(AsyncAction<CancellationToken> action);
    }
}
