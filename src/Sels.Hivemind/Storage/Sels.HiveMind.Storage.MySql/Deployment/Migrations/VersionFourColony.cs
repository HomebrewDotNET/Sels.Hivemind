using FluentMigrator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Storage.MySql.Deployment.Migrations
{
    /// <summary>
    /// Deploys the tables related to HiveMind colonies.
    /// </summary>
    [Migration(4)]
    public class VersionFourColony : AutoReversingMigration
    {
        /// <inheritdoc/>
        public override void Up()
        {
            //// Colony
            // Table
            if (!Schema.Table(MigrationState.TableNames.ColonyTable).Exists())
            {
                Create.Table(MigrationState.TableNames.ColonyTable)
                        .WithColumn("Id").AsString(255).NotNullable()
                            .PrimaryKey($"PK_{MigrationState.TableNames.ColonyTable}")
                        .WithColumn("Name").AsString(255).NotNullable()
                        .WithColumn("Status").AsInt32().NotNullable()
                        .WithColumn("Options").AsCustom("MEDIUMTEXT").Nullable()
                        .WithColumn("LockedBy").AsString(100).Nullable()
                        .WithColumn("LockedAt").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("LockHeartbeat").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("ModifiedAt").AsCustom("DateTime(6)").NotNullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.ColonyTable).Index("IX_Status").Exists())
            {
                Create.Index("IX_Status").OnTable(MigrationState.TableNames.ColonyTable)
                        .OnColumn("Status").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyTable).Index("IX_CreatedAt").Exists())
            {
                Create.Index("IX_CreatedAt").OnTable(MigrationState.TableNames.ColonyTable)
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyTable).Index("IX_ModifiedAt").Exists())
            {
                Create.Index("IX_ModifiedAt").OnTable(MigrationState.TableNames.ColonyTable)
                        .OnColumn("ModifiedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyTable).Index("IX_LockedAt").Exists())
            {
                Create.Index("IX_LockedAt").OnTable(MigrationState.TableNames.ColonyTable)
                        .OnColumn("LockedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyTable).Index("IX_LockedBy_ModifiedAt").Exists())
            {
                Create.Index("IX_LockedBy_ModifiedAt").OnTable(MigrationState.TableNames.ColonyTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("ModifiedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyTable).Index("IX_LockedBy_CreatedAt").Exists())
            {
                Create.Index("IX_LockedBy_CreatedAt").OnTable(MigrationState.TableNames.ColonyTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyTable).Index("IX_LockedBy_LockHeartbeat").Exists())
            {
                Create.Index("IX_LockedBy_LockHeartbeat").OnTable(MigrationState.TableNames.ColonyTable)
                        .OnColumn("LockedBy").Ascending()
                        .OnColumn("LockHeartbeat").Ascending();
            }
            //// Colony Property
            // Table
            if (!Schema.Table(MigrationState.TableNames.ColonyPropertyTable).Exists())
            {
                Create.Table(MigrationState.TableNames.ColonyPropertyTable)
                        .WithColumn("ColonyId").AsString(255).NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_ColonyPropertyTable_Colony", MigrationState.TableNames.ColonyTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("Name").AsString(255).Nullable()
                        .WithColumn("Type").AsInt32().NotNullable()
                        .WithColumn("OriginalType").AsCustom("TEXT").Nullable()
                        .WithColumn("TextValue").AsString(255).Nullable()
                        .WithColumn("NumberValue").AsInt64().Nullable()
                        .WithColumn("FloatingNumberValue").AsDouble().Nullable()
                        .WithColumn("DateValue").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("BooleanValue").AsBoolean().Nullable()
                        .WithColumn("OtherValue").AsCustom("MEDIUMTEXT").Nullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("ModifiedAt").AsCustom("DateTime(6)").NotNullable();

                Create.PrimaryKey($"PK_{MigrationState.TableNames.ColonyPropertyTable}").OnTable(MigrationState.TableNames.ColonyPropertyTable)
                        .Columns("ColonyId", "Name");
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.ColonyPropertyTable).Index("IX_TextValue_Name_ColonyId").Exists())
            {
                Create.Index("IX_TextValue_Name_ColonyId").OnTable(MigrationState.TableNames.ColonyPropertyTable)
                        .OnColumn("TextValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyPropertyTable).Index("IX_NumberValue_Name_ColonyId").Exists())
            {
                Create.Index("IX_NumberValue_Name_ColonyId").OnTable(MigrationState.TableNames.ColonyPropertyTable)
                        .OnColumn("NumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyPropertyTable).Index("IX_FloatingNumberValue_Name_ColonyId").Exists())
            {
                Create.Index("IX_FloatingNumberValue_Name_ColonyId").OnTable(MigrationState.TableNames.ColonyPropertyTable)
                        .OnColumn("FloatingNumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyPropertyTable).Index("IX_DateValue_Name_ColonyId").Exists())
            {
                Create.Index("IX_DateValue_Name_ColonyId").OnTable(MigrationState.TableNames.ColonyPropertyTable)
                        .OnColumn("DateValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyPropertyTable).Index("IX_Name_TextValue_ColonyId").Exists())
            {
                Create.Index("IX_Name_TextValue_ColonyId").OnTable(MigrationState.TableNames.ColonyPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("TextValue").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyPropertyTable).Index("IX_Name_NumberValue_ColonyId").Exists())
            {
                Create.Index("IX_Name_NumberValue_ColonyId").OnTable(MigrationState.TableNames.ColonyPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("NumberValue").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyPropertyTable).Index("IX_Name_FloatingNumberValue_ColonyId").Exists())
            {
                Create.Index("IX_Name_FloatingNumberValue_ColonyId").OnTable(MigrationState.TableNames.ColonyPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("FloatingNumberValue").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyPropertyTable).Index("IX_Name_DateValue_ColonyId").Exists())
            {
                Create.Index("IX_Name_DateValue_ColonyId").OnTable(MigrationState.TableNames.ColonyPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("DateValue").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyPropertyTable).Index("IX_Name_BooleanValue_ColonyId").Exists())
            {
                Create.Index("IX_Name_BooleanValue_ColonyId").OnTable(MigrationState.TableNames.ColonyPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("BooleanValue").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }

            //// Daemon
            // Table
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonTable).Exists())
            {
                Create.Table(MigrationState.TableNames.ColonyDaemonTable)
                        .WithColumn("ColonyId").AsString(255).NotNullable()
                            .ForeignKey($"FK_{MigrationState.Environment}_ColonyDaemonTable_Colony", MigrationState.TableNames.ColonyTable, "Id")
                                .OnDeleteOrUpdate(System.Data.Rule.Cascade)
                        .WithColumn("Name").AsString(255).NotNullable()
                        .WithColumn("Priority").AsByte().NotNullable()
                        .WithColumn("Status").AsInt32().NotNullable()
                        .WithColumn("OriginalInstanceTypeName").AsString(255).Nullable()
                        .WithColumn("StateTypeName").AsString(255).Nullable()
                        .WithColumn("StateStorageValue").AsCustom("MEDIUMTEXT").Nullable()
                        .WithColumn("RestartPolicy").AsInt32().NotNullable()
                        .WithColumn("EnabledLogLevel").AsInt32().NotNullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("ModifiedAt").AsCustom("DateTime(6)").NotNullable();

                Create.PrimaryKey($"PK_{MigrationState.TableNames.ColonyDaemonTable}").OnTable(MigrationState.TableNames.ColonyDaemonTable)
                        .Columns("ColonyId", "Name");
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonTable).Index("IX_Priority").Exists())
            {
                Create.Index("IX_Priority").OnTable(MigrationState.TableNames.ColonyDaemonTable)
                        .OnColumn("Priority").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonTable).Index("IX_Status").Exists())
            {
                Create.Index("IX_Status").OnTable(MigrationState.TableNames.ColonyDaemonTable)
                        .OnColumn("Status").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonTable).Index("IX_CreatedAt").Exists())
            {
                Create.Index("IX_CreatedAt").OnTable(MigrationState.TableNames.ColonyDaemonTable)
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonTable).Index("IX_ModifiedAt").Exists())
            {
                Create.Index("IX_ModifiedAt").OnTable(MigrationState.TableNames.ColonyDaemonTable)
                        .OnColumn("ModifiedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonTable).Index("IX_Name").Exists())
            {
                Create.Index("IX_Name").OnTable(MigrationState.TableNames.ColonyDaemonTable)
                        .OnColumn("Name").Ascending();
            }
            //// Daemon Property
            // Table
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonPropertyTable).Exists())
            {
                Create.Table(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .WithColumn("ColonyId").AsString(255).NotNullable()
                        .WithColumn("DaemonName").AsString(255).NotNullable()
                        .WithColumn("Name").AsString(100).Nullable()
                        .WithColumn("Type").AsInt32().NotNullable()
                        .WithColumn("OriginalType").AsCustom("TEXT").Nullable()
                        .WithColumn("TextValue").AsString(255).Nullable()
                        .WithColumn("NumberValue").AsInt64().Nullable()
                        .WithColumn("FloatingNumberValue").AsDouble().Nullable()
                        .WithColumn("DateValue").AsCustom("DateTime(6)").Nullable()
                        .WithColumn("BooleanValue").AsBoolean().Nullable()
                        .WithColumn("OtherValue").AsCustom("MEDIUMTEXT").Nullable()
                        .WithColumn("CreatedAt").AsCustom("DateTime(6)").NotNullable()
                        .WithColumn("ModifiedAt").AsCustom("DateTime(6)").NotNullable();

                Create.PrimaryKey($"PK_{MigrationState.TableNames.ColonyDaemonPropertyTable}").OnTable(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .Columns("ColonyId", "DaemonName", "Name");
                Create.ForeignKey($"FK_{MigrationState.Environment}_ColonyDaemonPropertyTable_ColonyDaemon").FromTable(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .ForeignColumns("ColonyId", "DaemonName")
                        .ToTable(MigrationState.TableNames.ColonyDaemonTable).PrimaryColumns("ColonyId", "Name")
                        .OnDeleteOrUpdate(System.Data.Rule.Cascade);
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonPropertyTable).Index("IX_TextValue_Name_DaemonName_ColonyId").Exists())
            {
                Create.Index("IX_TextValue_Name_DaemonName_ColonyId").OnTable(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .OnColumn("TextValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("DaemonName").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonPropertyTable).Index("IX_NumberValue_Name_DaemonName_ColonyId").Exists())
            {
                Create.Index("IX_NumberValue_Name_DaemonName_ColonyId").OnTable(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .OnColumn("NumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("DaemonName").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonPropertyTable).Index("IX_FloatingNumberValue_Name_DaemonName_ColonyId").Exists())
            {
                Create.Index("IX_FloatingNumberValue_Name_DaemonName_ColonyId").OnTable(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .OnColumn("FloatingNumberValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("DaemonName").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonPropertyTable).Index("IX_DateValue_Name_DaemonName_ColonyId").Exists())
            {
                Create.Index("IX_DateValue_Name_DaemonName_ColonyId").OnTable(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .OnColumn("DateValue").Ascending()
                        .OnColumn("Name").Ascending()
                        .OnColumn("DaemonName").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonPropertyTable).Index("IX_Name_TextValue_DaemonName_ColonyId").Exists())
            {
                Create.Index("IX_Name_TextValue_DaemonName_ColonyId").OnTable(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("TextValue").Ascending()
                        .OnColumn("DaemonName").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonPropertyTable).Index("IX_Name_NumberValue_DaemonName_ColonyId").Exists())
            {
                Create.Index("IX_Name_NumberValue_DaemonName_ColonyId").OnTable(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("NumberValue").Ascending()
                        .OnColumn("DaemonName").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonPropertyTable).Index("IX_Name_FloatingNumberValue_DaemonName_ColonyId").Exists())
            {
                Create.Index("IX_Name_FloatingNumberValue_DaemonName_ColonyId").OnTable(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("FloatingNumberValue").Ascending()
                        .OnColumn("DaemonName").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonPropertyTable).Index("IX_Name_DateValue_DaemonName_ColonyId").Exists())
            {
                Create.Index("IX_Name_DateValue_DaemonName_ColonyId").OnTable(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("DateValue").Ascending()
                        .OnColumn("DaemonName").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonPropertyTable).Index("IX_Name_BooleanValue_DaemonName_ColonyId").Exists())
            {
                Create.Index("IX_Name_BooleanValue_DaemonName_ColonyId").OnTable(MigrationState.TableNames.ColonyDaemonPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("BooleanValue").Ascending()
                        .OnColumn("DaemonName").Ascending()
                        .OnColumn("ColonyId").Ascending();
            }
            //// Daemon log table
            // Table
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonLogTable).Exists())
            {
                Create.Table(MigrationState.TableNames.ColonyDaemonLogTable)
                        .WithColumn("Id").AsInt64().NotNullable()
                            .Identity()
                            .PrimaryKey($"PK_{MigrationState.TableNames.ColonyDaemonLogTable}")
                        .WithColumn("ColonyId").AsString(255).NotNullable()
                        .WithColumn("DaemonName").AsString(255).NotNullable().WithColumn("LogLevel").AsInt32().NotNullable()
                        .WithColumn("Message").AsCustom("LONGTEXT").NotNullable()
                        .WithColumn("ExceptionType").AsString(1024).Nullable()
                        .WithColumn("ExceptionMessage").AsCustom("LONGTEXT").Nullable()
                        .WithColumn("ExceptionStackTrace").AsCustom("LONGTEXT").Nullable()
                        .WithColumn("CreatedAt").AsCustom("DATETIME(6)").Nullable();

                Create.ForeignKey($"FK_{MigrationState.Environment}_ColonyDaemonLogTable_ColonyDaemon").FromTable(MigrationState.TableNames.ColonyDaemonLogTable)
                        .ForeignColumns("ColonyId", "DaemonName")
                        .ToTable(MigrationState.TableNames.ColonyDaemonTable).PrimaryColumns("ColonyId", "Name")
                        .OnDeleteOrUpdate(System.Data.Rule.Cascade);
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonLogTable).Index("IX_ColonyId_DaemonName_LogLevel_CreatedAt").Exists())
            {
                Create.Index("IX_ColonyId_DaemonName_LogLevel_CreatedAt").OnTable(MigrationState.TableNames.ColonyDaemonLogTable)
                        .OnColumn("ColonyId").Ascending()
                        .OnColumn("DaemonName").Ascending()
                        .OnColumn("LogLevel").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonLogTable).Index("IX_ColonyId_DaemonName_CreatedAt").Exists())
            {
                Create.Index("IX_ColonyId_DaemonName_CreatedAt").OnTable(MigrationState.TableNames.ColonyDaemonLogTable)
                        .OnColumn("ColonyId").Ascending()
                        .OnColumn("DaemonName").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
            if (!Schema.Table(MigrationState.TableNames.ColonyDaemonLogTable).Index("IX_ColonyId_CreatedAt").Exists())
            {
                Create.Index("IX_ColonyId_CreatedAt").OnTable(MigrationState.TableNames.ColonyDaemonLogTable)
                        .OnColumn("ColonyId").Ascending()
                        .OnColumn("CreatedAt").Ascending();
            }
        }
    }
}
