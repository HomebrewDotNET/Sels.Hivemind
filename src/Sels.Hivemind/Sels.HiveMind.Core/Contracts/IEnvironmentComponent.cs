﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Sels.HiveMind
{
    /// <summary>
    /// Represents a component of type <typeparamref name="T"/> created for a HiveMind environment.
    /// </summary>
    /// <typeparam name="T">The type of component</typeparam>
    public interface IEnvironmentComponent<T> : IComponent <T>
        where T : class
    {
        /// <summary>
        /// The HiveMind environment the component is created for.
        /// </summary>
        string Environment { get; }
    }
}
