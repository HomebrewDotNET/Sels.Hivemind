using Sels.HiveMind.Calendar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Core.Tests.Components.Calendar
{
    public class DailyTimeframeCalendarTests
    {
        [Test]
        [TestCase("2022/01/01 10:00:00", "09:00:00", "17:00:00", true)] // 10 AM is within 9 AM - 5 PM
        [TestCase("2022/01/01 18:00:00", "09:00:00", "17:00:00", false)] // 6 PM is outside 9 AM - 5 PM
        public async Task IsInRange_ShouldReturnExpectedResult_WhenTimeIsProvided(string dateStr, string startTimeStr, string endTimeStr, bool expectedResult)
        {
            // Arrange
            var date = DateTime.Parse(dateStr);
            var startTime = TimeSpan.Parse(startTimeStr);
            var endTime = TimeSpan.Parse(endTimeStr);
            var timeframeCalendar = new DailyTimeframeCalendar(startTime, endTime);

            // Act
            var result = await timeframeCalendar.IsInRangeAsync(date);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        [TestCase("2022/01/01 17:00:00", "18:00:00", "02:00:00", "2022/01/01 18:00:00")] // 5 PM to same day 6 PM
        [TestCase("2022/01/02 19:00:00", "22:00:00", "06:00:00", "2022/01/02 22:00:00")] // 7 PM to same day 10 PM
        [TestCase("2022/01/03 14:00:00", "15:00:00", "23:00:00", "2022/01/03 15:00:00")] // 2 PM to same day 3 PM
        [TestCase("2022/01/04 08:00:00", "05:00:00", "07:00:00", "2022/01/05 05:00:00")] // 8 AM to next day 5 AM
        [TestCase("2022/01/05 19:00:00", "20:00:00", "04:00:00", "2022/01/05 20:00:00")] // 7 PM to same day 8 PM
        public async Task GetNextInRange_ShouldReturnNextDateInRange_WhenDateIsNotInRange(string dateStr, string startTimeStr, string endTimeStr, string expectedDateStr)
        {
            // Arrange
            var date = DateTime.Parse(dateStr);
            var startTime = TimeSpan.Parse(startTimeStr);
            var endTime = TimeSpan.Parse(endTimeStr);
            var expectedDate = DateTime.Parse(expectedDateStr);
            var timeframeCalendar = new DailyTimeframeCalendar(startTime, endTime);

            // Act
            var nextDate = await timeframeCalendar.GetNextInRangeAsync(date);

            // Assert
            Assert.That(nextDate, Is.EqualTo(expectedDate));
        }

        [Test]
        [TestCase("2022/01/01 01:00:00", "18:00:00", "02:00:00", "2022/01/01 02:00:00")] // 1 AM to same day 2 AM
        [TestCase("2022/01/02 23:00:00", "20:00:00", "04:00:00", "2022/01/03 04:00:00")] // 11 PM to next day 4 PM
        [TestCase("2022/01/03 05:00:00", "22:00:00", "06:00:00", "2022/01/03 06:00:00")] // 5 AM to same day 6 AM
        [TestCase("2022/01/04 05:00:00", "04:00:00", "06:00:00", "2022/01/04 06:00:00")] // 5 AM to same day 6 AM
        [TestCase("2022/01/05 03:00:00", "02:00:00", "04:00:00", "2022/01/05 04:00:00")] // 3 AM to same day 4 AM
        public async Task GetNextOutsideOfRange_ShouldReturnNextDateOutsideOfRange_WhenDateIsInRange(string dateStr, string startTimeStr, string endTimeStr, string expectedDateStr)
        {
            // Arrange
            var date = DateTime.Parse(dateStr);
            var startTime = TimeSpan.Parse(startTimeStr);
            var endTime = TimeSpan.Parse(endTimeStr);
            var expectedDate = DateTime.Parse(expectedDateStr);
            var timeframeCalendar = new DailyTimeframeCalendar(startTime, endTime);

            // Act
            var nextDate = await timeframeCalendar.GetNextOutsideOfRangeAsync(date);

            // Assert
            Assert.That(nextDate, Is.EqualTo(expectedDate));
        }
    }
}
