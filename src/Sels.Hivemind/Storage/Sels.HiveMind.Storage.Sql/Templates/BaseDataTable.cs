using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Templates
{
    /// <summary>
    /// Base class for tables that contains unqueryable data attached to jobs.
    /// </summary>
    public abstract class BaseDataTable
    {
        /// <summary>
        /// The name of the data entry.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The value serialized in a format for storage.
        /// </summary>
        public string Value { get; set; }
    }
}
