namespace Sels.HiveMind.Colony.Swarm
{
    /// <summary>
    /// Defines the behaviour of how middleware should be inherited by child swarms when they fetch jobs from parent swarms.
    /// </summary>
    public enum SwarmMiddlewareInheritanceBehaviour
    {
        /// <summary>
        /// The middleware will only be used by the drones of the swarm where the middleware is defined on.
        /// </summary>
        Exclusive = 0,
        /// <summary>
        /// Middlware can be used by child swarms when fetching work from the current swarm and optionally it's parents.
        /// </summary>
        Inherit = 1
    }
}
