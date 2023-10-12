using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job
{
    /// <summary>
    /// Table that contains the properties linked to a background job.
    /// </summary>
    public class BackgroundJobPropertyTable : BasePropertyTable
    {
        /// <summary>
        /// The id of the background job the property is linked to.
        /// </summary>
        public long BackgroundJobId { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="property"/>.
        /// </summary>
        /// <param name="property">The instance to create from</param>
        public BackgroundJobPropertyTable(StorageProperty property) : base(property)
        {

        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BackgroundJobPropertyTable()
        {

        }
    }
}
