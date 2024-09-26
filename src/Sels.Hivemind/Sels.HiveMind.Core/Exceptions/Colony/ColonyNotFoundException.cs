using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony
{
    /// <summary>
    /// Thrown when a colony couldn't be found.
    /// </summary>
    public class ColonyNotFoundException : Exception
    {
        // Properties
        /// <summary>
        /// The id of the colony that could not be found.
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// The HiveMind environment where the colony could not be found in.
        /// </summary>
        public string Environment { get; }

        /// <inheritdoc cref="ColonyNotFoundException"/>
        /// <param name="id"><inheritdoc cref="Id"/></param>
        /// <param name="environment"><inheritdoc cref="Environment"/></param>
        public ColonyNotFoundException(string id, string environment) : base($"Could not find colony <{id}> in environment <{environment}>")
        {
            Id = id.ValidateArgumentNotNullOrWhitespace(nameof(id));
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }
    }
}
