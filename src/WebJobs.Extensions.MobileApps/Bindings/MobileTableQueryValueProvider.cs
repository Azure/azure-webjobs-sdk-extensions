// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    internal class MobileTableQueryValueProvider<T> : IValueProvider
    {
        private ParameterInfo _parameter;
        private MobileTableContext _context;

        public MobileTableQueryValueProvider(ParameterInfo parameter, MobileTableContext context)
        {
            _parameter = parameter;
            _context = context;
        }

        public Type Type
        {
            get { return _parameter.ParameterType; }
        }

        public object GetValue()
        {
            Type paramType = this.Type;

            if (paramType.IsGenericType &&
                paramType.GetGenericTypeDefinition() == typeof(IMobileServiceTableQuery<>))
            {
                // If TableName is specified, add it to the internal table cache. Now items of this type
                // will operate on the specified TableName.
                if (!string.IsNullOrEmpty(_context.ResolvedTableName))
                {
                    _context.Client.AddToTableNameCache(typeof(T), _context.ResolvedTableName);
                }

                IMobileServiceTable<T> table = _context.Client.GetTable<T>();
                return table.CreateQuery();
            }

            return null;
        }

        public string ToInvokeString()
        {
            return string.Empty;
        }
    }
}