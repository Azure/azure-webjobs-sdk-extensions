// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub.Sdk.Table;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Extensions;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Table
{
    internal class TableBinding : IBinding
    {
        public TableBinding(
            ParameterInfo parameter, 
            TableConfigContext configContext)
        {
            ValidateParameter(parameter);

            Parameter = parameter;
            ConfigContext = configContext;
        }

        private ParameterInfo Parameter { get; }
        private TableConfigContext ConfigContext { get; }

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

            var entityType = Parameter.ParameterType.GetGenericArguments().Single();
            var valueProviderType = typeof(TableValueProvider<>).MakeGenericType(entityType);
            var valueProvider = (IValueProvider)Activator.CreateInstance(
                valueProviderType, Parameter, ConfigContext);

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
            var genericTypeDefinition =
                parameter.ParameterType.IsGenericType
                    ? parameterType.GetGenericTypeDefinition()
                    : null;

            if (genericTypeDefinition != typeof(ITable<>) &&
                genericTypeDefinition != typeof(IAsyncCollector<>))
            {
                throw new InvalidOperationException(string.Format(
                    "The attribute {0} indicates a table binding. " +
                    "The parameter type must be one of the following: {1}. " +
                    "To bind to a table client do not specify a table name. " +
                    "To bind to an entity specify the entity identifier.",
                    typeof(ApiHubTableAttribute).Name,
                    string.Join(", ",
                        typeof(ITable<>).GetGenericTypeDisplayName("TEntity"),
                        typeof(IAsyncCollector<>).GetGenericTypeDisplayName("TEntity"))));
            }
        }

        private class TableValueProvider<TEntity> : IValueProvider
            where TEntity : class
        {
            public TableValueProvider(ParameterInfo parameter, TableConfigContext configContext)
            {
                Parameter = parameter;
                ConfigContext = configContext;
            }

            private ParameterInfo Parameter { get; }
            private TableConfigContext ConfigContext { get; }

            public Type Type
            {
                get { return Parameter.ParameterType; }
            }

            public object GetValue()
            {
                var attribute = Parameter.GetTableAttribute();
                var table = attribute.GetTableReference<TEntity>(ConfigContext);
                var parameterType = Parameter.ParameterType;
                var genericTypeDefinition = parameterType.GetGenericTypeDefinition();

                if (genericTypeDefinition == typeof(IAsyncCollector<>))
                {
                    return new TableAsyncCollector<TEntity>(table);
                }

                return table;
            }

            public string ToInvokeString()
            {
                return string.Empty;
            }
        }
    }
}