using Microsoft.Extensions.Caching.Memory;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Collections;
using Sels.HiveMind.Job;
using Sels.HiveMind.Queue;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using static Sels.HiveMind.HiveMindConstants.Job;

namespace Sels.HiveMind.Client
{
    /// <inheritdoc cref="IJobBuilder{TBuilder}"/>
    internal abstract class BaseJobBuilder<T> : IJobBuilder<T>
    {
        // Fields
        private readonly List<MiddlewareInfo> _middleware = new List<MiddlewareInfo>();
        private readonly Dictionary<string, object> _properties = new Dictionary<string, object>();
        protected readonly IMemoryCache _cache;
        protected readonly HiveMindOptions _options;

        // Properties
        /// <inheritdoc/>
        public IStorageConnection Connection { get; }
        // <inheritdoc/>
        public string Queue { get; private set; }
        // <inheritdoc/>
        public QueuePriority Priority { get; private set; }
        // <inheritdoc/>
        public IReadOnlyList<MiddlewareInfo> Middleware => _middleware;
        // <inheritdoc/>
        public IReadOnlyDictionary<string, object> Properties => _properties;

        protected abstract T Builder { get; }

        protected BaseJobBuilder(IStorageConnection connection, HiveMindOptions options, IMemoryCache cache)
        {
            Connection = connection.ValidateArgument(nameof(connection));

            InQueue(HiveMindConstants.Queue.DefaultQueue, QueuePriority.Normal);

            _cache = cache.ValidateArgument(nameof(cache));
            _options = options.ValidateArgument(nameof(options));
        }

        /// <inheritdoc/>
        public T InQueue(string queue, QueuePriority priority = QueuePriority.Normal)
        {
            Queue = queue.ValidateArgument(nameof(queue));
            Priority = priority;
            return Builder;
        }

        /// <inheritdoc/>
        public T WithProperty(string name, object value)
        {
            name.ValidateArgument(nameof(name));
            value.ValidateArgument(nameof(value));

            _properties.AddOrUpdate(name, value);
            return Builder;
        }

        /// <inheritdoc/>
        public T WithMiddleWare(Type type, object context, byte? priority)
        {
            type.ValidateArgument(nameof(type));
            type.ValidateArgumentInstanceable(nameof(type));

            CheckMiddleware(type, context);
            _middleware.Add(new MiddlewareInfo(type, context, priority, _options, _cache));

            return Builder;
        }

        protected abstract void CheckMiddleware(Type type, object context);
    }
}
