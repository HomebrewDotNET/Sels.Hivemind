using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Reflection;
using Sels.HiveMind.Client.Query;
using Sels.HiveMind.Job;
using Sels.HiveMind.Query.Job;
using Sels.HiveMind.Queue;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Sels.HiveMind.Client
{
    /// <summary>
    /// Builder for selecting what to place a condition on when querying jobs.
    /// </summary>
    public interface IQueryJobConditionBuilder : IQueryPropertyBuilder<IQueryJobConditionBuilder>
    {
        /// <summary>
        /// Groups together multiple conditions.
        /// </summary>
        /// <param name="builder">Builder for defining the conditions</param>
        /// <returns>Builder for defining more conditions</returns>
        IChainedQueryConditionBuilder<IQueryJobConditionBuilder> Group(Func<IQueryJobConditionBuilder, IChainedQueryConditionBuilder<IQueryJobConditionBuilder>> builder);
        /// <summary>
        /// Adds a condition on the id a job.
        /// </summary>
        IQueryConditionComparisonBuilder<string, IQueryJobConditionBuilder> Id { get; }
        /// <summary>
        /// Adds a condition on the queue of a job.
        /// </summary>
        IQueryConditionTextComparisonBuilder<string, IQueryJobConditionBuilder> Queue { get; }
        /// <summary>
        /// Adds a condition on the creation date of a job.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime, IQueryJobConditionBuilder> CreatedAt { get; }
        /// <summary>
        /// Adds a condition on the last modification date of a job.
        /// </summary>
        IQueryConditionComparisonBuilder<DateTime, IQueryJobConditionBuilder> ModifiedAt { get; }
        /// <summary>
        /// Adds single/multiple conditions on the current state of a job.
        /// </summary>
        /// <param name="stateConditionBuilder">Builder for defining multiple conditions on the current state of a job</param>
        IChainedQueryConditionBuilder<IQueryJobConditionBuilder> CurrentState(Func<IQueryJobMultiStateConditionBuilder, IChainedQueryConditionBuilder<IQueryJobMultiStateConditionBuilder>> stateConditionBuilder);
        /// <summary>
        /// Adds a condition on a past state of a job.
        /// </summary>
        IQueryJobStateConditionBuilder<IQueryJobConditionBuilder> AnyPastState { get; }
        /// <summary>
        /// Adds single/multiple conditions on a single past state of a job.
        /// </summary>
        /// <param name="stateConditionBuilder">Builder for defining multiple conditions on a single past state</param>
        IChainedQueryConditionBuilder<IQueryJobConditionBuilder> PastState(Func<IQueryJobMultiStateConditionBuilder, IChainedQueryConditionBuilder<IQueryJobMultiStateConditionBuilder>> stateConditionBuilder);
    }
    /// <summary>
    /// Builder for defining multiple conditions on a single state of a job.
    /// </summary>
    public interface IQueryJobMultiStateConditionBuilder : IQueryJobStateConditionBuilder<IQueryJobMultiStateConditionBuilder>
    {
    }
    /// <summary>
    /// Builder for defining conditions on a state of a job.
    /// </summary>
    /// <typeparam name="TReturn">THe type to return for the fluent syntax</typeparam>
    public interface IQueryJobStateConditionBuilder<TReturn>
    {
        /// <summary>
        /// Adds a query condition on the state name of a job.
        /// </summary>
        /// <returns>Builder for defining more conditions</returns>
        IQueryConditionTextComparisonBuilder<string, TReturn> Name { get; }
        /// <summary>
        /// Adds a query condition on the elected date on the state of the job.
        /// </summary>
        /// <returns>Builder for defining how to compare the value</returns>
        IQueryConditionComparisonBuilder<DateTime, TReturn> ElectedDate { get; }
    }
}
