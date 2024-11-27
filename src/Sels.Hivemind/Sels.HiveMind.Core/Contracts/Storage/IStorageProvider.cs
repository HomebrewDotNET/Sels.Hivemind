using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// Provides access to <see cref="IStorage"/> configured for a certain HiveMind environment.
    /// </summary>
    public interface IStorageProvider : IComponentProvider<IStorage>
    {
    }
}
