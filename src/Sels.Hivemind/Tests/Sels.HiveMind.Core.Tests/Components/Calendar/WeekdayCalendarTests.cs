using Sels.HiveMind.Calendar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Core.Tests.Components.Calendar
{
    public class WeekdayCalendarTests
    {
        [Test]
        [TestCase("2022/01/03", new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, true)] // Monday
        [TestCase("2022/01/04", new[] { DayOfWeek.Monday, DayOfWeek.Friday }, false)] // Tuesday
        [TestCase("2022/01/05", new[] { DayOfWeek.Wednesday, DayOfWeek.Friday }, true)] // Wednesday
        [TestCase("2022/01/06", new[] { DayOfWeek.Tuesday, DayOfWeek.Friday }, false)] // Thursday
        [TestCase("2022/01/07", new[] { DayOfWeek.Monday, DayOfWeek.Friday }, true)] // Friday
        [TestCase("2022/01/08", new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }, false)] // Saturday
        public async Task IsInRange_ShouldReturnExpectedResult_WhenDateIsProvided(string dateStr, DayOfWeek[] daysOfWeek, bool expectedResult)
        {
            // Arrange
            var date = DateTime.Parse(dateStr);
            var weekdayCalendar = new WeekdayCalendar(new List<DayOfWeek>(daysOfWeek));

            // Act
            var result = await weekdayCalendar.IsInRangeAsync(date);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        [TestCase("2022/01/04", new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, "2022/01/05")] // Tuesday to Wednesday
        [TestCase("2022/01/06", new[] { DayOfWeek.Monday, DayOfWeek.Friday }, "2022/01/07")] // Thursday to Friday
        [TestCase("2022/01/04", new[] { DayOfWeek.Tuesday }, "2022/01/04")] //Same day (Tuesday)
        [TestCase("2022/01/08", new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }, "2022/01/10")] // Saturday to Monday
        public async Task GetNextInRange_ShouldReturnNextDateInRange_WhenDateIsNotInRange(string dateStr, DayOfWeek[] daysOfWeek, string expectedDateStr)
        {
            // Arrange
            var date = DateTime.Parse(dateStr);
            var expectedDate = DateTime.Parse(expectedDateStr);
            var weekdayCalendar = new WeekdayCalendar(new List<DayOfWeek>(daysOfWeek));

            // Act
            var nextDate = await weekdayCalendar.GetNextInRangeAsync(date);

            // Assert
            Assert.That(nextDate, Is.EqualTo(expectedDate));
        }

        [Test]
        [TestCase("2022/01/03", new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, "2022/01/04")] // Monday to Tuesday
        [TestCase("2022/01/05", new[] { DayOfWeek.Monday, DayOfWeek.Friday }, "2022/01/05")] // Same day (Wednesday)
        [TestCase("2022/01/07", new[] { DayOfWeek.Wednesday, DayOfWeek.Friday }, "2022/01/08")] // Friday to Saturday
        [TestCase("2022/01/09", new[] { DayOfWeek.Thursday, DayOfWeek.Sunday }, "2022/01/10")] // Saturday to Monday
        public async Task GetNextOutsideOfRange_ShouldReturnNextDateOutsideOfRange_WhenDateIsInRange(string dateStr, DayOfWeek[] daysOfWeek, string expectedDateStr)
        {
            // Arrange
            var date = DateTime.Parse(dateStr);
            var expectedDate = DateTime.Parse(expectedDateStr);
            var weekdayCalendar = new WeekdayCalendar(new List<DayOfWeek>(daysOfWeek));

            // Act
            var nextDate = await weekdayCalendar.GetNextOutsideOfRangeAsync(date);

            // Assert
            Assert.That(nextDate, Is.EqualTo(expectedDate));
        }
    }
}
