using FluentMigrator;
using Sels.Core.Extensions.Fluent;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Queue.MySql.Deployment.Migrations
{
    /// <summary>
    /// Deploys the tables related to HiveMind job queues.
    /// </summary>
    [Migration(1)]
    public class VersionOneJobQueue : AutoReversingMigration
    {
        /// <inheritdoc/>
        public override void Up()
        {
            DeployJobQueueTable(MigrationState.Names.JobQueueTable, true);
            DeployJobQueueTable(MigrationState.Names.BackgroundJobProcessQueueTable, false);
            DeployJobQueueTable(MigrationState.Names.BackgroundJobCleanupQueueTable, false);
            DeployJobQueueTable(MigrationState.Names.RecurringJobTriggerQueueTable, false);
        }

        private void DeployJobQueueTable(string tableName, bool includeType)
        {
            // Table
            if (!Schema.Table(tableName).Exists())
            {
                Create.Table(tableName)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{tableName}")
                        .When(includeType, x => x.WithColumn("Type").AsString(255).NotNullable())
                        .WithColumn("Name").AsString(255).NotNullable()
                        .WithColumn("JobId").AsString(65535).NotNullable()
                        .WithColumn("Priority").AsInt32().NotNullable()
                        .WithColumn("ExecutionId").AsString(36).NotNullable()
                        .WithColumn("ProcessId").AsString(36).Nullable()
                        .WithColumn("QueueTime").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("FetchedAt").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("EnqueuedAt").AsCustom("DateTime(6)").NotNullable();
            }
            // Indexes
            if (includeType)
            {
                if (!Schema.Table(tableName).Index("IX_Type_Name_FetchedAt_Priority_QueueTime").Exists())
                {
                    Create.Index("IX_Type_Name_FetchedAt_Priority_QueueTime").OnTable(tableName)
                            .OnColumn("Type").Ascending()
                            .OnColumn("Name").Ascending()
                            .OnColumn("FetchedAt").Ascending()
                            .OnColumn("Priority").Ascending()
                            .OnColumn("QueueTime").Ascending();
                }

                if (!Schema.Table(tableName).Index("IX_Type_FetchedAt_Priority_QueueTime_Name").Exists())
                {
                    Create.Index("IX_Type_FetchedAt_Priority_QueueTime_Name").OnTable(tableName)
                            .OnColumn("Type").Ascending()
                            .OnColumn("FetchedAt").Ascending()
                            .OnColumn("Priority").Ascending()
                            .OnColumn("QueueTime").Ascending()
                            .OnColumn("Name").Ascending();
                }
                if (!Schema.Table(tableName).Index("IX_FetchedAt").Exists())
                {
                    Create.Index("IX_FetchedAt").OnTable(tableName)
                        .OnColumn("FetchedAt").Ascending();
                }
            }
            else
            {
                if (!Schema.Table(tableName).Index("IX_Name_FetchedAt_Priority_QueueTime").Exists())
                {
                    Create.Index("IX_Name_FetchedAt_Priority_QueueTime").OnTable(tableName)
                            .OnColumn("Name").Ascending()
                            .OnColumn("FetchedAt").Ascending()
                            .OnColumn("Priority").Ascending()
                            .OnColumn("QueueTime").Ascending();
                }

                if (!Schema.Table(tableName).Index("IX_FetchedAt_Priority_QueueTime_Name").Exists())
                {
                    Create.Index("IX_FetchedAt_Priority_QueueTime_Name").OnTable(tableName)
                            .OnColumn("FetchedAt").Ascending()
                            .OnColumn("Priority").Ascending()
                            .OnColumn("QueueTime").Ascending()
                            .OnColumn("Name").Ascending();
                }
            }
        }
    }
}
