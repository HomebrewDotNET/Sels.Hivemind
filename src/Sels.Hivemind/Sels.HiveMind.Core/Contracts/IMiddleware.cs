using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind
{
    /// <summary>
    /// Base interface with common properties shared by all the middleware.
    /// </summary>
    public interface IMiddleware
    {
        /// <summary>
        /// The priority of the middleware in the pipeline. Lower value means higher priority. Null means last.
        /// Will be set by the consumer.
        /// </summary>
        public byte? Priority { get; set; }
    }
}
