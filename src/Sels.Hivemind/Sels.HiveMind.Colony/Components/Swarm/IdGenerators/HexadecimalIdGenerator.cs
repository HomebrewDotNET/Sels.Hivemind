using Sels.Core.Extensions.Threading;
using Sels.HiveMind.Colony.Swarm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm.IdGenerators
{
    public class HexadecimalIdGenerator : IDroneIdGenerator
    {
        // Fields
        private readonly int? _padding;
        private readonly bool _toLower;

        /// <inheritdoc cref="HexadecimalIdGenerator"/>
        /// <param name="padding">Optionally how many 0 should be added until we reach the configured lenght</param>
        /// <param name="tolower">If the letter should be returned as lower case</param>
        public HexadecimalIdGenerator(int? padding, bool tolower)
        {
            _padding = Guard.Is(padding, x => !x.HasValue || x.Value >= 1);
            _toLower = tolower;
        }

        /// <inheritdoc/>
        public Task<string[]> GenerateRangeAsync(int amount) => Enumerable.Range(1, amount).Select(x => x.ToString($"{(_toLower ? 'x' : 'X')}{(_padding.HasValue ? _padding.Value : string.Empty)}")).ToArray().ToTaskResult();
    }
}
