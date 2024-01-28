using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions;
using Sels.HiveMind.Storage.Sql.Templates;
using System;
using System.Collections.Generic;
using System.Text;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;

namespace Sels.HiveMind.Storage.Sql.Job
{
    /// <summary>
    /// Model that maps to the table that contains the queryable properties of background job states.
    /// </summary>
    public class BackgroundJobStatePropertyTable : BasePropertyTable
    {
        /// <summary>
        /// The id of the state the property is linked to.
        /// </summary>
        public long StateId { get; set; }

        /// <summary>
        /// Creates an instance from <paramref name="property"/>.
        /// </summary>
        /// <param name="property">The instance to create from</param>
        public BackgroundJobStatePropertyTable(StorageProperty property) : base(property)
        {

        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BackgroundJobStatePropertyTable()
        {

        }
    }
}
