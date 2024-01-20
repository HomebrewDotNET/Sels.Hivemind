using FluentMigrator;
using Sels.SQL.QueryBuilder.Builder;
using Sels.SQL.QueryBuilder.MySQL;
using System;
using System.Collections.Generic;
using System.Text;
using MySqlHelper = Sels.SQL.QueryBuilder.MySQL.MySql;

namespace Sels.HiveMind.Storage.MySql.Deployment.Maintenance
{
    /// <summary>
    /// Releases the deployment lock.
    /// </summary>
    [Maintenance(MigrationStage.AfterAll)]
    public class ReleaseDeploymentLock : Migration
    {
        /// <inheritdoc/>
        public override void Up()
        {
            var query = MySqlHelper.Select().ColumnExpression(x => x.ReleaseLock(MigrationState.DeploymentLockName)).Build(ExpressionCompileOptions.AppendSeparator);
            Execute.Sql(query);
        }

        /// <inheritdoc/>
        public override void Down()
        {
        }
    }
}
