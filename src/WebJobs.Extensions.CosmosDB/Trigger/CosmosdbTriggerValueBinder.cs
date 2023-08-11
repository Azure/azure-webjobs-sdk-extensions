﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerValueBinder : IValueProvider
    {
        private readonly object _value;
        private readonly ParameterInfo _parameter;
        private readonly bool _isJArray;
        private readonly bool _isString;

        public CosmosDBTriggerValueBinder(
            ParameterInfo parameter, 
            object value)
        {
            _value = value;
            _parameter = parameter;
            Type parameterType = CosmosDBTriggerAttributeBindingProviderGenerator.GetParameterType(parameter);
            _isJArray = parameterType.IsAssignableFrom(typeof(JArray));
            _isString = parameterType.IsAssignableFrom(typeof(string));
        }

        public Type Type
        {
            get
            {
                if (_isJArray 
                    || _isString)
                {
                    return typeof(IReadOnlyCollection<JObject>);
                }

                return _parameter.ParameterType;
            }
        }

        public Task<object> GetValueAsync()
        {
            if (_isString)
            {
                return Task.FromResult<object>(JArray.Parse(_value as string).ToString(Newtonsoft.Json.Formatting.None));
            }

            if (_isJArray)
            {
                return Task.FromResult<object>(JArray.FromObject(_value));
            }

            return Task.FromResult<object>(_value);
        }

        public string ToInvokeString() => string.Empty;
    }
}
