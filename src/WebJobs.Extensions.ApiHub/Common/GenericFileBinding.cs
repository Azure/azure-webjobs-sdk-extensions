using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    internal class GenericFileBinding<TAttribute, TFile> : IBinding
        where TAttribute : Attribute, IFileAttribute
    {
        private readonly BindingTemplate _bindingTemplate;
        private readonly Func<TAttribute, Task<TFile>> _strategyBuilder;
        private readonly TAttribute _source;
        private readonly IConverterManager _converterManager;
        private readonly ParameterInfo _parameter;


        public GenericFileBinding(
            ParameterInfo parameter,
            BindingTemplate bindingTemplate,
            TAttribute source,
            Func<TAttribute, Task<TFile>> strategyBuilder,
            IConverterManager converterManager)
        {
            this._parameter = parameter;
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
            return this.BindAsync(boundFileName, context.ValueContext);
        }

        public async Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            string path = (string)value;

            IFileStreamProvider strategy = await GetStreamHelper(path);

            var targetType = _parameter.ParameterType;

            object userObj;
            Func<Task> onComplete;

            if ((_source.Access == FileAccess.Write) || (targetType == typeof(TextWriter)))
            {
                var tuple = await strategy.OpenWriteStreamAsync();
                var writeStream = tuple.Item1;
                var saveStreamFunc = tuple.Item2;

                if (targetType == typeof(Stream))
                {
                    userObj = writeStream;
                    onComplete = saveStreamFunc;
                }
                else if (targetType == typeof(TextWriter))
                {
                    var tw = new StreamWriter(writeStream);
                    userObj = tw;
                    onComplete = async () =>
                    {
                        tw.Flush();
                        await saveStreamFunc();
                    };
                }
                else
                {
                    // $$$ Move this error up to the binder level.
                    // Unknown?
                    throw new InvalidOperationException("Unsupported type:" + targetType.FullName);
                }
            }
            else
            {
                var readStream = await strategy.OpenReadStreamAsync();
                onComplete = () => Task.FromResult(0); // Nop
                if (targetType == typeof(Stream))
                {
                    userObj = readStream;
                }
                else if (targetType == typeof(TextReader))
                {
                    userObj = new StreamReader(readStream);
                }
                else if (targetType == typeof(string))
                {
                    userObj = new StreamReader(readStream).ReadToEnd();
                }
                else 
                {
                    throw new InvalidOperationException("Unspported type:" + targetType.FullName);
                }
            }
                        
            
            //var text = await Convert(targetType, _strategy, path);

            var valueProvider = new ConstantObj
            {
                Type = targetType,
                _value = userObj,
                _onCompleted = onComplete
            };
            return valueProvider;
        }


        // Get a stream binder from a resolved path
        private async Task<IFileStreamProvider> GetStreamHelper(string path)
        {
            TAttribute clone = JsonConvert.DeserializeObject<TAttribute>(JsonConvert.SerializeObject(_source));
            clone.Path = path;

            TFile nativeFile = await this._strategyBuilder(clone);
            var func = this._converterManager.GetConverter<TFile, IFileStreamProvider>();
            IFileStreamProvider strategy = func(nativeFile);
            return strategy;
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor { };
        }


    }

}