// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Table
{
    internal class TableClientBinding : IBinding
    {
        public TableClientBinding(
            ParameterInfo parameter, 
            TableConfigContext configContext)
        {
            ValidateParameter(parameter);

            Parameter = parameter;
            ConfigContext = configContext;
        }

        private ParameterInfo Parameter { get; set; }
        private TableConfigContext ConfigContext { get; set; }

        public bool FromAttribute
        {
            get { return true; }
        }

        public Task<IValueProvider> BindAsync(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            return BindAsync(null, context.ValueContext);
        }

        public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            IValueProvider valueProvider = new TableClientValueProvider(Parameter, ConfigContext);

            return Task.FromResult(valueProvider);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor
            {
                Name = Parameter.Name
            };
        }

        private static void ValidateParameter(ParameterInfo parameter)
        {
            var parameterType = parameter.ParameterType;
            if (parameterType != typeof(ITableClient))
            {
                throw new InvalidOperationException(string.Format(
                    "The attribute {0} indicates a table client binding. " + 
                    "The parameter type must be {1}. " +
                    "To bind to a table specify the table name. " +
                    "To bind to an entity specify the table name and the entity identifier.",
                    typeof(ApiHubTableAttribute).Name,
                    typeof(ITableClient).FullName));
            }
        }

        private class TableClientValueProvider : IValueProvider
        {
            public TableClientValueProvider(
                ParameterInfo parameter,
                TableConfigContext configContext)
            {
                Parameter = parameter;
                ConfigContext = configContext;
            }

            private ParameterInfo Parameter { get; set; }
            private TableConfigContext ConfigContext { get; set; }

            public Type Type
            {
                get { return Parameter.ParameterType; }
            }

            public object GetValue()
            {
                var attribute = Parameter.GetTableAttribute();
                var tableClient = attribute.GetTableClient(ConfigContext);

                return tableClient;
            }

            public string ToInvokeString()
            {
                return string.Empty;
            }
        }
    }
}