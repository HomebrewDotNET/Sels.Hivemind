using Sels.HiveMind.Calendar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind.Core.Tests.Components.Calendar
{
    public class DayCalendarTests
    {
        [Test]
        public async Task IsInRange_ShouldReturnTrue_WhenDateIsInRange()
        {
            // Arrange
            var calendar = new DayCalendar((1, 1, 2022), (3, 1, 2022), (5, 1, 2022));

            // Act
            var result = await calendar.IsInRangeAsync(new DateTime(2022, 1, 3));

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public async Task IsInRange_ShouldReturnFalse_WhenDateIsNotInRange()
        {
            // Arrange
            var calendar = new DayCalendar((1, 1, 2022), (3, 1, 2022), (5, 1, 2022));

            // Act
            var result = await calendar.IsInRangeAsync(new DateTime(2022, 1, 4));

            // Assert
            Assert.IsFalse(result);
        }      
        [Test]
        public async Task IsInRange_ShouldHandleNullMonthAndYear()
        {
            // Arrange
            var calendar = new DayCalendar((1, null, null), (31, null, null), (1, null, null));

            // Act
            var result = await calendar.IsInRangeAsync(new DateTime(1, 1, 1));

            // Assert
            Assert.IsTrue(result);
        }
        [Test]
        public async Task IsInRange_ShouldHandleNullMonth()
        {
            // Arrange
            var calendar = new DayCalendar((1, null, 2022), (31, null, 2022), (1, null, 2023));

            // Act
            var result = await calendar.IsInRangeAsync(new DateTime(2022, 1, 31));

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public async Task IsInRange_ShouldHandleNullYear()
        {
            // Arrange
            var calendar = new DayCalendar((1, 1, null), (31, 12, null), (1, 1, null));

            // Act
            var result = await calendar.IsInRangeAsync(new DateTime(1, 1, 1));

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public async Task GetNextInRange_ShouldReturnNextDateInRange_WhenDateIsNotInRange()
        {
            // Arrange
            var calendar = new DayCalendar((1, 1, 2022), (3, 1, 2022), (5, 1, 2022));

            // Act
            var result = await calendar.GetNextInRangeAsync(new DateTime(2022, 1, 4));

            // Assert
            Assert.That(result, Is.EqualTo(new DateTime(2022, 1, 5)));
        }
        [Test]
        public async Task GetNextInRange_ShouldHandleNullMonthAndYear()
        {
            // Arrange
            var calendar = new DayCalendar((1, null, null), (30, null, null), (15, null, null));

            // Act
            var result = await calendar.GetNextInRangeAsync(new DateTime(2023, 1, 12));

            // Assert
            Assert.That(result, Is.EqualTo(new DateTime(2023, 1, 15)));
        }
        [Test]
        public async Task GetNextInRange_ShouldHandleNullMonth()
        {
            // Arrange
            var calendar = new DayCalendar((1, null, 2022), (31, null, 2022), (1, null, 2023));

            // Act
            var result = await calendar.GetNextInRangeAsync(new DateTime(2022, 1, 2));

            // Assert
            Assert.That(result, Is.EqualTo(new DateTime(2022, 1, 31)));
        }

        [Test]
        public async Task GetNextInRange_ShouldHandleNullYear()
        {
            // Arrange
            var calendar = new DayCalendar((1, 1, null), (31, 12, null), (1, 5, null));

            // Act
            var result = await calendar.GetNextInRangeAsync(new DateTime(2020, 1, 15));

            // Assert
            Assert.That(result, Is.EqualTo(new DateTime(2020, 5, 1)));
        }
        [Test]
        public async Task GetNextOutsideOfRange_ShouldReturnNextDateOutsideOfRange_WhenDateIsInRange()
        {
            // Arrange
            var calendar = new DayCalendar((1, 1, 2022), (3, 1, 2022), (5, 1, 2022));

            // Act
            var result = await calendar.GetNextOutsideOfRangeAsync(new DateTime(2022, 1, 3));

            // Assert
            Assert.That(result, Is.EqualTo(new DateTime(2022, 1, 4)));
        }

        [Test]
        public async Task GetNextOutsideOfRange_ShouldReturnFirstDayOfNextMonth_WhenDateIsLastDayOfMonth()
        {
            // Arrange
            var calendar = new DayCalendar((1, 1, 2022), (31, 1, 2022), (1, 2, 2023));

            // Act
            var result = await calendar.GetNextOutsideOfRangeAsync(new DateTime(2022, 1, 31));

            // Assert
            Assert.That(result, Is.EqualTo(new DateTime(2022, 2, 1)));
        }

        [Test]
        public async Task GetNextOutsideOfRange_ShouldReturnFirstDayOfNextYear_WhenDateIsLastDayOfYear()
        {
            // Arrange
            var calendar = new DayCalendar((1, 1, 2022), (31, 12, 2022));

            // Act
            var result = await calendar.GetNextOutsideOfRangeAsync(new DateTime(2022, 12, 31));

            // Assert
            Assert.That(result, Is.EqualTo(new DateTime(2023, 1, 1)));
        }
        [Test]
        public async Task GetNextOutsideOfRange_ShouldHandleNullMonthAndYear()
        {
            // Arrange
            var calendar = new DayCalendar((1, null, null), (31, null, null), (20, null, null));

            // Act
            var result = await calendar.GetNextOutsideOfRangeAsync(new DateTime(2016, 1, 2));

            // Assert
            Assert.That(result, Is.EqualTo(new DateTime(2016, 1, 2)));
        }
        [Test]
        public async Task GetNextOutsideOfRange_ShouldHandleNullMonth()
        {
            // Arrange
            var calendar = new DayCalendar((1, null, 2022), (31, null, 2022), (1, null, 2023));

            // Act
            var result = await calendar.GetNextOutsideOfRangeAsync(new DateTime(2022, 1, 1));

            // Assert
            Assert.That(result, Is.EqualTo(new DateTime(2022, 1, 2)));
        }

        [Test]
        public async Task GetNextOutsideOfRange_ShouldHandleNullYear()
        {
            // Arrange
            var calendar = new DayCalendar((1, 1, null), (31, 12, null), (15, 6, null));

            // Act
            var result = await calendar.GetNextOutsideOfRangeAsync(new DateTime(2019, 6, 15));

            // Assert
            Assert.That(result, Is.EqualTo(new DateTime(2019, 6, 16)));
        }
    }
}
