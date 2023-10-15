using FluentMigrator;
using Sels.HiveMind.Storage.Sql.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.MySql.Deployment.Migrations
{
    /// <summary>
    /// Deploys the tables related to HiveMind background jobs.
    /// </summary>
    [Migration(1)]
    public class VersionOneBackgroundJob : AutoReversingMigration
    {
        /// <inheritdoc/>
        public override void Up()
        {
            //// Background job
            // Table
            if (!Schema.Table(MigrationState.Names.BackgroundJobTable).Exists())
            {
                Create.Table(MigrationState.Names.BackgroundJobTable)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{MigrationState.Names.BackgroundJobTable}")
                        .WithColumn("Queue").AsString(255).NotNullable()
                        .WithColumn("Priority").AsInt32().NotNullable()
                        .WithColumn("ExecutionId").AsString(36).NotNullable()
                        .WithColumn("InvocationData").AsString(65535).NotNullable()
                        .WithColumn("MiddlewareData").AsString(65535).Nullable()
                        .WithColumn("LockProcessId").AsString(36).Nullable()
                        .WithColumn("LockedBy").AsString(100).Nullable()
                        .WithColumn("LockedAt").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("LockHeartbeat").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("ModifiedAt").AsCustom("DateTime(6)").NotNullable();
            }
            // Indexes

            //// Background job property
            // Table
            if (!Schema.Table(MigrationState.Names.BackgroundJobPropertyTable).Exists())
            {
                Create.Table(MigrationState.Names.BackgroundJobPropertyTable)
                        .WithColumn("BackgroundJobId").AsInt64().NotNullable()
                        .WithColumn("Name").AsString(100).Nullable()
                        .WithColumn("Type").AsInt32().NotNullable()
                        .WithColumn("OriginalType").AsString(65535).Nullable()
                        .WithColumn("TextValue").AsString(65535).Nullable()
                        .WithColumn("NumberValue").AsInt64().Nullable()
                        .WithColumn("FloatingNumberValue").AsDouble().Nullable()
                        .WithColumn("DateValue").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("OtherValue").AsString(65535).Nullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("ModifiedAt").AsCustom("DateTime(6)").NotNullable();

                Create.PrimaryKey($"PK_{MigrationState.Names.BackgroundJobPropertyTable}").OnTable(MigrationState.Names.BackgroundJobPropertyTable)
                        .Columns("BackgroundJobId", "Name");
            }
            // Indexes
            //// Background job state
            // Table
            if (!Schema.Table(MigrationState.Names.BackgroundJobStateTable).Exists())
            {
                Create.Table(MigrationState.Names.BackgroundJobStateTable)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{MigrationState.Names.BackgroundJobStateTable}")
                        .WithColumn("Name").AsString(100).NotNullable()
                        .WithColumn("OriginalType").AsString(65535).NotNullable()
                        .WithColumn("BackgroundJobId").AsInt64().NotNullable()
                            .ForeignKey($"FK_{MigrationState.Names.BackgroundJobStateTable}_{MigrationState.Names.BackgroundJobTable}", MigrationState.Names.BackgroundJobTable, "Id")
                        .WithColumn("ElectedDate").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("Reason").AsString(65535).Nullable()
                        .WithColumn("IsCurrent").AsBoolean().NotNullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.Names.BackgroundJobStateTable).Index("IX_IsCurrent").Exists())
            {
                Create.Index("IX_IsCurrent").OnTable(MigrationState.Names.BackgroundJobStateTable)
                        .OnColumn("IsCurrent").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobStateTable).Index("IX_BackgroundJobId_ElectedDate").Exists())
            {
                Create.Index("IX_BackgroundJobId_ElectedDate").OnTable(MigrationState.Names.BackgroundJobStateTable)
                        .OnColumn("BackgroundJobId").Ascending()
                        .OnColumn("ElectedDate").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobStateTable).Index("IX_Name").Exists())
            {
                Create.Index("IX_Name").OnTable(MigrationState.Names.BackgroundJobStateTable)
                        .OnColumn("Name").Ascending();
            }

            //// Background job state property
            // Table
            if (!Schema.Table(MigrationState.Names.BackgroundJobStatePropertyTable).Exists())
            {
                Create.Table(MigrationState.Names.BackgroundJobStatePropertyTable)
                        .WithColumn("StateId").AsInt64().NotNullable()
                        .WithColumn("Name").AsString(100).Nullable()
                        .WithColumn("Type").AsInt32().NotNullable()
                        .WithColumn("OriginalType").AsString(65535).Nullable()
                        .WithColumn("TextValue").AsString(65535).Nullable()
                        .WithColumn("NumberValue").AsInt64().Nullable()
                        .WithColumn("FloatingNumberValue").AsDouble().Nullable()
                        .WithColumn("DateValue").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("OtherValue").AsString(65535).Nullable();

                Create.PrimaryKey($"PK_{MigrationState.Names.BackgroundJobStatePropertyTable}").OnTable(MigrationState.Names.BackgroundJobStatePropertyTable)
                        .Columns("StateId", "Name");
            }
            // Indexes
        }
    }
}
