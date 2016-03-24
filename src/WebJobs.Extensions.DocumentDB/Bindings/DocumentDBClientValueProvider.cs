// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBClientValueProvider : IValueProvider
    {
        private DocumentDBContext _context;

        public DocumentDBClientValueProvider(DocumentDBContext context)
        {
            _context = context;
        }

        public Type Type
        {
            get
            {
                return typeof(DocumentClient);
            }
        }

        public object GetValue()
        {
            return _context.Service.GetClient();
        }

        public string ToInvokeString()
        {
            return string.Empty;
        }
    }
}