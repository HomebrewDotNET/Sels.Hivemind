using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Generates a range of unique id(s) for drones in a swarm.
    /// </summary>
    public interface IDroneIdGenerator
    {
        /// <summary>
        /// Generates <paramref name="amount"/> unique drone id's
        /// </summary>
        /// <param name="amount">How many id's to generate</param>
        /// <returns><paramref name="amount"/> unique id's</returns>
        public Task<string[]> GenerateRangeAsync(int amount);
    }
}
