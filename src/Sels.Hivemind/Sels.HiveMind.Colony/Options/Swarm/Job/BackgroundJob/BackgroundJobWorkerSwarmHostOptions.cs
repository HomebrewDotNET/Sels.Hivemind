using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm.Job.BackgroundJob
{
    /// <summary>
    /// The options used by a worker swarm host that processes background jobs by executing them.
    /// </summary>
    public class BackgroundJobWorkerSwarmHostOptions : WorkerSwarmHostOptions<IBackgroundJobMiddleware, BackgroundJobWorkerSwarmHostOptions, IBackgroundJobWorkerSwarmHostOptions>, IBackgroundJobWorkerSwarmHostOptions
    {
        /// <inheritdoc/>
        protected override BackgroundJobWorkerSwarmHostOptions Self => this;

        /// <inheritdoc cref="BackgroundJobWorkerSwarmHostOptions"/>
        public BackgroundJobWorkerSwarmHostOptions()
        {
            
        }

        /// <inheritdoc cref="BackgroundJobWorkerSwarmHostOptions"/>
        /// <param name="name"><inheritdoc cref="ISwarmHostOptions{TOptions}.Name"/></param>
        /// <param name="builder">The delegate used to configure the current instance</param>
        public BackgroundJobWorkerSwarmHostOptions(string name, Action<BackgroundJobWorkerSwarmHostOptions> builder)
        {
            Name = Guard.IsNotNullOrWhitespace(name);
            Guard.IsNotNull(builder).Invoke(this);
        }

        /// <inheritdoc/>
        protected override BackgroundJobWorkerSwarmHostOptions CreateSubSwarmOptions(string name, Action<BackgroundJobWorkerSwarmHostOptions> builder) => new BackgroundJobWorkerSwarmHostOptions(name, builder);
    }

    /// <summary>
    /// Contains the validation rules for <see cref="BackgroundJobWorkerSwarmHostOptions"/>
    /// </summary>
    public class BackgroundJobWorkerSwarmHostOptionsValidationProfile : WorkerSwarmHostOptionsValidationProfile<IBackgroundJobMiddleware, BackgroundJobWorkerSwarmHostOptions, IBackgroundJobWorkerSwarmHostOptions>
    {
        // Statics
        /// <summary>
        /// Default global instance.
        /// </summary>
        public static BackgroundJobWorkerSwarmHostOptionsValidationProfile Instance { get; } = new BackgroundJobWorkerSwarmHostOptionsValidationProfile();

        /// <inheritdoc cref="BackgroundJobWorkerSwarmHostOptionsValidationProfile"/>
        public BackgroundJobWorkerSwarmHostOptionsValidationProfile() : base()
        {
        }
    }
}
