using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.Core;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Logging;
using Sels.Core.Mediator.Event;
using Sels.HiveMind.Events;
using Sels.HiveMind.Events.Job;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.EventHandlers
{
    /// <summary>
    /// Sets some properties during creation of jobs.
    /// </summary>
    public class MetaDataTagger : IBackgroundJobSavingEventHandler
    {
        // Statics
        private static OSPlatform OSPlatform = Helper.App.GetCurrentOsPlatform();

        // Fields
        private readonly IOptionsSnapshot<JobMetaDataOptions> _options;
        private readonly ILogger _logger;

        // Properties
        /// <inheritdoc/>
        public ushort? Priority => 0; // Run first so properties can be overwritten

        /// <inheritdoc cref="MetaDataTagger"/>
        /// <param name="options">Used to access the options for the handler</param>
        /// <param name="logger">Optional logger for tracing</param>
        public MetaDataTagger(IOptionsSnapshot<JobMetaDataOptions> options, ILogger<MetaDataTagger> logger = null)
        {
            _options = options.ValidateArgument(nameof(options));
            _logger = logger;
        }

        /// <summary>
        /// Proxy constructor.
        /// </summary>
        protected MetaDataTagger()
        {

        }

        /// <inheritdoc/>
        public virtual Task HandleAsync(IEventListenerContext context, BackgroundJobSavingEvent @event, CancellationToken token)
        {
            context.ValidateArgument(nameof(context));
            @event.ValidateArgument(nameof(@event));

            var job = @event.Job;
            if (@event.IsCreation)
            {
                var options = _options.Get(@event.Job.Environment);
                _logger.Log($"Setting meta data on new background job");
                if (options.MetaData.HasFlag(JobMetaData.MachineName)) job.SetProperty(HiveMindConstants.Job.Properties.SourceMachineName, Environment.MachineName);
                if (options.MetaData.HasFlag(JobMetaData.MachinePlatform)) job.SetProperty(HiveMindConstants.Job.Properties.SourceMachinePlatform, OSPlatform.ToString());
                if (options.MetaData.HasFlag(JobMetaData.MachineArchitecture)) job.SetProperty(HiveMindConstants.Job.Properties.SourceMachineArchitecture, RuntimeInformation.ProcessArchitecture);
                if (options.MetaData.HasFlag(JobMetaData.UserName)) job.SetProperty(HiveMindConstants.Job.Properties.SourceUserName, Environment.UserName);
                if (options.MetaData.HasFlag(JobMetaData.UserDomain)) job.SetProperty(HiveMindConstants.Job.Properties.SourceUserDomain, Environment.UserDomainName);
                if (options.MetaData.HasFlag(JobMetaData.ThreadCulture)) job.SetProperty(HiveMindConstants.Job.Properties.ThreadCulture, Thread.CurrentThread.CurrentCulture.Name);
                if (options.MetaData.HasFlag(JobMetaData.ThreadUiCulture)) job.SetProperty(HiveMindConstants.Job.Properties.ThreadUiCulture, Thread.CurrentThread.CurrentUICulture.Name);
            }

            return Task.CompletedTask;
        }
    }
}
