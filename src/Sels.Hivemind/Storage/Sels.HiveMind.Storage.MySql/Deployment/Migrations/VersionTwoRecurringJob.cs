using FluentMigrator;
using Sels.HiveMind.Storage.Sql.Job;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.MySql.Deployment.Migrations
{
    /// <summary>
    /// Deploys the tables related to HiveMind recurring jobs.
    /// </summary>
    [Migration(2)]
    public class VersionTwoRecurringJob : AutoReversingMigration
    {
        /// <inheritdoc/>
        public override void Up()
        {
            CreateRecurringJobTables();
            CreateRecurringJobStateTables();
            CreateRecurringJobProcessTables();
        }

        private void CreateRecurringJobTables()
        {
            //// Recurring job
            // Table
            if (!Schema.Table(MigrationState.TableNames.RecurringJobTable).Exists())
            {
                Create.Table(MigrationState.TableNames.RecurringJobTable)
                        .WithColumn("Id").AsString(255).NotNullable()
                            .PrimaryKey($"PK_{MigrationState.TableNames.RecurringJobTable}")
                        .WithColumn("Queue").AsString(255).NotNullable()
                        .WithColumn("Priority").AsInt32().NotNullable()
                        .WithColumn("ExecutionId").AsString(36).NotNullable()
                            .WithDefaultValue(string.Empty)
                        .WithColumn("Schedule").AsCustom("MEDIUMTEXT").Nullable()
                        .WithColumn("InvocationData").AsCustom("MEDIUMTEXT").NotNullable()
                        .WithColumn("MiddlewareData").AsCustom("MEDIUMTEXT").Nullable()
                        .WithColumn("Settings").AsCustom("MEDIUMTEXT").Nullable()
                        .WithColumn("ExecutedAmount").AsInt64().NotNullable()
                            .WithDefaultValue(0L)
                        .WithColumn("LockedBy").AsString(100).Nullable()
                        .WithColumn("LockedAt").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("LockHeartbeat").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("ExpectedExecutionDate").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("LastStartedDate").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("LastCompletedDate").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("ModifiedAt").AsCustom("DateTime(6)").NotNullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.RecurringJobTable).Index("IX_Queue_Priority_CreatedAt").Exists())
            {
                Create.Index("IX_Queue_Priority_CreatedAt").OnTable(MigrationState.TableNames.RecurringJobTable)
                        .OnColumn("Queue").Ascending()
                        .OnColumn("Priority").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobTable).Index("IX_Queue_Priority_ModifiedAt").Exists())
            {
                Create.Index("IX_Queue_Priority_ModifiedAt").OnTable(MigrationState.TableNames.RecurringJobTable)
                        .OnColumn("Queue").Ascending()
                        .OnColumn("Priority").Ascending()
                        .OnColumn("ModifiedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobTable).Index("IX_LockedBy_ModifiedAt").Exists())
            {
                Create.Index("IX_LockedBy_ModifiedAt").OnTable(MigrationState.TableNames.RecurringJobTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("ModifiedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobTable).Index("IX_LockedBy_CreatedAt").Exists())
            {
                Create.Index("IX_LockedBy_CreatedAt").OnTable(MigrationState.TableNames.RecurringJobTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobTable).Index("IX_LockedBy_LockHeartbeat").Exists())
            {
                Create.Index("IX_LockedBy_LockHeartbeat").OnTable(MigrationState.TableNames.RecurringJobTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("LockHeartbeat").Ascending();
            }


            //// Recurring job property
            // Table
            if (!Schema.Table(MigrationState.TableNames.RecurringJobPropertyTable).Exists())
            {
                Create.Table(MigrationState.TableNames.RecurringJobPropertyTable)
                        .WithColumn("RecurringJobId").AsString(255).NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_RecurringJobProperty_RecurringJob", MigrationState.TableNames.RecurringJobTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("Name").AsString(100).Nullable()
                        .WithColumn("Type").AsInt32().NotNullable()
                        .WithColumn("OriginalType").AsCustom("TEXT").Nullable()
                        .WithColumn("TextValue").AsString(255).Nullable()
                        .WithColumn("NumberValue").AsInt64().Nullable()
                        .WithColumn("FloatingNumberValue").AsDouble().Nullable()
                        .WithColumn("DateValue").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("OtherValue").AsCustom("MEDIUMTEXT").Nullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("ModifiedAt").AsCustom("DateTime(6)").NotNullable();

                Create.PrimaryKey($"PK_{MigrationState.TableNames.RecurringJobPropertyTable}").OnTable(MigrationState.TableNames.RecurringJobPropertyTable)
                        .Columns("RecurringJobId", "Name");
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.RecurringJobPropertyTable).Index("IX_TextValue_Name_RecurringJobId").Exists())
            {
                Create.Index("IX_TextValue_Name_RecurringJobId").OnTable(MigrationState.TableNames.RecurringJobPropertyTable)
                        .OnColumn("TextValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("RecurringJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobPropertyTable).Index("IX_NumberValue_Name_RecurringJobId").Exists())
            {
                Create.Index("IX_NumberValue_Name_RecurringJobId").OnTable(MigrationState.TableNames.RecurringJobPropertyTable)
                        .OnColumn("NumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("RecurringJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobPropertyTable).Index("IX_FloatingNumberValue_Name_RecurringJobId").Exists())
            {
                Create.Index("IX_FloatingNumberValue_Name_RecurringJobId").OnTable(MigrationState.TableNames.RecurringJobPropertyTable)
                        .OnColumn("FloatingNumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("RecurringJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobPropertyTable).Index("IX_DateValue_Name_RecurringJobId").Exists())
            {
                Create.Index("IX_DateValue_Name_RecurringJobId").OnTable(MigrationState.TableNames.RecurringJobPropertyTable)
                        .OnColumn("DateValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("RecurringJobId").Ascending();
            }
        }

        private void CreateRecurringJobStateTables()
        {
            //// Recurring job state
            // Table
            if (!Schema.Table(MigrationState.TableNames.RecurringJobStateTable).Exists())
            {
                Create.Table(MigrationState.TableNames.RecurringJobStateTable)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{MigrationState.TableNames.RecurringJobStateTable}")
                        .WithColumn("Name").AsString(100).NotNullable()
                        .WithColumn("Sequence").AsInt64().NotNullable()
                        .WithColumn("OriginalType").AsCustom("TEXT").NotNullable()
                        .WithColumn("RecurringJobId").AsString(255).NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_RecurringJobState_RecurringJob", MigrationState.TableNames.RecurringJobTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("ElectedDate").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("Reason").AsCustom("TEXT").Nullable()
                        .WithColumn("IsCurrent").AsBoolean().NotNullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable();
            }
            // Contraints
            if (!Schema.Table(MigrationState.TableNames.RecurringJobStateTable).Constraint("UQ_RecurringJobId_Sequence").Exists())
            {
                Create.UniqueConstraint("UQ_RecurringJobId_Sequence").OnTable(MigrationState.TableNames.RecurringJobStateTable)
                        .Columns("RecurringJobId", "Sequence");
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.RecurringJobStateTable).Index("IX_RecurringJobId_ElectedDate").Exists())
            {
                Create.Index("IX_RecurringJobId_ElectedDate").OnTable(MigrationState.TableNames.RecurringJobStateTable)
                        .OnColumn("RecurringJobId").Ascending()
                        .OnColumn("ElectedDate").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobStateTable).Index("IX_Name_IsCurrent_ElectedDate_RecurringJobId").Exists())
            {
                Create.Index("IX_Name_IsCurrent_ElectedDate_RecurringJobId").OnTable(MigrationState.TableNames.RecurringJobStateTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("IsCurrent").Ascending()
                        .OnColumn("ElectedDate").Ascending()
                        .OnColumn("RecurringJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobStateTable).Index("IX_IsCurrent_Name_ElectedDate_RecurringJobId").Exists())
            {
                Create.Index("IX_IsCurrent_Name_ElectedDate_RecurringJobId").OnTable(MigrationState.TableNames.RecurringJobStateTable)
                        .OnColumn("IsCurrent").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("ElectedDate").Ascending()
                        .OnColumn("RecurringJobId").Ascending();
            }

            //// Recurring job state property
            // Table
            if (!Schema.Table(MigrationState.TableNames.RecurringJobStatePropertyTable).Exists())
            {
                Create.Table(MigrationState.TableNames.RecurringJobStatePropertyTable)
                        .WithColumn("StateId").AsInt64().NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_RecurringJobStateProperty_RecurringJobState", MigrationState.TableNames.RecurringJobStateTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("Name").AsString(100).Nullable()
                        .WithColumn("Type").AsInt32().NotNullable()
                        .WithColumn("OriginalType").AsCustom("TEXT").Nullable()
                        .WithColumn("TextValue").AsString(255).Nullable()
                        .WithColumn("NumberValue").AsInt64().Nullable()
                        .WithColumn("FloatingNumberValue").AsDouble().Nullable()
                        .WithColumn("DateValue").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("OtherValue").AsCustom("MEDIUMTEXT").Nullable();

                Create.PrimaryKey($"PK_{MigrationState.TableNames.RecurringJobStatePropertyTable}").OnTable(MigrationState.TableNames.RecurringJobStatePropertyTable)
                        .Columns("StateId", "Name");
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.RecurringJobStatePropertyTable).Index("IX_Name").Exists())
            {
                Create.Index("IX_Name").OnTable(MigrationState.TableNames.RecurringJobStatePropertyTable)
                        .OnColumn("Name").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobStatePropertyTable).Index("IX_TextValue_Name_StateId").Exists())
            {
                Create.Index("IX_TextValue_Name_StateId").OnTable(MigrationState.TableNames.RecurringJobStatePropertyTable)
                        .OnColumn("TextValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("StateId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobStatePropertyTable).Index("IX_NumberValue_Name_StateId").Exists())
            {
                Create.Index("IX_NumberValue_Name_StateId").OnTable(MigrationState.TableNames.RecurringJobStatePropertyTable)
                        .OnColumn("NumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("StateId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobStatePropertyTable).Index("IX_FloatingNumberValue_Name_StateId").Exists())
            {
                Create.Index("IX_FloatingNumberValue_Name_StateId").OnTable(MigrationState.TableNames.RecurringJobStatePropertyTable)
                        .OnColumn("FloatingNumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("StateId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobStatePropertyTable).Index("IX_DateValue_Name_StateId").Exists())
            {
                Create.Index("IX_DateValue_Name_StateId").OnTable(MigrationState.TableNames.RecurringJobStatePropertyTable)
                        .OnColumn("DateValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("StateId").Ascending();
            }
        }

        private void CreateRecurringJobProcessTables()
        {
            //// Log
            // Table
            if (!Schema.Table(MigrationState.TableNames.RecurringJobLogTable).Exists())
            {
                Create.Table(MigrationState.TableNames.RecurringJobLogTable)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{MigrationState.TableNames.RecurringJobLogTable}")
                        .WithColumn("RecurringJobId").AsString(255).NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_RecurringJobLog_RecurringJob", MigrationState.TableNames.RecurringJobTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("LogLevel").AsInt32().NotNullable()
                        .WithColumn("Message").AsCustom("LONGTEXT").NotNullable()
                        .WithColumn("ExceptionType").AsString(1024).Nullable()
                        .WithColumn("ExceptionMessage").AsCustom("LONGTEXT").Nullable()
                        .WithColumn("ExceptionStackTrace").AsCustom("LONGTEXT").Nullable()
                        .WithColumn("CreatedAtUtc").AsCustom("DATETIME(6)").Nullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.RecurringJobLogTable).Index("IX_RecurringJobId_LogLevel_CreatedAtUtc").Exists())
            {
                Create.Index("IX_RecurringJobId_LogLevel_CreatedAtUtc").OnTable(MigrationState.TableNames.RecurringJobLogTable)
                        .OnColumn("RecurringJobId").Ascending()
                        .OnColumn("LogLevel").Ascending()
                        .OnColumn("CreatedAtUtc").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.RecurringJobLogTable).Index("IX_RecurringJobId_CreatedAtUtc").Exists())
            {
                Create.Index("IX_RecurringJobId_CreatedAtUtc").OnTable(MigrationState.TableNames.RecurringJobLogTable)
                        .OnColumn("RecurringJobId").Ascending()
                        .OnColumn("CreatedAtUtc").Ascending();
            }

            //// Data
            // Table
            if (!Schema.Table(MigrationState.TableNames.RecurringJobDataTable).Exists())
            {
                Create.Table(MigrationState.TableNames.RecurringJobDataTable)
                        .WithColumn("RecurringJobId").AsString(255).NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_RecurringJobData_RecurringJob", MigrationState.TableNames.RecurringJobTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("Name").AsString(100).Nullable()
                        .WithColumn("Value").AsCustom("LONGTEXT").NotNullable();

                Create.PrimaryKey($"PK_{MigrationState.TableNames.RecurringJobDataTable}").OnTable(MigrationState.TableNames.RecurringJobDataTable)
                        .Columns("RecurringJobId", "Name");
            }

            //// Action
            // Table
            if (!Schema.Table(MigrationState.TableNames.RecurringJobActionTable).Exists())
            {
                Create.Table(MigrationState.TableNames.RecurringJobActionTable)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{MigrationState.TableNames.RecurringJobActionTable}")
                        .WithColumn("RecurringJobId").AsString(255).NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_RecurringJobActionTable_RecurringJob", MigrationState.TableNames.RecurringJobTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("Type").AsCustom("TEXT").NotNullable()
                        .WithColumn("ContextType").AsCustom("TEXT").Nullable()
                        .WithColumn("Context").AsCustom("LONGTEXT").Nullable()
                        .WithColumn("ExecutionId").AsString(36).NotNullable()
                        .WithColumn("ForceExecute").AsBoolean().NotNullable()
                        .WithColumn("Priority").AsByte().NotNullable()
                        .WithColumn("CreatedAtUtc").AsCustom("DATETIME(6)").Nullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.RecurringJobActionTable).Index("IX_RecurringJobId_Priority_CreatedAtUtc").Exists())
            {
                Create.Index("IX_RecurringJobId_Priority_CreatedAtUtc").OnTable(MigrationState.TableNames.RecurringJobActionTable)
                        .OnColumn("RecurringJobId").Ascending()
                        .OnColumn("Priority").Ascending()
                        .OnColumn("CreatedAtUtc").Ascending();
            }
        }
    }
}
