using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Protocols;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    // File Triggers implement this.
    public interface IFileTriggerStrategy<TFile>
    {
        // Get the path from the file. 
        string GetPath(TFile file);

        // Get the binding contract. Mutable so that we can add to this. 
        void GetStaticBindingContract(IDictionary<string, Type> contract);

        // Add runtime information to the contract. 
        void GetRuntimeBindingContract(TFile file, IDictionary<string, object> contract);
    }

    // Provde Read/Write streams. 
    // $$$ cOuld this be rationalized with IConverter?
    public interface IFileStreamProvider
    {
        // Get a stream that reads from the source.
        Task<Stream> OpenReadStreamAsync();

        // Stream and a "Completion" function to be called when finished writing to the stream. 
        // That can be used to flush results. 
        // Technically, the a derived stream could override Close() to call OnComplete, but that's 
        // hard for callers to implement. 
        Task<Tuple<Stream, Func<Task>>> OpenWriteStreamAsync();
    }

    // Provide a way to get to the  common file-properties on file attributes. 
    public interface IFileAttribute
    {
        // Path with resolution 
        // allow set so we can update this attribute with resolved parameters. 
        string Path { get; set; }
        FileAccess Access { get; }
    }

    // Provide backdoor so that trigger binder can call regular binder and have consistent semantics. 
    // $$$ Only needed because Core-sdk 
    public interface IBindingProvider2
    {
        Task<IBinding> BindDirect(BindingProviderContext context);
    }
}