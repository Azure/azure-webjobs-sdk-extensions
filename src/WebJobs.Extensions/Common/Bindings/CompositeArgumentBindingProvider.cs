using System.Collections.Generic;
using System.Reflection;
using Microsoft.Azure.WebJobs.Extensions.Common.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Common.Bindings
{
    /// <summary>
    /// Composes a collection of binding providers, and delegates to each when attempting to bind.
    /// </summary>
    /// <typeparam name="TArgument">The argument type to bind to.</typeparam>
    internal class CompositeArgumentBindingProvider<TArgument> : IArgumentBindingProvider<TArgument>
    {
        private readonly IEnumerable<IArgumentBindingProvider<TArgument>> _providers;

        public CompositeArgumentBindingProvider(params IArgumentBindingProvider<TArgument>[] providers)
        {
            _providers = providers;
        }

        public IArgumentBinding<TArgument> TryCreate(ParameterInfo parameter)
        {
            foreach (var provider in _providers)
            {
                var binding = provider.TryCreate(parameter);
                if (binding != null)
                {
                    return binding;
                }
            }

            return null;
        }
    }
}
