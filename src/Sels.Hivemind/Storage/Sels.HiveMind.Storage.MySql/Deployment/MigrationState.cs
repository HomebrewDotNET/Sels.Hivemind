using Sels.HiveMind.Storage.Sql;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.MySql.Deployment
{
    /// <summary>
    /// Used to pass down parameters to migrations.
    /// </summary>
    internal static class MigrationState
    {
        // Fields
        private static string _environment;

        // Properties
        /// <summary>
        /// The environment to deploy to.
        /// </summary>
        public static string Environment { get { return _environment; } set 
            {
                _environment = value;
                TableNames = new TableNames(value);
            } 
        }
        /// <summary>
        /// Contains the names of the tables to deploy.
        /// </summary>
        public static TableNames TableNames { get; private set; }
        /// <summary>
        /// The name of the distributed lock that will be used to synchronize deployments.
        /// </summary>
        public static string DeploymentLockName = "Deployment";
        /// <summary>
        /// How long to wait for the deployment lock before throwing an error.
        /// </summary>
        public static TimeSpan DeploymentLockTimeout = TimeSpan.FromMinutes(5);

        
    }
}
