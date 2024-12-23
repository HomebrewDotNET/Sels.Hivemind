﻿using Microsoft.Extensions.Logging;
using Sels.HiveMind.Client;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job.Recurring
{
    /// <summary>
    /// Exposes more information/functionality to executing recurring jobs.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.RecurringJobType)]
    public interface IRecurringJobExecutionContext : IJobExecutionContext<IWriteableRecurringJob>
    {
    }
}
