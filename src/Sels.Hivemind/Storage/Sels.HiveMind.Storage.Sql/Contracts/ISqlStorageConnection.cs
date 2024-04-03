using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Sels.HiveMind.Storage.Sql
{
    /// <summary>
    /// A <see cref="IStorageConnection"/> opened for an Sql storage.
    /// </summary>
    public interface ISqlStorageConnection : IStorageConnection
    {
        /// <summary>
        /// The connection currently opened to the sql database.
        /// </summary>
        IDbConnection DbConnection { get; }
        /// <summary>
        /// The current transaction for the connection if one was opened.
        /// </summary>
        IDbTransaction DbTransaction { get; }
    }
}
