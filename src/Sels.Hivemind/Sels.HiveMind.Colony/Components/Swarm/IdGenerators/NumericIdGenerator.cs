using Sels.Core.Extensions.Fluent;
using Sels.Core.Extensions.Threading;
using Sels.HiveMind.Colony.Swarm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm.IdGenerators
{
    /// <summary>
    /// Generates numeric id(s) for drones.
    /// </summary>
    public class NumericIdGenerator : IDroneIdGenerator
    {
        // Fields
        private readonly int? _padding;

        /// <inheritdoc cref="NumericIdGenerator"/>
        /// <param name="padding">Optionally how many 0 should be added until we reach the configured lenght</param>
        public NumericIdGenerator(int? padding)
        {
            _padding = Guard.Is(padding, x => !x.HasValue || x.Value >= 1);
        }
        /// <inheritdoc cref="NumericIdGenerator"/>
        public Task<string[]> GenerateRangeAsync(int amount) => Enumerable.Range(1, amount).Select(x => x.ToString()).WhenForEach(_padding.HasValue, x => x.PadLeft(_padding.Value, '0')).ToArray().ToTaskResult();
    }
}
