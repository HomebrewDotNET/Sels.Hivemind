namespace Sels.HiveMind.Query
{
    /// <summary>
    /// Determines how a property should be queried.
    /// </summary>
    public enum PropertyConditionQueryType
    {
        /// <summary>
        /// Value of the property should be compared.
        /// </summary>
        Value = 0,
        /// <summary>
        /// Condition should check if property exists.
        /// </summary>
        Exists = 1,
        /// <summary>
        /// Condition should check if property is missing.
        /// </summary>
        NotExists = 2
    }
}
