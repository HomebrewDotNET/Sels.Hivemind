using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Colony;
using Sels.HiveMind.Client.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Builder for selecting what to place a condition on when querying colonies.
    /// </summary>
    public interface IQueryColonyConditionBuilder : IQueryPropertyBuilder<IQueryColonyConditionBuilder>
    {
        /// <summary>
        /// Groups together multiple conditions.
        /// </summary>
        /// <param name="builder">Builder for defining the conditions</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryColonyConditionBuilder> Group(Func<IQueryColonyConditionBuilder, IChainedQueryConditionBuilder<IQueryColonyConditionBuilder>> builder);
        /// <summary>
        /// Adds a condition on the id a background job.
        /// </summary>
        IQueryConditionTextComparisonBuilder<string, IQueryColonyConditionBuilder> Id { get; }
        /// <summary>
        /// Adds a condition on the name of a colony.
        /// </summary>
        IQueryConditionTextComparisonBuilder<string, IQueryColonyConditionBuilder> Name { get; }
        /// <summary>
        /// Adds a condition on the status of a colony.
        /// </summary>
        IQueryConditionComparisonBuilder<ColonyStatus, IQueryColonyConditionBuilder> Status { get; }
        /// <summary>
        /// Adds a condition on the creation date of a colony.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime, IQueryColonyConditionBuilder> CreatedAt { get; }
        /// <summary>
        /// Adds a condition on the last modification date of a colony.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime, IQueryColonyConditionBuilder> ModifiedAt { get; }
        /// <summary>
        /// Adds a condition on the daemons of the colony.
        /// </summary>
        IQueryColonyDaemonConditionBuilder Daemon { get; }
    }
    /// <summary>
    /// Builder for defining conditions on a daemon of a colony.
    /// </summary>
    public interface IQueryColonyDaemonConditionBuilder : IQueryPropertyBuilder<IQueryColonyConditionBuilder>
    {
        /// <summary>
        /// Adds a condition on the name of a daemon.
        /// </summary>
        IQueryConditionTextComparisonBuilder<string, IQueryColonyConditionBuilder> Name { get; }
        /// <summary>
        /// Adds a condition on the status of a daemon.
        /// </summary>
        IQueryConditionComparisonBuilder<ColonyStatus, IQueryColonyConditionBuilder> Status { get; }
        /// <summary>
        /// Adds a condition on the creation date of a daemon.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime, IQueryColonyConditionBuilder> CreatedAt { get; }
        /// <summary>
        /// Adds a condition on the last modification date of a daemon.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime, IQueryColonyConditionBuilder> ModifiedAt { get; }
    }
}
