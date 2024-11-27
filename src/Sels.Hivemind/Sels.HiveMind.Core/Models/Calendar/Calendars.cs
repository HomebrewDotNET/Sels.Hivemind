using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Calendar
{
    /// <summary>
    /// Contains the names of the built-in HiveMind calendars.
    /// </summary>
    public enum Calendars
    {
        /// <summary>
        /// The name of the calendar that includes Monday.
        /// </summary>
        Monday = 1,

        /// <summary>
        /// The name of the calendar that includes Tuesday.
        /// </summary>
        Tuesday = 2,

        /// <summary>
        /// The name of the calendar that includes Wednesday.
        /// </summary>
        Wednesday = 3,

        /// <summary>
        /// The name of the calendar that includes Thursday.
        /// </summary>
        Thursday = 4,

        /// <summary>
        /// The name of the calendar that includes Friday.
        /// </summary>
        Friday = 5,

        /// <summary>
        /// The name of the calendar that includes Saturday.
        /// </summary>
        Saturday = 6,

        /// <summary>
        /// The name of the calendar that includes Sunday.
        /// </summary>
        Sunday = 7,

        /// <summary>
        /// The name of the calendar that includes the work days of the week. (Mon - Fri)
        /// </summary>
        WorkWeek = 8,

        /// <summary>
        /// The name of the calendar that includes the weekend days. (Sat - Sun)
        /// </summary>
        Weekend = 9,

        /// <summary>
        /// The name of the calendar that defines a timeframe that starts at 9 AM and ends at 5 PM.
        /// </summary>
        NineToFive = 10,

        /// <summary>
        /// The name of the calendar that includes the first day of the month.
        /// </summary>
        StartOfMonth = 11,
        /// <summary>
        /// The name of the calendar that includes the first day of the year.
        /// </summary>
        StartOfYear = 12,
    }
}
