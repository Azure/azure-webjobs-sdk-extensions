// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Tables;
using Microsoft.WindowsAzure.Storage.Table;

namespace Sample.Extension
{
    internal class SampleTableBindingProvider : IArgumentBindingProvider<ITableArgumentBinding>
    {
        public ITableArgumentBinding TryCreate(ParameterInfo parameter)
        {
            // Determine whether the target is a Table parameter that we should bind to
            TableAttribute tableAttribute = parameter.GetCustomAttribute<TableAttribute>(inherit: false);
            if (tableAttribute == null ||
                !parameter.ParameterType.IsGenericType ||
                (parameter.ParameterType.GetGenericTypeDefinition() != typeof(Table<>)))
            {
                return null;
            }

            // create the binding
            Type elementType = GetItemType(parameter.ParameterType);
            Type bindingType = typeof(Binding<>).MakeGenericType(elementType);

            return (ITableArgumentBinding)Activator.CreateInstance(bindingType);
        }

        private static Type GetItemType(Type queryableType)
        {
            Type[] genericArguments = queryableType.GetGenericArguments();
            var itemType = genericArguments[0];
            return itemType;
        }

        private class Binding<TElement> : ITableArgumentBinding
        {
            public FileAccess Access
            {
                get { return FileAccess.ReadWrite; }
            }

            public Type ValueType
            {
                get
                {
                    return typeof(Table<TElement>);
                }
            }

            public Task<IValueProvider> BindAsync(CloudTable value, ValueBindingContext context)
            {
                return Task.FromResult<IValueProvider>(new ValueBinder(value));
            }

            private class ValueBinder : IValueBinder
            {
                private readonly CloudTable _table;

                public ValueBinder(CloudTable table)
                {
                    _table = table;
                }

                public Type Type
                {
                    get { return typeof(Table<TElement>); }
                }

                public object GetValue()
                {
                    return new Table<TElement>(_table);
                }

                public Task SetValueAsync(object value, CancellationToken cancellationToken)
                {
                    // this is where any queued up storage operations can be flushed
                    Table<TElement> tableBinding = value as Table<TElement>;
                    return tableBinding.FlushAsync(cancellationToken);
                }

                public string ToInvokeString()
                {
                    return _table.Name;
                }
            }
        }
    }
}
