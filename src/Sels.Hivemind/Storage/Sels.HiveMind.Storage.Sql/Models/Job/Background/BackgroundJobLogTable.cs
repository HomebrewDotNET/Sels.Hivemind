using Dapper;
using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Xml.Linq;

namespace Sels.HiveMind.Storage.Sql.Job.Background
{
    /// <summary>
    /// Model that maps to the table that contains the processing logs of a background job.
    /// </summary>
    public class BackgroundJobLogTable : LogEntry
    {
        /// <summary>
        /// The primary key of the column.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The id of the background job the log is attached to.
        /// </summary>
        public long BackgroundJobId { get; set; }

        /// <inheritdoc cref="BackgroundJobTable"/>
        public BackgroundJobLogTable()
        {
            
        }

        /// <inheritdoc cref="BackgroundJobTable"/>
        /// <param name="backgroundJobId"><inheritdoc cref="BackgroundJobId"/></param>
        /// <param name="logEntry">The instance to copy the properties from</param>
        public BackgroundJobLogTable(long backgroundJobId, LogEntry logEntry) : base(logEntry)
        {
            BackgroundJobId = backgroundJobId;
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

            parameters.AddBackgroundJobId(BackgroundJobId, $"{nameof(BackgroundJobId)}{index}");
            parameters.Add($"{nameof(LogLevel)}{index}", LogLevel, DbType.Int32, ParameterDirection.Input);
            parameters.Add($"{nameof(Message)}{index}", Message, DbType.String, ParameterDirection.Input, -1);
            parameters.Add($"{nameof(ExceptionType)}{index}", ExceptionType, DbType.String, ParameterDirection.Input, 1024);
            parameters.Add($"{nameof(ExceptionMessage)}{index}", ExceptionMessage, DbType.String, ParameterDirection.Input, -1);
            parameters.Add($"{nameof(ExceptionStackTrace)}{index}", ExceptionStackTrace, DbType.String, ParameterDirection.Input, -1);
            parameters.Add($"{nameof(CreatedAt)}{index}", CreatedAtUtc, DbType.DateTime2, ParameterDirection.Input);
        }
    }
}
