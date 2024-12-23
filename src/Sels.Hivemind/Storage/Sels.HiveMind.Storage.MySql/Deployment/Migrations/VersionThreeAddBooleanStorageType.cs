﻿using FluentMigrator;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.MySql.Deployment.Migrations
{
    /// <summary>
    /// Adds a boolean storage type to the tables storing properties.
    /// </summary>
    [Migration(3)]
    public class VersionThreeAddBooleanStorageType : AutoReversingMigration
    {
        /// <inheritdoc/>
        public override void Up()
        {
            ////// Background job
            //// Background job property
            // Table
            if(Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Exists() && !Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Column("BooleanValue").Exists())
            {
                Alter.Table(MigrationState.TableNames.BackgroundJobPropertyTable)
                    .AddColumn("BooleanValue").AsBoolean().Nullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.BackgroundJobPropertyTable).Index("IX_Name_BooleanValue_BackgroundJobId").Exists())
            {
                Create.Index("IX_Name_BooleanValue_BackgroundJobId").OnTable(MigrationState.TableNames.BackgroundJobPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("BooleanValue").Ascending()
                        .OnColumn("BackgroundJobId").Ascending();
            }
            ////// Recurring job
            //// Recurring job property
            // Table
            if (Schema.Table(MigrationState.TableNames.RecurringJobPropertyTable).Exists() && !Schema.Table(MigrationState.TableNames.RecurringJobPropertyTable).Column("BooleanValue").Exists())
            {
                Alter.Table(MigrationState.TableNames.RecurringJobPropertyTable)
                    .AddColumn("BooleanValue").AsBoolean().Nullable();
            }
            // Indexes
            if (!Schema.Table(MigrationState.TableNames.RecurringJobPropertyTable).Index("IX_Name_BooleanValue_RecurringJobId").Exists())
            {
                Create.Index("IX_Name_BooleanValue_RecurringJobId").OnTable(MigrationState.TableNames.RecurringJobPropertyTable)
                        .OnColumn("Name").Ascending()
                        .OnColumn("BooleanValue").Ascending()
                        .OnColumn("RecurringJobId").Ascending();
            }
        }
    }
}
