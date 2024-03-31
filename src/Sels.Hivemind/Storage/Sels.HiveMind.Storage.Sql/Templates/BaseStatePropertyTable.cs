using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Templates
{
    /// <summary>
    /// base class for a table that contains queryable properties on a state of a job.
    /// </summary>
    public class BaseStatePropertyTable : BasePropertyTable
    {
        /// <summary>
        /// The id of the state the property is linked to.
        /// </summary>
        public long StateId { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="property"/>.
        /// </summary>
        /// <param name="property">The instance to create from</param>
        public BaseStatePropertyTable(StorageProperty property) : base(property)
        {

        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BaseStatePropertyTable()
        {

        }
    }
}
