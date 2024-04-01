using Sels.Core.Conversion.Extensions;
using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Interval
{
    /// <summary>
    /// An interval that uses <see cref="TimeSpan"/> to generate next dates.
    /// </summary>
    public class TimeInterval : IInterval
    {
        // Statics
        /// <summary>
        /// The type of the interval.
        /// </summary>
        public static string Type => "Time";

        /// <inheritdoc />
        public Task<DateTime> GetNextDateAsync(DateTime date, object interval, CancellationToken cancellationToken = default)
        {
            interval.ValidateArgument(nameof(interval));

            if(interval is TimeSpan timeSpan)
            {
                if(timeSpan < TimeSpan.Zero)
                {
                    throw new ArgumentException("TimeSpan cannot be negative", nameof(interval));
                }

                return Task.FromResult(date.Add(timeSpan));
            }
            else
            {
                throw new ArgumentException($"Interval {interval} is not a TimeSpan", nameof(interval));
            }
        }
    }
}
