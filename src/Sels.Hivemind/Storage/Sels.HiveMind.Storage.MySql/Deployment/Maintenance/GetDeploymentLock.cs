using FluentMigrator;
using Sels.Core.Conversion.Extensions;
using Sels.SQL.QueryBuilder.Builder;
using Sels.SQL.QueryBuilder.MySQL;
using System;
using System.Collections.Generic;
using System.Text;
using MySqlHelper = Sels.SQL.QueryBuilder.MySQL.MySql;

namespace Sels.HiveMind.Storage.MySql.Deployment.Maintenance
{
    /// <summary>
    /// Gets an exclusive lock during deployment to synchronize multiple instances.
    /// </summary>
    [Maintenance(MigrationStage.BeforeAll)]
    public class GetDeploymentLock : Migration
    {
        /// <inheritdoc/>
        public override void Up()
        {
            var query = MySqlHelper.If().Condition(x => x.GetLock(MigrationState.DeploymentLockName, MigrationState.DeploymentLockTimeout.TotalSeconds).NotEqualTo.Value(1))
                                        .Then(x => x.Signal("Could not get deployment lock"), false).Build(ExpressionCompileOptions.AppendSeparator);
            Execute.Sql(query);
        }

        /// <inheritdoc/>
        public override void Down()
        {
        }
    }
}
