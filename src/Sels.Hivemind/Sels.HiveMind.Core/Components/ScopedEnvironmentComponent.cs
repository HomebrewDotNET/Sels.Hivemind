using Microsoft.Extensions.DependencyInjection;
using Sels.Core.Extensions;
using Sels.Core.Extensions.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sels.HiveMind
{
    /// <summary>
    /// <inheritdoc cref="IEnvironmentComponent{T}"/> that is scoped to a <see cref="AsyncServiceScope"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ScopedEnvironmentComponent<T> : ScopedComponent<T>, IEnvironmentComponent<T> where T : class
    {
        // Properties
        /// <inheritdoc/>
        public string Environment { get; }

        /// <inheritdoc cref="ScopedEnvironmentComponent{T}"/>
        /// <param name="environment"><inheritdoc cref="Environment"/></param>
        /// <param name="component"><inheritdoc cref="Component"/></param>
        /// <param name="scope">The service scope used to resolve <paramref name="component"/></param>
        public ScopedEnvironmentComponent(string environment, T component, AsyncServiceScope scope) : base(component, scope)
        {
            Environment = environment.ValidateArgumentNotNullOrWhitespace(nameof(environment));
        }

    }
}
