// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    internal class MobileTableTableValueProvider<T> : IValueProvider
    {
        private ParameterInfo _parameter;
        private MobileTableContext _context;

        public MobileTableTableValueProvider(ParameterInfo parameter, MobileTableContext context)
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
            Type paramType = _parameter.ParameterType;

            if (paramType == typeof(IMobileServiceTable))
            {
                return _context.Client.GetTable(_context.ResolvedTableName);
            }

            if (paramType.IsGenericType &&
                paramType.GetGenericTypeDefinition() == typeof(IMobileServiceTable<>))
            {
                // If TableName is specified, add it to the internal table cache. Now items of this type
                // will operate on the specified TableName.
                if (!string.IsNullOrEmpty(_context.ResolvedTableName))
                {
                    _context.Client.AddToTableNameCache(typeof(T), _context.ResolvedTableName);
                }
                return _context.Client.GetTable<T>();
            }

            return null;
        }

        public string ToInvokeString()
        {
            return string.Empty;
        }
    }
}