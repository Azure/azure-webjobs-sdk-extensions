// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Table
{
    internal class TableEntityBinding : IBinding
    {
        public TableEntityBinding(ParameterInfo parameter, TableConfigContext configContext)
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

            var parameterType = Parameter.ParameterType;
            var valueProviderType = typeof(TableEntityValueBinder<>).MakeGenericType(parameterType);
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
                TableConfigContext configContext)
            {
                Parameter = parameter;
                ConfigContext = configContext;
            }

            private ParameterInfo Parameter { get; }
            private TableConfigContext ConfigContext { get; }
            private string SerializedInputValue { get; set; }

            public Type Type
            {
                get { return Parameter.ParameterType; }
            }

            public object GetValue()
            {
                var attribute = Parameter.GetTableAttribute();
                if (string.IsNullOrEmpty(attribute.EntityId))
                {
                    SerializedInputValue = null;
                    return null;
                }

                var table = attribute.GetTableReference<TEntity>(ConfigContext);
                var entityId = attribute.GetEntityId(ConfigContext);
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

                var attribute = Parameter.GetTableAttribute();
                var table = attribute.GetTableReference<TEntity>(ConfigContext);
                var entityId = attribute.GetEntityId(ConfigContext);

                await table.UpdateEntityAsync(entityId, entity, cancellationToken);
            }

            public string ToInvokeString()
            {
                return string.Empty;
            }
        }
    }
}