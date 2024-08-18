using Sels.HiveMind.Colony.Swarm.Job;
using Sels.HiveMind.Job.Recurring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Colony.Swarm.Job.Recurring
{
    /// <summary>
    /// The options used by a worker swarm host that processes background jobs by executing them.
    /// </summary>
    public class RecurringJobWorkerSwarmHostOptions : WorkerSwarmHostOptions<IRecurringJobMiddleware, RecurringJobWorkerSwarmHostOptions, IRecurringJobWorkerSwarmHostOptions>, IRecurringJobWorkerSwarmHostOptions
    {
        /// <inheritdoc/>
        protected override RecurringJobWorkerSwarmHostOptions Self => this;

        /// <inheritdoc cref="RecurringJobWorkerSwarmHostOptions"/>
        public RecurringJobWorkerSwarmHostOptions()
        {

        }

        /// <inheritdoc cref="RecurringJobWorkerSwarmHostOptions"/>
        /// <param name="name"><inheritdoc cref="ISwarmHostOptions{TOptions}.Name"/></param>
        /// <param name="builder">The delegate used to configure the current instance</param>
        public RecurringJobWorkerSwarmHostOptions(string name, Action<RecurringJobWorkerSwarmHostOptions> builder)
        {
            Name = Guard.IsNotNullOrWhitespace(name);
            Guard.IsNotNull(builder).Invoke(this);
        }

        /// <inheritdoc/>
        protected override RecurringJobWorkerSwarmHostOptions CreateSubSwarmOptions(string name, Action<RecurringJobWorkerSwarmHostOptions> builder) => new RecurringJobWorkerSwarmHostOptions(name, builder);
    }

    /// <summary>
    /// Contains the validation rules for <see cref="RecurringJobWorkerSwarmHostOptions"/>
    /// </summary>
    public class RecurringJobWorkerSwarmHostOptionsValidationProfile : WorkerSwarmHostOptionsValidationProfile<IRecurringJobMiddleware, RecurringJobWorkerSwarmHostOptions, IRecurringJobWorkerSwarmHostOptions>
    {
        // Statics
        /// <summary>
        /// Default global instance.
        /// </summary>
        public static RecurringJobWorkerSwarmHostOptionsValidationProfile Instance { get; } = new RecurringJobWorkerSwarmHostOptionsValidationProfile();

        /// <inheritdoc cref="RecurringJobWorkerSwarmHostOptionsValidationProfile"/>
        public RecurringJobWorkerSwarmHostOptionsValidationProfile() : base()
        {
        }
    }
}
