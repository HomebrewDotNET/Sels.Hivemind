using Dapper;
using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Storage.Sql.Job.Background;
using Sels.HiveMind.Storage.Sql.Templates;
using System.Xml.Linq;

namespace Sels.HiveMind.Storage.Sql
{
    /// <summary>
    /// Contains extension methods for adding dapper parameters.
    /// </summary>
    public static class ParameterExtensions
    {
        /// <summary>
        /// Adds a dapper parameter that maps to the <see cref="BaseLockableTable{T}.LockedBy"/> column.
        /// </summary>
        /// <param name="parameters">The bag to add the parameters to</param>
        /// <param name="locker">The value of the locker</param>
        /// <param name="parameterName">The name of the parameter to add</param>
        public static void AddLocker(this DynamicParameters parameters, string? locker, string parameterName = "locker")
        {
            parameters.ValidateArgument(nameof(parameters));
            parameterName.ValidateArgumentNotNullOrWhitespace(nameof(parameterName));

            parameters.Add(parameterName, locker, System.Data.DbType.String, System.Data.ParameterDirection.Input, 100);
        }

        /// <summary>
        /// Adds a dapper parameter that maps to the <see cref="BasePropertyTable.Name"/> column.
        /// </summary>
        /// <param name="parameters">The bag to add the parameters to</param>
        /// <param name="name">The value of the name</param>
        /// <param name="parameterName">The name of the parameter to add</param>
        public static void AddPropertyName(this DynamicParameters parameters, string? name, string parameterName = "name")
        {
            parameters.ValidateArgument(nameof(parameters));
            parameterName.ValidateArgumentNotNullOrWhitespace(nameof(parameterName));

            parameters.Add(parameterName, name, System.Data.DbType.String, System.Data.ParameterDirection.Input, 100);
        }

        /// <summary>
        /// Adds a dapper parameter that maps to the <see cref="BaseDataTable.Name"/> column.
        /// </summary>
        /// <param name="parameters">The bag to add the parameters to</param>
        /// <param name="name">The value of the name</param>
        /// <param name="parameterName">The name of the parameter to add</param>
        public static void AddDataName(this DynamicParameters parameters, string? name, string parameterName = "name")
        {
            parameters.ValidateArgument(nameof(parameters));
            parameterName.ValidateArgumentNotNullOrWhitespace(nameof(parameterName));

            parameters.Add(parameterName, name, System.Data.DbType.String, System.Data.ParameterDirection.Input, 100);
        }

        /// <summary>
        /// Adds a dapper parameter that maps to the foreign key column that points to the <see cref="BackgroundJobTable"/>.
        /// </summary>
        /// <param name="parameters">The bag to add the parameters to</param>
        /// <param name="recurringJobId">The background job id to add</param>
        /// <param name="parameterName">The name of the parameter to add</param>
        public static void AddBackgroundJobId(this DynamicParameters parameters, long recurringJobId, string parameterName = "backgroundJobId")
        {
            parameters.ValidateArgument(nameof(parameters));
            parameterName.ValidateArgumentNotNullOrWhitespace(nameof(parameterName));

            parameters.Add(parameterName, recurringJobId, System.Data.DbType.Int64, System.Data.ParameterDirection.Input);
        }

        /// <summary>
        /// Adds a dapper parameter that maps to the foreign key column that points to the <see cref="RecurringJobTable"/>.
        /// </summary>
        /// <param name="parameters">The bag to add the parameters to</param>
        /// <param name="recurringJobId">The recurring job id to add</param>
        /// <param name="parameterName">The name of the parameter to add</param>
        public static void AddRecurringJobId(this DynamicParameters parameters, string recurringJobId, string parameterName = "recurringJobId")
        {
            parameters.ValidateArgument(nameof(parameters));
            parameterName.ValidateArgumentNotNullOrWhitespace(nameof(parameterName));

            parameters.Add(parameterName, recurringJobId, System.Data.DbType.String, System.Data.ParameterDirection.Input, 255);
        }

        /// <summary>
        /// Adds a dapper parameters that contains the amount of rows to skip when working with pagination.
        /// </summary>
        /// <param name="parameters">The bag to add the parameters to</param>
        /// <param name="page">The current page to return</param>
        /// <param name="pageSize">The size of the pages</param>
        /// <param name="parameterName">The name of the parameter to add</param>
        public static void AddPage(this DynamicParameters parameters, int page, int pageSize, string parameterName = "page")
        {
            parameters.ValidateArgument(nameof(parameters));
            parameterName.ValidateArgumentNotNullOrWhitespace(nameof(parameterName));
            page.ValidateArgumentLargerOrEqual(nameof(page), 0);
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 0);

            parameters.Add(parameterName, (page - 1) * pageSize, System.Data.DbType.Int32, System.Data.ParameterDirection.Input);
        }

        /// <summary>
        /// Adds a dapper parameters that contains the amount of rows included in a single page when working with pagination.
        /// </summary>
        /// <param name="parameters">The bag to add the parameters to</param>
        /// <param name="pageSize">The size of the pages</param>
        /// <param name="parameterName">The name of the parameter to add</param>
        public static void AddPageSize(this DynamicParameters parameters, int pageSize, string parameterName = "pageSize")
        {
            parameters.ValidateArgument(nameof(parameters));
            parameterName.ValidateArgumentNotNullOrWhitespace(nameof(parameterName));
            pageSize.ValidateArgumentLargerOrEqual(nameof(pageSize), 0);

            parameters.Add(parameterName, pageSize, System.Data.DbType.Int32, System.Data.ParameterDirection.Input);
        }

        /// <summary>
        /// Adds a dapper parameters that contains the maximum amount of rows to return in a query.
        /// </summary>
        /// <param name="parameters">The bag to add the parameters to</param>
        /// <param name="limit">The result size limit</param>
        /// <param name="parameterName">The name of the parameter to add</param>
        public static void AddLimit(this DynamicParameters parameters, int limit, string parameterName = "limit")
        {
            parameters.ValidateArgument(nameof(parameters));
            parameterName.ValidateArgumentNotNullOrWhitespace(nameof(parameterName));
            limit.ValidateArgumentLargerOrEqual(nameof(limit), 0);

            parameters.Add(parameterName, limit, System.Data.DbType.Int32, System.Data.ParameterDirection.Input);
        }
    }
}
