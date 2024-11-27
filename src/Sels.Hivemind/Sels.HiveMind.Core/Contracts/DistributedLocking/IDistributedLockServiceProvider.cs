using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.DistributedLocking
{
    /// <summary>
    /// Provider that is able to create <see cref="IDistributedLockService"/> of a certain type.
    /// </summary>
    public interface IDistributedLockServiceProvider : IComponentProvider<IDistributedLockService>
    {

    }
}
