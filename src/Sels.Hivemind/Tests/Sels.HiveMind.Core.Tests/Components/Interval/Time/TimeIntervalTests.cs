

using Microsoft.Extensions.DependencyInjection;
using Sels.Core;
using Sels.HiveMind.Interval;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Core.Tests.Components.Interval.Time
{
    public class TimeIntervalTests
    {
        [Test]
        public async Task GetNextDateAsync_ShouldAddIntervalToCurrentDate_WhenIntervalIsValid()
        {
            // Arrange
            await using var provider = new ServiceCollection()
                                       .AddHiveMind()
                                       .BuildServiceProvider();
            var intervalProvider = provider.GetRequiredService<IIntervalProvider>();
            await using var intervalScope = await intervalProvider.CreateAsync(TimeInterval.Type);
            var currentDate = DateTime.Now;
            var interval = intervalScope.Component;
            var minutes = Helper.Random.GetRandomDouble(0, 1440);
            var timespan = TimeSpan.FromMinutes(minutes);

            // Act
            var nextDate = await interval.GetNextDateAsync(currentDate, timespan);

            // Assert
            Assert.That(nextDate, Is.EqualTo(currentDate.AddMinutes(minutes)));
        }

        [Test]
        public async Task GetNextDateAsync_ShouldThrowArgumentException_WhenIntervalIsInvalid()
        {
            // Arrange
            await using var provider = new ServiceCollection()
                                       .AddHiveMind()
                                       .BuildServiceProvider();
            var intervalProvider = provider.GetRequiredService<IIntervalProvider>();
            await using var intervalScope = await intervalProvider.CreateAsync(TimeInterval.Type);
            var currentDate = DateTime.Now;
            var interval = intervalScope.Component;

            // Act and Assert
            Assert.ThrowsAsync<ArgumentException>(async () => await interval.GetNextDateAsync(currentDate, "Invalid"));
        }

        [Test]
        public async Task GetNextDateAsync_ShouldThrowArgumentException_WhenIntervalIsNegative()
        {
            // Arrange
            await using var provider = new ServiceCollection()
                                       .AddHiveMind()
                                       .BuildServiceProvider();
            var intervalProvider = provider.GetRequiredService<IIntervalProvider>();
            await using var intervalScope = await intervalProvider.CreateAsync(TimeInterval.Type);
            var currentDate = DateTime.Now;
            var interval = intervalScope.Component;

            // Act and Assert
            Assert.ThrowsAsync<ArgumentException>(async () => await interval.GetNextDateAsync(currentDate, TimeSpan.FromHours(-1)));
        }
    }
}
