﻿using FluentMigrator;
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
                        .WithColumn("InvocationData").AsCustom("MEDIUMTEXT").NotNullable()
                        .WithColumn("MiddlewareData").AsCustom("MEDIUMTEXT").Nullable()
                        .WithColumn("LockProcessId").AsString(36).Nullable()
                        .WithColumn("LockedBy").AsString(100).Nullable()
                        .WithColumn("LockedAt").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("LockHeartbeat").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("ModifiedAt").AsCustom("DateTime(6)").NotNullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.Names.BackgroundJobTable).Index("IX_Queue_Priority_CreatedAt").Exists())
            {
                Create.Index("IX_Queue_Priority_CreatedAt").OnTable(MigrationState.Names.BackgroundJobTable)
                        .OnColumn("Queue").Ascending()
                        .OnColumn("Priority").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobTable).Index("IX_Queue_Priority_ModifiedAt").Exists())
            {
                Create.Index("IX_Queue_Priority_ModifiedAt").OnTable(MigrationState.Names.BackgroundJobTable)
                        .OnColumn("Queue").Ascending()
                        .OnColumn("Priority").Ascending()
                        .OnColumn("ModifiedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobTable).Index("IX_LockedBy_ModifiedAt").Exists())
            {
                Create.Index("IX_LockedBy_ModifiedAt").OnTable(MigrationState.Names.BackgroundJobTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("ModifiedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobTable).Index("IX_LockedBy_CreatedAt").Exists())
            {
                Create.Index("IX_LockedBy_CreatedAt").OnTable(MigrationState.Names.BackgroundJobTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobTable).Index("IX_ExecutionId").Exists())
            {
                Create.Index("IX_ExecutionId").OnTable(MigrationState.Names.BackgroundJobTable)
                        .OnColumn("ExecutionId").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobTable).Index("IX_LockedBy_LockHeartbeat").Exists())
            {
                Create.Index("IX_LockedBy_LockHeartbeat").OnTable(MigrationState.Names.BackgroundJobTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("LockHeartbeat").Ascending();
            }


            //// Background job property
            // Table
            if (!Schema.Table(MigrationState.Names.BackgroundJobPropertyTable).Exists())
            {
                Create.Table(MigrationState.Names.BackgroundJobPropertyTable)
                        .WithColumn("BackgroundJobId").AsInt64().NotNullable()
                        .WithColumn("Name").AsString(100).Nullable()
                        .WithColumn("Type").AsInt32().NotNullable()
                        .WithColumn("OriginalType").AsCustom("TEXT").Nullable()
                        .WithColumn("TextValue").AsString(700).Nullable()
                        .WithColumn("NumberValue").AsInt64().Nullable()
                        .WithColumn("FloatingNumberValue").AsDouble().Nullable()
                        .WithColumn("DateValue").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("OtherValue").AsCustom("MEDIUMTEXT").Nullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("ModifiedAt").AsCustom("DateTime(6)").NotNullable();

                Create.PrimaryKey($"PK_{MigrationState.Names.BackgroundJobPropertyTable}").OnTable(MigrationState.Names.BackgroundJobPropertyTable)
                        .Columns("BackgroundJobId", "Name");
            }
            // Indexes
            if (!Schema.Table(MigrationState.Names.BackgroundJobPropertyTable).Index("IX_TextValue_Name_BackgroundJobId").Exists())
            {
                Create.Index("IX_TextValue_Name_BackgroundJobId").OnTable(MigrationState.Names.BackgroundJobPropertyTable)
                        .OnColumn("TextValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobPropertyTable).Index("IX_NumberValue_Name_BackgroundJobId").Exists())
            {
                Create.Index("IX_NumberValue_Name_BackgroundJobId").OnTable(MigrationState.Names.BackgroundJobPropertyTable)
                        .OnColumn("NumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobPropertyTable).Index("IX_FloatingNumberValue_Name_BackgroundJobId").Exists())
            {
                Create.Index("IX_FloatingNumberValue_Name_BackgroundJobId").OnTable(MigrationState.Names.BackgroundJobPropertyTable)
                        .OnColumn("FloatingNumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobPropertyTable).Index("IX_DateValue_Name_BackgroundJobId").Exists())
            {
                Create.Index("IX_DateValue_Name_BackgroundJobId").OnTable(MigrationState.Names.BackgroundJobPropertyTable)
                        .OnColumn("DateValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }

            //// Background job state
            // Table
            if (!Schema.Table(MigrationState.Names.BackgroundJobStateTable).Exists())
            {
                Create.Table(MigrationState.Names.BackgroundJobStateTable)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{MigrationState.Names.BackgroundJobStateTable}")
                        .WithColumn("Name").AsString(100).NotNullable()
                        .WithColumn("OriginalType").AsCustom("TEXT").NotNullable()
                        .WithColumn("BackgroundJobId").AsInt64().NotNullable()
                            .ForeignKey($"FK_{MigrationState.Names.BackgroundJobStateTable}_{MigrationState.Names.BackgroundJobTable}", MigrationState.Names.BackgroundJobTable, "Id")
                        .WithColumn("ElectedDate").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("Reason").AsString(700).Nullable()
                        .WithColumn("IsCurrent").AsBoolean().NotNullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.Names.BackgroundJobStateTable).Index("IX_Name_IsCurrent_ElectedDate_BackgroundJobId").Exists())
            {
                Create.Index("IX_Name_IsCurrent_ElectedDate_BackgroundJobId").OnTable(MigrationState.Names.BackgroundJobStateTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("IsCurrent").Ascending()
                        .OnColumn("ElectedDate").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobStateTable).Index("IX_Name_IsCurrent_BackgroundJobId").Exists())
            {
                Create.Index("IX_Name_IsCurrent_BackgroundJobId").OnTable(MigrationState.Names.BackgroundJobStateTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("IsCurrent").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobStateTable).Index("IX_BackgroundJobId_ElectedDate_IsCurrent").Exists())
            {
                Create.Index("IX_BackgroundJobId_ElectedDate_IsCurrent").OnTable(MigrationState.Names.BackgroundJobStateTable)
                        .OnColumn("BackgroundJobId").Ascending()
                        .OnColumn("IsCurrent").Ascending()
                        .OnColumn("ElectedDate").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobStateTable).Index("IX_Reason_IsCurrent_ElectedDate_BackgroundJobId").Exists())
            {
                Create.Index("IX_Reason_IsCurrent_ElectedDate_BackgroundJobId").OnTable(MigrationState.Names.BackgroundJobStateTable)
                        .OnColumn("Reason").Ascending()
                        .OnColumn("IsCurrent").Ascending()
                        .OnColumn("ElectedDate").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobStateTable).Index("IX_Reason_IsCurrent_BackgroundJobId").Exists())
            {
                Create.Index("IX_Reason_IsCurrent_BackgroundJobId").OnTable(MigrationState.Names.BackgroundJobStateTable)
                        .OnColumn("Reason").Ascending()
                        .OnColumn("IsCurrent").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }

            //// Background job state property
            // Table
            if (!Schema.Table(MigrationState.Names.BackgroundJobStatePropertyTable).Exists())
            {
                Create.Table(MigrationState.Names.BackgroundJobStatePropertyTable)
                        .WithColumn("StateId").AsInt64().NotNullable()
                        .WithColumn("Name").AsString(100).Nullable()
                        .WithColumn("Type").AsInt32().NotNullable()
                        .WithColumn("OriginalType").AsCustom("TEXT").Nullable()
                        .WithColumn("TextValue").AsString(700).Nullable()
                        .WithColumn("NumberValue").AsInt64().Nullable()
                        .WithColumn("FloatingNumberValue").AsDouble().Nullable()
                        .WithColumn("DateValue").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("OtherValue").AsCustom("MEDIUMTEXT").Nullable();

                Create.PrimaryKey($"PK_{MigrationState.Names.BackgroundJobStatePropertyTable}").OnTable(MigrationState.Names.BackgroundJobStatePropertyTable)
                        .Columns("StateId", "Name");
            }
            // Indexes
            if (!Schema.Table(MigrationState.Names.BackgroundJobStatePropertyTable).Index("IX_TextValue_Name_StateId").Exists())
            {
                Create.Index("IX_TextValue_Name_StateId").OnTable(MigrationState.Names.BackgroundJobStatePropertyTable)
                        .OnColumn("TextValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("StateId").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobStatePropertyTable).Index("IX_NumberValue_Name_StateId").Exists())
            {
                Create.Index("IX_NumberValue_Name_StateId").OnTable(MigrationState.Names.BackgroundJobStatePropertyTable)
                        .OnColumn("NumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("StateId").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobStatePropertyTable).Index("IX_FloatingNumberValue_Name_StateId").Exists())
            {
                Create.Index("IX_FloatingNumberValue_Name_StateId").OnTable(MigrationState.Names.BackgroundJobStatePropertyTable)
                        .OnColumn("FloatingNumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("StateId").Ascending();
            }
            if (!Schema.Table(MigrationState.Names.BackgroundJobStatePropertyTable).Index("IX_DateValue_Name_StateId").Exists())
            {
                Create.Index("IX_DateValue_Name_StateId").OnTable(MigrationState.Names.BackgroundJobStatePropertyTable)
                        .OnColumn("DateValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("StateId").Ascending();
            }
        }
    }
}
