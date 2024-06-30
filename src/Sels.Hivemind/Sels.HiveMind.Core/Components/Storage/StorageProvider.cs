using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Conversion;
using Sels.Core.Extensions.Logging;
using Sels.HiveMind;
using Sels.HiveMind.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Storage
{
    /// <inheritdoc cref="IStorageProvider"/>
    public class StorageProvider : ComponentProvider<IStorage>,IStorageProvider
    {
        /// <inheritdoc cref="StorageProvider"/>
        /// <param name="serviceProvider">Used by the factories to resolve any dependencies</param>
        /// <param name="factories">Any available factories</param>
        /// <param name="logger">Optional logger for tracing</param>
        public StorageProvider(IServiceProvider serviceProvider, IEnumerable<IComponentFactory<IStorage>> factories, ILogger<StorageProvider> logger = null) : base(serviceProvider, factories, logger)
        {

        }
    }
}
