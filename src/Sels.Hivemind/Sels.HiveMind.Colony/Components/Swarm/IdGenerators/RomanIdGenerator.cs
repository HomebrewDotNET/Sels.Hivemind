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
    /// Generates id(s) for drones using roman numeric characters.
    /// </summary>
    public class RomanIdGenerator : IDroneIdGenerator
    {
        // Statics
        private static readonly Dictionary<int, string> Roman = new Dictionary<int, string>() { { 1, "I" }, { 4, "IV" }, { 5, "V" }, { 9, "IX" }, { 10, "X" }, { 40, "XL" }, { 50, "L" }, { 90, "XC" }, { 100, "C" }, { 400, "CD" }, { 500, "D" }, { 900, "CM" }, { 1000, "M" } }.OrderByDescending(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

        // Fields
        private readonly bool _toLower;

        /// <inheritdoc cref="RomanIdGenerator"/>
        /// <param name="toLower">If the letter should be returned as lower case</param>
        public RomanIdGenerator(bool toLower)
        {
            _toLower = toLower;   
        }
        /// <inheritdoc/>
        public Task<string[]> GenerateRangeAsync(int amount) => Enumerable.Range(1, amount).Select(x => IntToChar(x)).WhenForEach(_toLower, x => x.ToLower()).ToArray().ToTaskResult();

        private string IntToChar(int value)
        {
            string result = string.Empty;
            while (value > 0)
                foreach (var item in Roman)
                    if (value / item.Key >= 1)
                    {
                        value -= item.Key;
                        result += item.Value;
                        break;
                    }
            return result;
        }
    }
}
