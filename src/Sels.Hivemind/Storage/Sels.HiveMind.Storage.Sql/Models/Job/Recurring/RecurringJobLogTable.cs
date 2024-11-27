using Dapper;
using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job.Recurring
{
    /// <summary>
    /// Model that maps to the table that contains the processing logs of a recurring job.
    /// </summary>
    public class RecurringJobLogTable : LogEntry
    {
        /// <summary>
        /// The primary key of the column.
        /// </summary>
        public long Id { get; set; }
        /// <summary>
        /// The id of the recurring job the log is attached to.
        /// </summary>
        public string RecurringJobId { get; set; }

        /// <inheritdoc cref="RecurringJobLogTable"/>
        /// <param name="recurringJobId"><inheritdoc cref="RecurringJobId"/></param>
        /// <param name="logEntry">The instance to copy the properties from</param>
        public RecurringJobLogTable(string recurringJobId, LogEntry logEntry) : base(logEntry)
        {
            RecurringJobId = recurringJobId;
        }

        /// <summary>
        /// Appends the create parameters to <paramref name="parameters"/> to insert the current instance.
        /// </summary>
        /// <param name="parameters">The parameters bag to add the insert parameters in</param>
        /// <param name="index">Unique index for the current number. Used as a suffix for the parameter names</param>
        public virtual void AppendCreateParameters(DynamicParameters parameters, int index)
        {
            parameters.ValidateArgument(nameof(parameters));
            index.ValidateArgumentLargerOrEqual(nameof(index), 0);

            parameters.AddRecurringJobId(RecurringJobId, $"{nameof(RecurringJobId)}{index}");
            parameters.Add($"{nameof(LogLevel)}{index}", LogLevel, DbType.Int32, ParameterDirection.Input);
            parameters.Add($"{nameof(Message)}{index}", Message, DbType.String, ParameterDirection.Input, -1);
            parameters.Add($"{nameof(ExceptionType)}{index}", ExceptionType, DbType.String, ParameterDirection.Input, 65535);
            parameters.Add($"{nameof(ExceptionMessage)}{index}", ExceptionMessage, DbType.String, ParameterDirection.Input, -1);
            parameters.Add($"{nameof(ExceptionStackTrace)}{index}", ExceptionStackTrace, DbType.String, ParameterDirection.Input, -1);
            parameters.Add($"{nameof(CreatedAt)}{index}", CreatedAtUtc, DbType.DateTime2, ParameterDirection.Input);
        }
    }
}
