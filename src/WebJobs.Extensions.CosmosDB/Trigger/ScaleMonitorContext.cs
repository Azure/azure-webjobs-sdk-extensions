// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger
{
    public class ScaleMonitorContext
    {
        // plan to obsolute
        private IDictionary<string, string> _config = new Dictionary<string, string>();

        public ILogger Logger { get; set; }

        public string TriggerData { get; set; }

        public string ExtensionOptions { get; set; } // payload of admin/host/config API
        
        public IDictionary<string, string> AppSettings { get; set; }

        public string FunctionName { get; set; }

        public List<ManagedIdentityInformation> ManagedIdentities { get; set; }

        public IConfiguration Configration { get; set; } // Convert from AppSettings

        public INameResolver NameResolver
        {
            get
            {
                return new DefaultNameResolver(Configration); // consider caching or immutable
            } 
        }

        public string this[string key]
        {
            get { return _config[key]; }
        }

        public T GetTriggerAttribute<T>()
        {
            // Write a logic hydrate T from TriggerData
            return default(T);
        }

        public T GetExtensionOption<T>()
        {
            // Write a logic hydrate K from ExtensionOption
            return default(T);
        }

        public void Add(string key, string value)
        {
            _config.Add(key, value);
        } 
    }
}
