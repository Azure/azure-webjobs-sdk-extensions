// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.EasyTables
{
    internal class EasyTableTableValueProvider<T> : IValueProvider
    {
        private ParameterInfo _parameter;
        private EasyTableContext _context;

        public EasyTableTableValueProvider(ParameterInfo parameter, EasyTableContext context)
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