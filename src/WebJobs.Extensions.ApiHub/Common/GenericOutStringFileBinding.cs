using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    // $$$ Support Out TFile,  Out T, using the ConverterManager
    // 1 per ParameterInfo
    // Bind to 'out string' 
    class GenericOutStringFileBinding<TAttribute, TFile> : IBinding
        where TAttribute : Attribute, IFileAttribute
    {
        private readonly BindingTemplate _bindingTemplate;
        private readonly Func<TAttribute, Task<TFile>> _strategyBuilder;
        private readonly TAttribute _source;
        private readonly IConverterManager _converterManager;

        public GenericOutStringFileBinding(
            BindingTemplate bindingTemplate,
            TAttribute source,
            Func<TAttribute, Task<TFile>> strategyBuilder,
            IConverterManager converterManager)
        {
            this._source = source;
            this._bindingTemplate = bindingTemplate;
            this._strategyBuilder = strategyBuilder;
            this._converterManager = converterManager;
        }

        public bool FromAttribute
        {
            get
            {
                return true;
            }
        }

        public Task<IValueProvider> BindAsync(BindingContext context)
        {
            string boundFileName = _bindingTemplate.Bind(context.BindingData);

            return BindAsync(boundFileName, context.ValueContext);
        }

        public async Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            string path = (string)value;
            IFileStreamProvider strategy = await GetStreamHelper(path);

            var outTuple = await strategy.OpenWriteStreamAsync();
            Stream outStream = outTuple.Item1;
            Func<Task> completedFunc = outTuple.Item2;

            // Bind to 'out string'
            var valueProvider = new GenericOutFileValueProvider(outStream, completedFunc);
            return valueProvider;
        }

        // Get a stream binder from a resolved path
        private async Task<IFileStreamProvider> GetStreamHelper(string path)
        {
            TAttribute clone = CloneAttributeWithResolvedPath(path);

            TFile nativeFile = await this._strategyBuilder(clone);
            var func = this._converterManager.GetConverter<TFile, IFileStreamProvider>();
            IFileStreamProvider strategy = func(nativeFile);
            return strategy;
        }

        private TAttribute CloneAttributeWithResolvedPath(string path)
        {
            TAttribute clone = JsonConvert.DeserializeObject<TAttribute>(JsonConvert.SerializeObject(_source));
            clone.Path = path;
            return clone;
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor { }; // $$$
        }

        class GenericOutFileValueProvider : ConstantObj, IValueBinder
        {
            private readonly Stream _outStream;
            private Func<Task> _completedFunc;

            public GenericOutFileValueProvider(Stream outStream, Func<Task> completedFunc)
            {
                this._outStream = outStream;
                this._completedFunc = completedFunc;
                this.Type = typeof(string).MakeByRefType();
            }

            public async Task SetValueAsync(object value, CancellationToken cancellationToken)
            {
                string contents = (string)value;

                var tw = new StreamWriter(_outStream);
                tw.Write(contents);
                tw.Flush();
                await _completedFunc();
            }
        }
    }
}