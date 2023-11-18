using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.HiveMind.Storage.Job;
using Sels.HiveMind.Storage.Sql.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.MySql.Job
{
    /// <summary>
    /// <inheritdoc cref="BackgroundJobTable"/>
    /// Contains extra properties to handle locking during queries because MySql doesn't support returning in update queries >:(.
    /// </summary>
    public class MySqlBackgroundJobTable : BackgroundJobTable
    {
        /// <summary>
        /// Column that contains a Guid that is generated during queries that lock multiple rows so we can return the rows that were updated.
        /// </summary>
        public string LockProcessId { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="job"/>.
        /// </summary>
        /// <param name="job">The instance to create from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public MySqlBackgroundJobTable(JobStorageData job, HiveMindOptions options, IMemoryCache cache) : base(job, options, cache)
        {
            job.ValidateArgument(nameof(job));

            if(job.Lock == null)
            {
                LockProcessId = null;
            }         
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public MySqlBackgroundJobTable()
        {

        }
    }
}
