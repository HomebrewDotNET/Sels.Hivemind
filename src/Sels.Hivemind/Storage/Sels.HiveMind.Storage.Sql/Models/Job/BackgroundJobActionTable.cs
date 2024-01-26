using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Conversion.Extensions;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.DateTimes;
using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job
{
    /// <summary>
    /// Model that maps to the table that contains the pending actions to execute on background jobs.
    /// </summary>
    public class BackgroundJobActionTable
    {
        // Properties
        /// <summary>
        /// The primary key of the column.
        /// </summary>
        public long Id { get; set; }
        /// <summary>
        /// The id of the background job the action is to be executed on.
        /// </summary>
        public long BackgroundJobId { get; set; }
        /// <summary>
        /// Contains the type of the action to execute.
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// Contains the name of the context type if one was provided for the action
        /// </summary>
        public string ContextType { get; set; }
        /// <summary>
        /// Contains the serialized context if one was provided for the action.
        /// </summary>
        public string Context { get; set; }
        /// <summary>
        /// The execution id of background job <see cref="BackgroundJobId"/> when the action was created.
        /// </summary>
        public string ExecutionId { get; set; }
        /// <summary>
        /// Can be used to ignore <see cref="ExecutionId"/> and just execute the action regardless of the execution id.
        /// </summary>
        public bool ForceExecute { get; set; }
        /// <summary>
        /// The priority of the action to determine the execution order. Lower values means the action will be executed sooner.
        /// </summary>
        public byte Priority { get; set; } = byte.MaxValue;
        /// <summary>
        /// When the action was created (in utc).
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <inheritdoc cref="BackgroundJobActionTable"/>
        public BackgroundJobActionTable()
        {
            
        }

        /// <inheritdoc cref="BackgroundJobActionTable"/>
        /// <param name="action">The instance to copy the state from</param>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        public BackgroundJobActionTable(ActionInfo action, HiveMindOptions options, IMemoryCache? cache)
        {
            action.ValidateArgument(nameof(action));
            options.ValidateArgument(nameof(options));

            BackgroundJobId = action.ComponentId.ConvertTo<long>();
            Type = HiveMindHelper.Storage.ConvertToStorageFormat(action.Type, options, cache);
            if(action.Context != null)
            {
                ContextType = HiveMindHelper.Storage.ConvertToStorageFormat(action.Context.GetType(), options, cache);
                Context = HiveMindHelper.Storage.ConvertToStorageFormat(action.Context, options, cache);
            }
            ExecutionId = action.ExecutionId.ToString();
            ForceExecute = action.ForceExecute;
            Priority = action.Priority;
            CreatedAtUtc = action.CreatedAtUtc.ToUniversalTime();
        }

        /// <summary>
        /// Converts the current instance into an instance of <see cref="ActionInfo"/>.
        /// </summary>
        /// <param name="options">The options to use for the conversion</param>
        /// <param name="cache">Optional cache that can be used by type converters</param>
        /// <returns>An instance of <see cref="ActionInfo"/> converted from the current instance</returns>
        public ActionInfo ToAction(HiveMindOptions options, IMemoryCache? cache)
        {
            options.ValidateArgument(nameof(options));

            var action = new ActionInfo();
            action.Id = Id.ToString();
            action.ComponentId = BackgroundJobId.ToString();
            action.Type = HiveMindHelper.Storage.ConvertFromStorageFormat(Type, typeof(Type), options, cache).CastTo<Type>();
            if (Context.HasValue())
            {
                var contextType = HiveMindHelper.Storage.ConvertFromStorageFormat(ContextType, typeof(Type), options, cache).CastTo<Type>();
                action.Context = HiveMindHelper.Storage.ConvertFromStorageFormat(Context, contextType, options, cache);
            }
            action.ExecutionId = ExecutionId.ConvertTo<Guid>();
            action.ForceExecute = ForceExecute;
            action.Priority = Priority;
            action.CreatedAtUtc = CreatedAtUtc.AsUtc();
            return action;
        }
    }
}
