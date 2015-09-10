// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Extensions.Framework
{
    /// <summary>
    /// Encapsulates a path that may contain "route parameters" (e.g. "import/{name}") and provides
    /// pattern parsing and binding.
    /// </summary>
    public class BindablePath
    {
        private readonly BindingTemplate _bindingTemplate;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="pattern"></param>
        public BindablePath(string pattern)
        {
            _bindingTemplate = BindingTemplate.FromString(pattern);
        }

        /// <summary>
        /// Gets the names of the parameters in the binding expression.
        /// </summary>
        public IEnumerable<string> ParameterNames
        {
            get
            {
                return _bindingTemplate.ParameterNames;
            }
        }

        /// <summary>
        /// Bind using the specified binding data.
        /// </summary>
        /// <param name="bindingData"></param>
        /// <returns></returns>
        public string Bind(IReadOnlyDictionary<string, object> bindingData)
        {
            if (bindingData == null ||
                !_bindingTemplate.ParameterNames.Any())
            {
                return _bindingTemplate.Pattern;
            }

            IReadOnlyDictionary<string, string> parameters = BindingDataPathHelper.ConvertParameters(bindingData);
            string path = _bindingTemplate.Bind(parameters);

            return path;
        }

        /// <summary>
        /// Validate the binding contract.
        /// </summary>
        /// <param name="bindingDataContract"></param>
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
