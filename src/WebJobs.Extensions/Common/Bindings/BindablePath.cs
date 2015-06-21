using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Extensions.Common.Bindings
{
    /// <summary>
    /// Encapsulates a path that may contain "route parameters" (e.g. "import/{name}") and provides
    /// pattern parsing and binding.
    /// </summary>
    internal class BindablePath
    {
        private BindingTemplate _bindingTemplate;

        public BindablePath(string pattern)
        {
            _bindingTemplate = BindingTemplate.FromString(pattern);
        }

        public IEnumerable<string> ParameterNames
        {
            get
            {
                return _bindingTemplate.ParameterNames;
            }
        }

        public string Bind(IReadOnlyDictionary<string, object> bindingData)
        {
            if (bindingData == null)
            {
                throw new ArgumentNullException("bindingData");
            }

            if (!_bindingTemplate.ParameterNames.Any())
            {
                return _bindingTemplate.Pattern;
            }

            IReadOnlyDictionary<string, string> parameters = BindingDataPathHelper.ConvertParameters(bindingData);
            string path = _bindingTemplate.Bind(parameters);

            return path;
        }

        public void ValidateContractCompatibility(IReadOnlyDictionary<string, Type> bindingDataContract)
        {
            IEnumerable<string> parameterNames = ParameterNames;
            if (parameterNames != null)
            {
                foreach (string parameterName in parameterNames)
                {
                    if (bindingDataContract != null && !bindingDataContract.ContainsKey(parameterName))
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "No binding parameter exists for '{0}'.", parameterName));
                    }
                }
            }
        }
    }
}
