// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Extensions;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Table
{
    internal class TableBinding : IBinding
    {
        private readonly BindingTemplate _dataSetNameBindingTemplate;
        private readonly BindingTemplate _tableNameBindingTemplate;

        public TableBinding(
            ParameterInfo parameter, 
            TableConfigContext configContext)
        {
            ValidateParameter(parameter);

            Parameter = parameter;
            ConfigContext = configContext;

            ApiHubTableAttribute attribute = Parameter.GetTableAttribute();
            if (!string.IsNullOrEmpty(attribute.DataSetName))
            {
                _dataSetNameBindingTemplate = BindingTemplate.FromString(attribute.DataSetName, ignoreCase: true);
            }

            if (!string.IsNullOrEmpty(attribute.TableName))
            {
                _tableNameBindingTemplate = BindingTemplate.FromString(attribute.TableName, ignoreCase: true);
            }
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

            var attribute = Parameter.GetTableAttribute();
            var resolvedAttribute = new ApiHubTableAttribute(attribute.Connection)
            {
                DataSetName = attribute.DataSetName,
                TableName = attribute.TableName
            };

            if (context.BindingData != null)
            {
                if (_dataSetNameBindingTemplate != null)
                {
                    resolvedAttribute.DataSetName = _dataSetNameBindingTemplate.Bind(context.BindingData);
                }

                if (_tableNameBindingTemplate != null)
                {
                    resolvedAttribute.TableName = _tableNameBindingTemplate.Bind(context.BindingData);
                }
            }

            return BindAsync(resolvedAttribute);
        }

        public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            // TODO: Add support for Dashboard string invoke

            var attribute = Parameter.GetTableAttribute();
            return BindAsync(attribute);
        }

        private Task<IValueProvider> BindAsync(ApiHubTableAttribute resolvedAttribute)
        {
            var entityType = Parameter.ParameterType.GetGenericArguments().Single();
            var valueProviderType = typeof(TableValueProvider<>).MakeGenericType(entityType);
            var valueProvider = (IValueProvider)Activator.CreateInstance(
                valueProviderType, Parameter, resolvedAttribute, ConfigContext);

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
            public TableValueProvider(
                ParameterInfo parameter,
                ApiHubTableAttribute resolvedAttribute,
                TableConfigContext configContext)
            {
                Parameter = parameter;
                ResolvedAttribute = resolvedAttribute;
                ConfigContext = configContext;
            }

            private ParameterInfo Parameter { get; set; }
            private ApiHubTableAttribute ResolvedAttribute { get; set; }
            private TableConfigContext ConfigContext { get; set; }

            public Type Type
            {
                get { return Parameter.ParameterType; }
            }

            public object GetValue()
            {
                var table = ResolvedAttribute.GetTableReference<TEntity>(ConfigContext);
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