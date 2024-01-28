using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind.Storage
{
    /// <summary>
    /// Enum that indicates how certain state is stored and can be queried.
    /// </summary>
    public enum StorageType
    {
        /// <summary>
        /// Value is stored as a number. (<see cref="long"/>)
        /// </summary>
        Number = 0,
        /// <summary>
        /// Value is stored as a floating point number. (<see cref="double"/>)
        /// </summary>
        FloatingNumber = 1,
        /// <summary>
        /// Value is stored as a date. (<see cref="DateTime"/>)
        /// </summary>
        Date = 2,
        /// <summary>
        /// Value is stored as text. (<see cref="string"/>)
        /// </summary>
        Text = 3,
        /// <summary>
        /// Value is serialized depending on it's source type. Cannot be queried.
        /// </summary>
        Serialized = 4
    }
}
