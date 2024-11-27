using Sels.Core.Extensions.Fluent;
using Sels.Core.Extensions.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm.IdGenerators
{
    /// <summary>
    /// Generates id(s) for drones using alphabet letters.
    /// </summary>
    public class AlphabetIdGenerator : IDroneIdGenerator
    {
        // Fields
        private readonly bool _toLower;

        /// <inheritdoc cref="AlphabetIdGenerator"/>
        /// <param name="toLower">If the letter should be returned as lower case</param>
        public AlphabetIdGenerator(bool toLower)
        {
            _toLower = toLower;   
        }
        /// <inheritdoc/>
        public Task<string[]> GenerateRangeAsync(int amount) => Enumerable.Range(1, amount).Select(x => IntToChar(x)).WhenForEach(_toLower, x => x.ToLower()).ToArray().ToTaskResult();

        private string IntToChar(int value)
        {
            if (value <= 26)
            {
                return Convert.ToChar(value + 64).ToString();
            }
            int div = value / 26;
            int mod = value % 26;
            if (mod == 0) { mod = 26; div--; }
            return IntToChar(div) + IntToChar(mod);
        }
    }
}
