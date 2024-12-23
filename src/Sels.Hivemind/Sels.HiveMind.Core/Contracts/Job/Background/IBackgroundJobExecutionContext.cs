﻿using Microsoft.Extensions.Logging;
using Sels.HiveMind.Client;
using Sels.HiveMind.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sels.HiveMind.Job.Background
{
    /// <summary>
    /// Exposes more information/functionality to executing background jobs.
    /// </summary>
    [LogParameter(HiveLog.Job.Type, HiveLog.Job.BackgroundJobType)]
    public interface IBackgroundJobExecutionContext : IJobExecutionContext<IWriteableBackgroundJob>
    {
    }
}
