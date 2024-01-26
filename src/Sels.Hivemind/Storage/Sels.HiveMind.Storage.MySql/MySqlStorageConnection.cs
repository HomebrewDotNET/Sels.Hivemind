using MySqlConnector;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Threading;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using static Sels.Core.Delegates.Async;

namespace Sels.HiveMind.Storage.MySql
{
    /// <inheritdoc cref="IStorageConnection"/>
    public class MySqlStorageConnection : IStorageConnection
    {
        // Fields
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly List<Delegates.Async.AsyncAction<CancellationToken>> _commitActions = new List<Delegates.Async.AsyncAction<CancellationToken>>();
        private readonly List<Delegates.Async.AsyncAction<CancellationToken>> _committedActions = new List<Delegates.Async.AsyncAction<CancellationToken>>();
        private List<AsyncAction> _disposeActions;

        // Properties
        /// <inheritdoc/>
        public IStorage Storage { get; set; }
        /// <inheritdoc/>
        public string Environment { get; }
        /// <inheritdoc/>
        public bool HasTransaction => Transaction != null;
        /// <summary>
        /// The opened connection to the database.
        /// </summary>
        public MySqlConnection Connection { get; private set; }
        /// <summary>
        /// The transaction if one is opened.
        /// </summary>
        public MySqlTransaction Transaction { get; private set; }

        public MySqlStorageConnection(MySqlConnection connection, IStorage storage, string environment)
        {
            Connection = connection.ValidateArgument(nameof(connection));
            Storage = storage.ValidateArgument(nameof(storage));
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }
        /// <inheritdoc/>
        public async Task BeginTransactionAsync(CancellationToken token = default)
        {
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                if (Transaction == null)
                {
                    Transaction = await Connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token).ConfigureAwait(false);
                }
            }
        }
        /// <inheritdoc/>
        public async Task CommitAsync(CancellationToken token = default)
        {
            await using (await _lock.LockAsync(token).ConfigureAwait(false))
            {
                Delegates.Async.AsyncAction<CancellationToken>[] preActions = null;
                Delegates.Async.AsyncAction<CancellationToken>[] postActions = null;

                do
                {
                    lock (_commitActions)
                    {
                        preActions = _commitActions.ToArray();
                        _commitActions.Clear();
                    }

                    foreach (var action in preActions)
                    {
                        await action(token).ConfigureAwait(false);
                    }
                } while (preActions.HasValue());
                

                if (Transaction != null) await Transaction.CommitAsync(token).ConfigureAwait(false);
                Transaction = null;

                do
                {
                    lock (_committedActions)
                    {
                        postActions = _committedActions.ToArray();
                        _committedActions.Clear();
                    }

                    foreach (var action in postActions)
                    {
                        await action(token).ConfigureAwait(false);
                    }
                }
                while (postActions.HasValue());
            }
        }
        /// <inheritdoc/>
        public void OnCommitting(Delegates.Async.AsyncAction<CancellationToken> action)
        {
            action.ValidateArgument(nameof(action));

            lock (_commitActions)
            {
                _commitActions.Add(action);
            }
        }
        /// <inheritdoc/>
        public void OnCommitted(Delegates.Async.AsyncAction<CancellationToken> action)
        {
            action.ValidateArgument(nameof(action));

            lock (_committedActions)
            {
                _committedActions.Add(action);
            }
        }
        /// <inheritdoc/>
        public void OnDispose(AsyncAction action)
        {
            action.ValidateArgument(nameof(action));
            lock (_lock)
            {
                _disposeActions ??= new List<AsyncAction>();
                _disposeActions.Add(action);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await using (await _lock.LockAsync().ConfigureAwait(false))
            {
                var exceptions = new List<Exception>();
                if (Transaction != null)
                {
                    try
                    {
                        await Transaction.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }

                try
                {
                    await Connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
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
                    foreach (var action in disposeActions)
                    {
                        try
                        {
                            await action().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }

                if (exceptions.HasValue()) throw new AggregateException($"Could not properly dispose connection to MySql storage", exceptions);
            }
        }
    }
}
