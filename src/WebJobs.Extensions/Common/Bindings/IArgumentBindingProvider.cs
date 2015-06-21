using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Common.Bindings
{
    /// <summary>
    /// Interface for a providing argument bindings.
    /// </summary>
    /// <typeparam name="TArgument">The argument type that will be bound to.</typeparam>
    internal interface IArgumentBindingProvider<TArgument>
    {
        /// <summary>
        /// Attempts to bind to the specified parameter, and returns the binding if
        /// successful, or null otherwise.
        /// </summary>
        /// <param name="parameter">The parameter to attempt to bind to.</param>
        /// <returns>The binding if successful, or null otherwise.</returns>
        IArgumentBinding<TArgument> TryCreate(ParameterInfo parameter);
    }
}
