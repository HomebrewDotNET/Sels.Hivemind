using FluentMigrator;
using System;
using System.Collections.Generic;
using System.Text;

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
            Execute.Sql($"IF GET_LOCK('{MigrationState.DeploymentLockName}', {MigrationState.DeploymentLockTimeout.TotalSeconds}) != 1 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Could not get deployment lock'; END IF;");
        }

        /// <inheritdoc/>
        public override void Down()
        {
        }
    }
}
