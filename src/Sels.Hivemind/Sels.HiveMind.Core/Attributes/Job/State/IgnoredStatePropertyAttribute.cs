﻿using Sels.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Sels.HiveMind.Job
{
    /// <summary>
    /// Can be put on a property of a job state to ignore it from being persisted.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class IgnoredStatePropertyAttribute : Attribute
    {

    }

    /// <summary>
    /// Contains extension methods for <see cref="IgnoredStatePropertyAttribute"/>.
    /// </summary>
    public static class IgnoredStatePropertyAttributeExtensions 
    {
        /// <summary>
        /// Checks if <paramref name="property"/> is an ignored job state property.
        /// </summary>
        /// <param name="property">The property to check</param>
        /// <returns>True if <paramref name="property"/> is an ignored job state property, otherwise false</returns>
        public static bool IsIgnoredStateProperty(this PropertyInfo property)
        {
            property.ValidateArgument(nameof(property));

            return property.GetCustomAttribute<IgnoredStatePropertyAttribute>() != null;
        }
    }
}
