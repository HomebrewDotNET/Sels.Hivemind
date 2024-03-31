using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage.Sql.Job.Recurring
{
    /// <summary>
    /// Model that maps to the table that contains the properties linked to a recurring job.
    /// </summary>
    public class RecurringJobPropertyTable : BaseStatePropertyTable
    {
        /// <summary>
        /// The id of the recurring job the property is linked to.
        /// </summary>
        public string RecurringJobId { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="property"/>.
        /// </summary>
        /// <param name="property">The instance to create from</param>
        public RecurringJobPropertyTable(StorageProperty property) : base(property)
        {

        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public RecurringJobPropertyTable()
        {

        }
    }
}
