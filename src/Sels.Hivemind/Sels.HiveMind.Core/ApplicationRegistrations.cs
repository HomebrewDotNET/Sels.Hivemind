using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using Sels.HiveMind.Storage;
using Sels.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sels.HiveMind.Client;
using Sels.HiveMind.Validation;
using Sels.HiveMind.Events;
using Sels.HiveMind.Events.Job;
using Sels.HiveMind.Requests;
using Sels.HiveMind;
using Sels.HiveMind.Job;
using Sels.Core.ServiceBuilder;
using Sels.HiveMind.Queue;
using Sels.HiveMind.EventHandlers;
using Sels.HiveMind.RequestHandlers;
using Sels.HiveMind.Scheduler;
using Sels.HiveMind.Service;
using Sels.HiveMind.Interval;
using System.Threading;
using System.Threading.Tasks;
using Sels.HiveMind.Calendar;
using System.Globalization;
using Sels.HiveMind.EventHandlers.BackgroundJob;
using Sels.HiveMind.EventHandlers.RecurringJob;
using Sels.HiveMind.Components.EventHandlers.RecurringJob;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extension methods for registering services.
    /// </summary>
    public static class ApplicationRegistrations
    {
        /// <summary>
        /// Adds all the core services needed by HiveMind.
        /// </summary>
        /// <param name="services">Collection to add the services to</param>
        /// <returns><paramref name="services"/> for method chaining</returns>
        public static IServiceCollection AddHiveMind(this IServiceCollection services)
        {
            services.ValidateArgument(nameof(services));

            // Event/request handlers
            services.AddEventHandlers();

            // Schedulers
            services.AddSchedulers();

            // Intervals
            services.AddIntervals();

            // Calendars
            services.AddIntervals();

            // Mediator
            services.AddNotifier();

            // Options
            services.AddOptions();
            services.AddValidationProfile<HiveMindOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<HiveMindOptions, HiveMindOptionsValidationProfile>();
            services.BindOptionsFromConfig<HiveMindOptions>(nameof(HiveMindOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);
            
            services.AddValidationProfile<HiveMindLoggingOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<HiveMindLoggingOptions, HiveMindLoggingOptionsValidationProfile>();
            services.BindOptionsFromConfig<HiveMindLoggingOptions>(nameof(HiveMindLoggingOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.Ignore, false);

            // Client
            services.New<IBackgroundJobClient, BackgroundJobClient>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ClientWarningThreshold, options.ClientErrorThreshold);
                    })
                    .TryRegister();
            services.New<IRecurringJobClient, RecurringJobClient>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ClientWarningThreshold, options.ClientErrorThreshold);
                    })
                    .TryRegister();

            // Services
            services.New<IStorageProvider, StorageProvider>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegister();
            services.New<IJobQueueProvider, JobQueueProvider>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegister();
            services.New<IJobSchedulerProvider, JobSchedulerProvider>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegister();
            services.New<IIntervalProvider, IntervalProvider>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegister();
            services.AddValidationProfile<BackgroundJobValidationProfile, string>();
            services.AddValidationProfile<BackgroundJobQueryValidationProfile, string>();
            services.New<IBackgroundJobService, BackgroundJobService>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegister();
            services.AddValidationProfile<RecurringJobValidationProfile, string>();
            //services.AddValidationProfile<RecurringJobQueryValidationProfile, string>();
            services.New<IRecurringJobService, RecurringJobService>()
                    .AsSingleton()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegister();


            return services;
        }

        private static IServiceCollection AddEventHandlers(this IServiceCollection services)
        {
            services.ValidateArgument(nameof(services));

            //// Background job
            // Meta data tagger
            services.BindOptionsFromConfig<JobMetaDataOptions>(nameof(JobMetaDataOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);

            services.New<MetaDataTagger>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<MetaDataTagger, BackgroundJobSavingEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            // Job retry handler
            services.AddValidationProfile<BackgroundJobRetryOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<BackgroundJobRetryOptions, BackgroundJobRetryOptionsValidationProfile>();
            services.BindOptionsFromConfig<BackgroundJobRetryOptions>(nameof(BackgroundJobRetryOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);

            services.New<BackgroundJobRetryHandler>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddRequestHandler<BackgroundJobStateElectionRequest, IBackgroundJobState, BackgroundJobRetryHandler>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            // Background job process trigger
            services.New<BackgroundJobProcessTrigger>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<BackgroundJobProcessTrigger, BackgroundJobFinalStateElectedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));
            services.AddEventListener<BackgroundJobProcessTrigger, BackgroundJobLockTimedOutEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            // Background job cleanup trigger
            services.New<BackgroundJobCleanupTrigger>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<BackgroundJobCleanupTrigger, BackgroundJobStateAppliedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));
            services.AddEventListener<BackgroundJobCleanupTrigger, BackgroundJobFinalStateElectedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            // Background job awaiting handler
            services.New<BackgroundJobAwaitingProcessTrigger>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<BackgroundJobAwaitingProcessTrigger, BackgroundJobFinalStateElectedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));
            services.AddRequestHandler<BackgroundJobStateElectionRequest, IBackgroundJobState, BackgroundJobAwaitingProcessTrigger>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));


            //// Recurring job
            // Recurring job process trigger
            services.New<RecurringJobProcessTrigger>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<RecurringJobProcessTrigger, RecurringJobFinalStateElectedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));
            services.AddEventListener<RecurringJobProcessTrigger, RecurringJobLockTimedOutEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            // Sequence manager
            services.New<RecurringJobStateSequenceManager>()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.EventHandlersWarningThreshold, options.EventHandlersErrorThreshold);
                    })
                    .AsSingleton()
                    .TryRegister();
            services.AddEventListener<RecurringJobStateSequenceManager, RecurringJobStateAppliedEvent>(x => x.AsForwardedService().WithBehaviour(RegisterBehaviour.TryAddImplementation));

            return services;
        }

        private static IServiceCollection AddSchedulers(this IServiceCollection services)
        {
            services.ValidateArgument(nameof(services));

            // Lazy 
            services.AddValidationProfile<LazySchedulerOptionsValidationProfile, string>();
            services.AddOptionProfileValidator<PullthroughSchedulerOptions, LazySchedulerOptionsValidationProfile>();
            services.BindOptionsFromConfig<PullthroughSchedulerOptions>(nameof(PullthroughSchedulerOptions), Sels.Core.Options.ConfigurationProviderNamedOptionBehaviour.SubSection, true);

            services.New<IJobSchedulerFactory, PullthroughSchedulerFactory>()
                    .AsScoped()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegisterImplementation();

            return services;
        }

        private static IServiceCollection AddIntervals(this IServiceCollection services)
        {
            services.ValidateArgument(nameof(services));

            // Time
            services.New<IIntervalFactory, TimeIntervalFactory>()
                    .AsScoped()
                    .Trace((s, x) => {
                        var options = s.GetRequiredService<IOptions<HiveMindLoggingOptions>>().Value;
                        return x.Duration.OfAll.WithDurationThresholds(options.ServiceWarningThreshold, options.ServiceErrorThreshold);
                    })
                    .TryRegisterImplementation();

            return services;
        }

        private static IServiceCollection AddCalendars(this IServiceCollection services)
        {
            services.ValidateArgument(nameof(services));

            services.AddCalendar(Calendars.Monday.ToString(), () => new WeekdayCalendar(new DayOfWeek[] { DayOfWeek.Monday }));
            services.AddCalendar(Calendars.Tuesday.ToString(), () => new WeekdayCalendar(new DayOfWeek[] { DayOfWeek.Tuesday }));
            services.AddCalendar(Calendars.Wednesday.ToString(), () => new WeekdayCalendar(new DayOfWeek[] { DayOfWeek.Wednesday }));
            services.AddCalendar(Calendars.Thursday.ToString(), () => new WeekdayCalendar(new DayOfWeek[] { DayOfWeek.Thursday }));
            services.AddCalendar(Calendars.Friday.ToString(), () => new WeekdayCalendar(new DayOfWeek[] { DayOfWeek.Friday }));
            services.AddCalendar(Calendars.Saturday.ToString(), () => new WeekdayCalendar(new DayOfWeek[] { DayOfWeek.Saturday }));
            services.AddCalendar(Calendars.Sunday.ToString(), () => new WeekdayCalendar(new DayOfWeek[] { DayOfWeek.Sunday }));
            services.AddCalendar(Calendars.WorkWeek.ToString(), () => new WeekdayCalendar(new DayOfWeek[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }));
            services.AddCalendar(Calendars.Weekend.ToString(), () => new WeekdayCalendar(new DayOfWeek[] { DayOfWeek.Saturday, DayOfWeek.Sunday }));

            services.AddCalendar(Calendars.NineToFive.ToString(), () => new DailyTimeframeCalendar(TimeSpan.FromHours(9), TimeSpan.FromHours(17)));

            services.AddCalendar(Calendars.StartOfMonth.ToString(), () => new DayCalendar((1, null, null)));
            services.AddCalendar(Calendars.StartOfYear.ToString(), () => new DayCalendar((1, 1, null)));

            return services;
        }

        #region Calendar
        /// <summary>
        /// Adds a calendar with a specific name that can be referenced by other HiveMind components.
        /// </summary>
        /// <param name="services">Collection to add the services to</param>
        /// <param name="name">The unique name of the calendar</param>
        /// <param name="factory">Delegate that creates the calendar. Deleagte matches the method signiture of <see cref="ICalendarFactory.CreateCalendarAsync(IServiceProvider, CancellationToken)"/></param>
        /// <returns><paramref name="services"/> for method chaining</returns>
        public static IServiceCollection AddCalendar(this IServiceCollection services, string name, Func<IServiceProvider, CancellationToken, Task<ICalendar>> factory)
        {
            services.ValidateArgument(nameof(services));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            factory.ValidateArgument(nameof(factory));

            services.AddSingleton<ICalendarFactory>(new DelegateCalendarFactory(name, factory));

            return services;
        }

        /// <summary>
        /// Adds a calendar with a specific name that can be referenced by other HiveMind components.
        /// </summary>
        /// <param name="services">Collection to add the services to</param>
        /// <param name="name">The unique name of the calendar</param>
        /// <param name="factory">Delegate that creates the calendar. Deleagte matches the method signiture of <see cref="ICalendarFactory.CreateCalendarAsync(IServiceProvider, CancellationToken)"/></param>
        /// <returns><paramref name="services"/> for method chaining</returns>
        public static IServiceCollection AddCalendar(this IServiceCollection services, string name, Func<IServiceProvider, CancellationToken, ICalendar> factory)
        {
            services.ValidateArgument(nameof(services));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            factory.ValidateArgument(nameof(factory));

            return services.AddCalendar(name, (s, t) => Task.FromResult(factory(s, t)));
        }

        /// <summary>
        /// Adds a calendar with a specific name that can be referenced by other HiveMind components.
        /// </summary>
        /// <param name="services">Collection to add the services to</param>
        /// <param name="name">The unique name of the calendar</param>
        /// <param name="factory">Delegate that creates the calendar. Deleagte matches the method signiture of <see cref="ICalendarFactory.CreateCalendarAsync(IServiceProvider, CancellationToken)"/></param>
        /// <returns><paramref name="services"/> for method chaining</returns>
        public static IServiceCollection AddCalendar(this IServiceCollection services, string name, Func<ICalendar> factory)
        {
            services.ValidateArgument(nameof(services));
            name.ValidateArgumentNotNullOrWhitespace(nameof(name));
            factory.ValidateArgument(nameof(factory));

            return services.AddCalendar(name, (s, t) => Task.FromResult(factory()));
        }
        #endregion
    }
}
