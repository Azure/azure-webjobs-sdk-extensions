// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Table
{
    internal class TableEntityBinding : IBinding
    {
        private readonly BindingTemplate _dataSetNameBindingTemplate;
        private readonly BindingTemplate _tableNameBindingTemplate;
        private readonly BindingTemplate _entityIdBindingTemplate;

        public TableEntityBinding(ParameterInfo parameter, TableConfigContext configContext)
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

            if (!string.IsNullOrEmpty(attribute.EntityId))
            {
                _entityIdBindingTemplate = BindingTemplate.FromString(attribute.EntityId, ignoreCase: true);
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
                TableName = attribute.TableName,
                EntityId = attribute.EntityId
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

                if (_entityIdBindingTemplate != null)
                {
                    resolvedAttribute.EntityId = _entityIdBindingTemplate.Bind(context.BindingData);
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
            var parameterType = Parameter.ParameterType;
            var valueProviderType = typeof(TableEntityValueBinder<>).MakeGenericType(parameterType);
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
            var attribute = parameter.GetTableAttribute();
            if (string.IsNullOrEmpty(attribute.TableName))
            {
                throw new InvalidOperationException(string.Format(
                    "The attribute {0} indicates a table entity binding but the table name was not specified.",
                    typeof(ApiHubTableAttribute).Name));
            }

            var parameterType = parameter.ParameterType;
            if (!parameterType.IsClass || parameterType.IsByRef)
            {
                throw new InvalidOperationException(string.Format(
                    "The attribute {0} indicates a table entity binding. " +
                    "The parameter type must be {1} or a POCO type and must not be passed by reference. " +
                    "To bind to a table do not specify an entity identifier. " +
                    "To bind to a table client do not specify the table name.",
                    typeof(ApiHubTableAttribute).Name,
                    typeof(JObject).Name));
            }
        }

        private class TableEntityValueBinder<TEntity> : IValueBinder
            where TEntity : class
        {
            public TableEntityValueBinder(
                ParameterInfo parameter,
                ApiHubTableAttribute resolvedAttribute,
                TableConfigContext configContext)
            {
                Parameter = parameter;
                ResolvedAttribute = resolvedAttribute;
                ConfigContext = configContext;
            }

            private ParameterInfo Parameter { get; set; }
            private TableConfigContext ConfigContext { get; set; }
            private ApiHubTableAttribute ResolvedAttribute { get; set; }
            private string SerializedInputValue { get; set; }

            public Type Type
            {
                get { return Parameter.ParameterType; }
            }

            public object GetValue()
            {
                if (string.IsNullOrEmpty(ResolvedAttribute.EntityId))
                {
                    SerializedInputValue = null;
                    return null;
                }

                var table = ResolvedAttribute.GetTableReference<TEntity>(ConfigContext);
                var entityId = ResolvedAttribute.GetEntityId(ConfigContext);
                var entity = table.GetEntityAsync(entityId).Result;

                if (entity == null)
                {
                    SerializedInputValue = null;
                    return null;
                }

                SerializedInputValue = JsonConvert.SerializeObject(entity);
                return entity;
            }

            public async Task SetValueAsync(object value, CancellationToken cancellationToken)
            {
                if (SerializedInputValue == null)
                {
                    return;
                }

                var entity = value as TEntity;
                if (entity == null)
                {
                    return;
                }

                var serializedOutputValue = JsonConvert.SerializeObject(entity);
                if (string.Equals(
                    SerializedInputValue,
                    serializedOutputValue,
                    StringComparison.Ordinal))
                {
                    return;
                }

                var table = ResolvedAttribute.GetTableReference<TEntity>(ConfigContext);
                var entityId = ResolvedAttribute.GetEntityId(ConfigContext);

                await table.UpdateEntityAsync(entityId, entity, cancellationToken);
            }

            public string ToInvokeString()
            {
                return string.Empty;
            }
        }
    }
}