// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    /// <summary>
    /// File Triggers implement this.
    /// </summary>
    /// <typeparam name="TFile"></typeparam>
    internal interface IFileTriggerStrategy<TFile>
    {
        /// <summary>
        /// Get the path from the file. 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        string GetPath(TFile file);

        /// <summary>
        /// Get the binding contract. Mutable so that we can add to this. 
        /// </summary>
        /// <param name="contract"></param>
        void GetStaticBindingContract(IDictionary<string, Type> contract);

        /// <summary>
        /// Add runtime information to the contract. 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="contract"></param>
        void GetRuntimeBindingContract(TFile file, IDictionary<string, object> contract);
    }
}