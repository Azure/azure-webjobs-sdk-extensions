// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerValueBinder : IValueProvider
    {
        private readonly object _value;
        private readonly ParameterInfo _parameter;
        private readonly bool _isJArray;
        private readonly bool _isString;
        private readonly bool _isAllVersionsAndDeletes;

        public CosmosDBTriggerValueBinder(
            ParameterInfo parameter, 
            object value,
            CosmosDBTriggerAttribute cosmosDBAttribute = null)
        {
            _value = value;
            _parameter = parameter;
            Type parameterType = CosmosDBTriggerAttributeBindingProviderGenerator.GetParameterType(parameter);
            _isJArray = parameterType.IsAssignableFrom(typeof(JArray));
            _isString = parameterType.IsAssignableFrom(typeof(string));
            _isAllVersionsAndDeletes = cosmosDBAttribute?.ChangeFeedMode == CosmosDBChangeFeedMode.AllVersionsAndDeletes;
        }

        public Type Type
        {
            get
            {
                if (_isJArray 
                    || _isString)
                {
                    // In AllVersionsAndDeletes mode the host receives ChangeFeedItem<JObject>
                    // from the change feed processor rather than raw JObjects.
                    return _isAllVersionsAndDeletes
                        ? typeof(IReadOnlyCollection<ChangeFeedItem<JObject>>)
                        : typeof(IReadOnlyCollection<JObject>);
                }

                return _parameter.ParameterType;
            }
        }

        public Task<object> GetValueAsync()
        {
            object value;

            if (_isString)
            {
                var jArray = (_value is string) ?
                    JArray.Parse(_value as string) :
                    JArray.FromObject(_value);

                value = jArray.ToString(Formatting.None);
            }
            else if (_isJArray)
            {
                value = (_value is string) ?
                    JArray.Parse(_value as string) :
                    JArray.FromObject(_value);
            }
            else
            {
                value = (_value is string) ?
                    JsonConvert.DeserializeObject(_value as string, _parameter.ParameterType) :
                    _value;
            }
            
            return Task.FromResult(value);
        }

        public string ToInvokeString() => string.Empty;
    }
}
