using Sels.Core.Extensions;
using Sels.HiveMind.Storage.Colony;
using Sels.HiveMind.Storage.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Templates
{
    /// <summary>
    /// Base class that contains common columns used in other tables.
    /// </summary>
    public class BaseTable
    {
        /// <summary>
        /// When the row was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// When the row was last updated.
        /// </summary>
        public DateTime ModifiedAt { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="colony"/>.
        /// </summary>
        /// <param name="colony">The instance to create from</param>
        public BaseTable(JobStorageData colony)
        {
            colony.ValidateArgument(nameof(colony));
            CreatedAt = colony.CreatedAtUtc.ToUniversalTime();
            ModifiedAt = colony.ModifiedAtUtc.ToUniversalTime();
        }

        /// <summary>
        /// Creates an instance from <paramref name="colony"/>.
        /// </summary>
        /// <param name="colony">The instance to create from</param>
        public BaseTable(ColonyStorageData colony)
        {
            colony.ValidateArgument(nameof(colony));
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BaseTable()
        {
            
        }
    }
}
