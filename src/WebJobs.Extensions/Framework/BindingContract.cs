using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Extensions.Framework
{
    /// <summary>
    /// Helper class for handling binding contracts.
    /// </summary>
    public class BindingContract
    {
        private BindingTemplateSource _bindingTemplateSource;
        private IReadOnlyDictionary<string, Type> _bindingDataContract;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="pattern">The binding template pattern.</param>
        /// <param name="builtInContract">The initial built in contract.</param>
        public BindingContract(string pattern, Dictionary<string, Type> builtInContract)
        {
            _bindingTemplateSource = BindingTemplateSource.FromString(pattern);
            _bindingDataContract = CreateBindingDataContract(builtInContract);
        }

        /// <summary>
        /// Gets the binding contract
        /// </summary>
        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get
            {
                return _bindingDataContract;
            }
        }

        /// <summary>
        /// Extracts binding data from the specified value.
        /// </summary>
        /// <param name="value">The value to extract data from.</param>
        /// <param name="builtInContract">The built in contract values.</param>
        /// <returns>The binding data.</returns>
        public IReadOnlyDictionary<string, object> GetBindingData(string value, Dictionary<string, object> builtInContract)
        {
            if (builtInContract == null)
            {
                throw new ArgumentNullException("builtInContract");
            }

            IReadOnlyDictionary<string, object> bindingDataFromTemplate = _bindingTemplateSource.CreateBindingData(value);
            if (bindingDataFromTemplate != null)
            {
                foreach (KeyValuePair<string, object> item in bindingDataFromTemplate)
                {
                    // In case of conflict, binding data from the template overrides the built-in binding data
                    builtInContract[item.Key] = item.Value;
                }
            }
            return builtInContract;
        }

        private IReadOnlyDictionary<string, Type> CreateBindingDataContract(Dictionary<string, Type> builtInContract)
        {
            // get any binding contract members from the binding template
            Dictionary<string, Type> contractFromTemplate = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (string parameterName in _bindingTemplateSource.ParameterNames)
            {
                contractFromTemplate.Add(parameterName, typeof(string));
            }

            foreach (KeyValuePair<string, Type> item in contractFromTemplate)
            {
                // In case of conflict, binding data from the template overrides built in binding data
                builtInContract[item.Key] = item.Value;
            }

            return builtInContract;
        }
    }
}
