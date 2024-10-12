using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Client.Query
{
    /// <summary>
    /// Expression used when querying data that can add itself to a string builder.
    /// </summary>
    public interface IQueryExpression
    {
        /// <summary>
        /// Adds text representation of the current expression to <paramref name="stringBuilder"/>.
        /// </summary>
        /// <param name="stringBuilder">The builder to add the text to</param>
        /// <param name="index">Index for tracking the current parameters</param>
        void ToString(StringBuilder stringBuilder, ref int index);
    }
}
