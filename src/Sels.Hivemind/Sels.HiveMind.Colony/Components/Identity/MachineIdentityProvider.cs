using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Sels.HiveMind.Colony.Identity
{
    /// <summary>
    /// <inheritdoc cref="IColonyIdentityProvider"/>.
    /// Generates an identity based on machine name and process id. Uses an incrementing number each time a name is generates per environment.
    /// </summary>
    public class MachineIdentityProvider : IColonyIdentityProvider
    {
        private readonly Dictionary<string, int> _generated = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public string GenerateName(IReadOnlyColony colony)
        {
            lock(_generated)
            {
                string name = null;
                if (_generated.ContainsKey(colony.Environment))
                {
                    var current = _generated[colony.Environment];
                    using (Process currentProcess = Process.GetCurrentProcess())
                    {
                        name = $"{Environment.MachineName}-{current}:{currentProcess.Id}";
                    }

                    _generated[colony.Environment] = current + 1;
                }
                else
                {
                    using (Process currentProcess = Process.GetCurrentProcess())
                    {
                        name = $"{Environment.MachineName}:{currentProcess.Id}";
                    }
                    _generated.Add(colony.Environment, 1);
                }

                return name;
            }
        }
    }
}
