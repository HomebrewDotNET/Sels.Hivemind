using Microsoft.Extensions.Logging;
using Sels.HiveMind.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Examples
{
    /// <summary>
    /// Example background job implementation.
    /// </summary>
    public class BackgroundJobExample
    {
        /// <summary>
        /// Example method that can be invoked by a Drone.
        /// </summary>
        /// <param name="context">HiveMind execution context</param>
        /// <param name="input">Custom input to the job</param>
        /// <param name="token">Token that will be cancelled to cancel the job</param>
        /// <returns>Custom processing result</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<object> ExecuteAsync(IBackgroundJobExecutionContext context, BackgroundJobExampleInput input, CancellationToken token)
        {
            // Access background job state
            if(!context.Job.TryGetProperty<Guid>("TenantId", out var tenantId))
            {
                context.Log(LogLevel.Warning, $"Tenant id is missing, can't execute job");
                throw new InvalidOperationException("Expected tenant id to be assigned to job but was missing");
            }

            // Get input to job
            var entityId = input.EntityId;

            // Check if already processed
            if(await context.Job.TryGetDataAsync<bool>("EntityProcessed", token) is (true, true))
            {
                context.Log(LogLevel.Warning, $"Entity {entityId} was already processed. Skipping");
            }
            else
            {
                // Process entity
                context.Log($"Processing entity {entityId}");

                // Save processing state
                await context.Job.SetDataAsync("EntityProcessed", true, token);
            }

            return $"Processed entity {entityId}";
        }
    }

    /// <summary>
    /// Example custom input for background job.
    /// </summary>
    public class BackgroundJobExampleInput
    {
        public string EntityId { get; set; }
    }
}
