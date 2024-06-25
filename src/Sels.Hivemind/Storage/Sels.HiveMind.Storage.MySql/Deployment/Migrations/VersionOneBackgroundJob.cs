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
            CreateBackgroundJobTables();
            CreateBackgroundJobStateTables();
            CreateBackgroundJobProcessTables();
        }

        private void CreateBackgroundJobTables()
        {
            //// Background job
            // Table
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobTable).Exists())
            {
                Create.Table(MigrationState.TableNames.BackgroundJobTable)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{MigrationState.TableNames.BackgroundJobTable}")
                        .WithColumn("Queue").AsString(255).NotNullable()
                        .WithColumn("Priority").AsInt32().NotNullable()
                        .WithColumn("ExecutionId").AsString(36).NotNullable()
                        .WithColumn("InvocationData").AsCustom("MEDIUMTEXT").NotNullable()
                        .WithColumn("MiddlewareData").AsCustom("MEDIUMTEXT").Nullable()
                        .WithColumn("LockedBy").AsString(100).Nullable()
                        .WithColumn("LockedAt").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("LockHeartbeat").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("ModifiedAt").AsCustom("DateTime(6)").NotNullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobTable).Index("IX_Queue_CreatedAt").Exists())
            {
                Create.Index("IX_Queue_CreatedAt").OnTable(MigrationState.TableNames.BackgroundJobTable)
                        .OnColumn("Queue").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobTable).Index("IX_Queue_ModifiedAt").Exists())
            {
                Create.Index("IX_Queue_ModifiedAt").OnTable(MigrationState.TableNames.BackgroundJobTable)
                        .OnColumn("Queue").Ascending()
                        .OnColumn("ModifiedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobTable).Index("IX_CreatedAt").Exists())
            {
                Create.Index("IX_CreatedAt").OnTable(MigrationState.TableNames.BackgroundJobTable)
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobTable).Index("IX_ModifiedAt").Exists())
            {
                Create.Index("IX_ModifiedAt").OnTable(MigrationState.TableNames.BackgroundJobTable)
                        .OnColumn("ModifiedAt").Ascending();
            }          
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobTable).Index("IX_LockedBy_ModifiedAt").Exists())
            {
                Create.Index("IX_LockedBy_ModifiedAt").OnTable(MigrationState.TableNames.BackgroundJobTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("ModifiedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobTable).Index("IX_LockedBy_CreatedAt").Exists())
            {
                Create.Index("IX_LockedBy_CreatedAt").OnTable(MigrationState.TableNames.BackgroundJobTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobTable).Index("IX_LockedBy_LockHeartbeat").Exists())
            {
                Create.Index("IX_LockedBy_LockHeartbeat").OnTable(MigrationState.TableNames.BackgroundJobTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("LockHeartbeat").Ascending();
            }


            //// Background job property
            // Table
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Exists())
            {
                Create.Table(MigrationState.TableNames.BackgroundJobPropertyTable)
                        .WithColumn("BackgroundJobId").AsInt64().NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_BackgroundJobProperty_BackgroundJob", MigrationState.TableNames.BackgroundJobTable, "Id")
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

                Create.PrimaryKey($"PK_{MigrationState.TableNames.BackgroundJobPropertyTable}").OnTable(MigrationState.TableNames.BackgroundJobPropertyTable)
                        .Columns("BackgroundJobId", "Name");
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Index("IX_TextValue_Name_BackgroundJobId").Exists())
            {
                Create.Index("IX_TextValue_Name_BackgroundJobId").OnTable(MigrationState.TableNames.BackgroundJobPropertyTable)
                        .OnColumn("TextValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Index("IX_NumberValue_Name_BackgroundJobId").Exists())
            {
                Create.Index("IX_NumberValue_Name_BackgroundJobId").OnTable(MigrationState.TableNames.BackgroundJobPropertyTable)
                        .OnColumn("NumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Index("IX_FloatingNumberValue_Name_BackgroundJobId").Exists())
            {
                Create.Index("IX_FloatingNumberValue_Name_BackgroundJobId").OnTable(MigrationState.TableNames.BackgroundJobPropertyTable)
                        .OnColumn("FloatingNumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Index("IX_DateValue_Name_BackgroundJobId").Exists())
            {
                Create.Index("IX_DateValue_Name_BackgroundJobId").OnTable(MigrationState.TableNames.BackgroundJobPropertyTable)
                        .OnColumn("DateValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Index("IX_Name_TextValue_BackgroundJobId").Exists())
            {
                Create.Index("IX_Name_TextValue_BackgroundJobId").OnTable(MigrationState.TableNames.BackgroundJobPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("TextValue").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Index("IX_Name_NumberValue_BackgroundJobId").Exists())
            {
                Create.Index("IX_Name_NumberValue_BackgroundJobId").OnTable(MigrationState.TableNames.BackgroundJobPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("NumberValue").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Index("IX_Name_FloatingNumberValue_BackgroundJobId").Exists())
            {
                Create.Index("IX_Name_FloatingNumberValue_BackgroundJobId").OnTable(MigrationState.TableNames.BackgroundJobPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("FloatingNumberValue").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Index("IX_Name_DateValue_BackgroundJobId").Exists())
            {
                Create.Index("IX_Name_DateValue_BackgroundJobId").OnTable(MigrationState.TableNames.BackgroundJobPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("DateValue").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
        }

        private void CreateBackgroundJobStateTables()
        {
            //// Background job state
            // Table
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobStateTable).Exists())
            {
                Create.Table(MigrationState.TableNames.BackgroundJobStateTable)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{MigrationState.TableNames.BackgroundJobStateTable}")
                        .WithColumn("Name").AsString(100).NotNullable()
                        .WithColumn("Sequence").AsInt64().NotNullable()
                        .WithColumn("OriginalType").AsCustom("TEXT").NotNullable()
                        .WithColumn("BackgroundJobId").AsInt64().NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_BackgroundJobState_BackgroundJob", MigrationState.TableNames.BackgroundJobTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("ElectedDate").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("Reason").AsCustom("TEXT").Nullable()
                        .WithColumn("IsCurrent").AsBoolean().NotNullable()
                        .WithColumn("Data").AsCustom("MEDIUMTEXT").Nullable();
            }
            // Contraints
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobStateTable).Constraint("UQ_BackgroundJobId_Sequence").Exists())
            {
                Create.UniqueConstraint("UQ_BackgroundJobId_Sequence").OnTable(MigrationState.TableNames.BackgroundJobStateTable)
                        .Columns("BackgroundJobId", "Sequence");
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobStateTable).Index("IX_BackgroundJobId_ElectedDate").Exists())
            {
                Create.Index("IX_BackgroundJobId_ElectedDate").OnTable(MigrationState.TableNames.BackgroundJobStateTable)
                        .OnColumn("BackgroundJobId").Ascending()
                        .OnColumn("ElectedDate").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobStateTable).Index("IX_Name_IsCurrent_ElectedDate_BackgroundJobId").Exists())
            {
                Create.Index("IX_Name_IsCurrent_ElectedDate_BackgroundJobId").OnTable(MigrationState.TableNames.BackgroundJobStateTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("IsCurrent").Ascending()
                        .OnColumn("ElectedDate").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobStateTable).Index("IX_IsCurrent_ElectedDate_BackgroundJobId").Exists())
            {
                Create.Index("IX_IsCurrent_ElectedDate_BackgroundJobId").OnTable(MigrationState.TableNames.BackgroundJobStateTable)
                        .OnColumn("IsCurrent").Ascending()
                        .OnColumn("ElectedDate").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
        }

        private void CreateBackgroundJobProcessTables()
        {
            //// Log
            // Table
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobLogTable).Exists())
            {
                Create.Table(MigrationState.TableNames.BackgroundJobLogTable)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{MigrationState.TableNames.BackgroundJobLogTable}")
                        .WithColumn("BackgroundJobId").AsInt64().NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_BackgroundJobLog_BackgroundJob", MigrationState.TableNames.BackgroundJobTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("LogLevel").AsInt32().NotNullable()
                        .WithColumn("Message").AsCustom("LONGTEXT").NotNullable()
                        .WithColumn("ExceptionType").AsString(1024).Nullable()
                        .WithColumn("ExceptionMessage").AsCustom("LONGTEXT").Nullable()
                        .WithColumn("ExceptionStackTrace").AsCustom("LONGTEXT").Nullable()
                        .WithColumn("CreatedAt").AsCustom("DATETIME(6)").Nullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobLogTable).Index("IX_BackgroundJobId_LogLevel_CreatedAt").Exists())
            {
                Create.Index("IX_BackgroundJobId_LogLevel_CreatedAt").OnTable(MigrationState.TableNames.BackgroundJobLogTable)
                        .OnColumn("BackgroundJobId").Ascending()
                        .OnColumn("LogLevel").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobLogTable).Index("IX_BackgroundJobId_CreatedAt").Exists())
            {
                Create.Index("IX_BackgroundJobId_CreatedAt").OnTable(MigrationState.TableNames.BackgroundJobLogTable)
                        .OnColumn("BackgroundJobId").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }

            //// Data
            // Table
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobDataTable).Exists())
            {
                Create.Table(MigrationState.TableNames.BackgroundJobDataTable)
                        .WithColumn("BackgroundJobId").AsInt64().NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_BackgroundJobData_BackgroundJob", MigrationState.TableNames.BackgroundJobTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("Name").AsString(100).Nullable()
                        .WithColumn("Value").AsCustom("LONGTEXT").NotNullable();

                Create.PrimaryKey($"PK_{MigrationState.TableNames.BackgroundJobDataTable}").OnTable(MigrationState.TableNames.BackgroundJobDataTable)
                        .Columns("BackgroundJobId", "Name");
            }

            //// Action
            // Table
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobActionTable).Exists())
            {
                Create.Table(MigrationState.TableNames.BackgroundJobActionTable)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{MigrationState.TableNames.BackgroundJobActionTable}")
                        .WithColumn("BackgroundJobId").AsInt64().NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_BackgroundJobActionTable_BackgroundJob", MigrationState.TableNames.BackgroundJobTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("Type").AsCustom("TEXT").NotNullable()
                        .WithColumn("ContextType").AsCustom("TEXT").Nullable()
                        .WithColumn("Context").AsCustom("LONGTEXT").Nullable()
                        .WithColumn("ExecutionId").AsString(36).NotNullable()
                        .WithColumn("ForceExecute").AsBoolean().NotNullable()
                        .WithColumn("Priority").AsByte().NotNullable()
                        .WithColumn("CreatedAt").AsCustom("DATETIME(6)").Nullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobActionTable).Index("IX_BackgroundJobId_Priority_CreatedAt").Exists())
            {
                Create.Index("IX_BackgroundJobId_Priority_CreatedAt").OnTable(MigrationState.TableNames.BackgroundJobActionTable)
                        .OnColumn("BackgroundJobId").Ascending()
                        .OnColumn("Priority").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
        }
    }
}
