using FluentMigrator.Runner.VersionTableInfo;
using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.MySql.Deployment
{
    /// <summary>
    /// Schema for the table that stores the current schema version.
    /// </summary>
    internal class SchemaVersionTableInfo : DefaultVersionTableMetaData
    {
        // Fields
        private readonly string _tableName;

#pragma warning disable CS0618 // Type or member is obsolete
        public SchemaVersionTableInfo(string environment)
        {
            _tableName = $"HiveMind.{environment}.StorageSchema";
        }
#pragma warning restore CS0618 // Type or member is obsolete

        public override string TableName => _tableName;
    }
}
