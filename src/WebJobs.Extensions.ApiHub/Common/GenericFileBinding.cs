// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Newtonsoft.Json;

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

            object userObj = null;
            Func<object, Task> onComplete;
            ConstantObj valueProvider = null;

            if (_source.Access == FileAccess.Write) 
            {
                var tuple = await strategy.OpenWriteStreamAsync();
                var writeStream = tuple.Item1;
                var saveStreamFunc = tuple.Item2;

                if (targetType == typeof(Stream) || (_parameter.IsOut && targetType == typeof(Stream).MakeByRefType()))
                {
                    userObj = writeStream;
                    onComplete = saveStreamFunc;
                }
                else if (targetType == typeof(TextWriter) || (_parameter.IsOut && targetType == typeof(TextWriter).MakeByRefType()))
                {
                    var tw = new StreamWriter(writeStream);
                    userObj = tw;
                    onComplete = async obj =>
                    {
                        tw.Flush();
                        await saveStreamFunc(obj);
                    };
                }
                else if (targetType == typeof(StreamWriter) || (_parameter.IsOut && targetType == typeof(StreamWriter).MakeByRefType()))
                {
                    var tw = new StreamWriter(writeStream);
                    userObj = tw;
                    onComplete = async obj =>
                    {
                        tw.Flush();
                        await saveStreamFunc(obj);
                    };
                }
                else if (targetType == typeof(byte[]) || (_parameter.IsOut && targetType == typeof(byte[]).MakeByRefType()))
                {
                    userObj = writeStream;
                    onComplete = saveStreamFunc;
                }
                else
                {
                    throw new InvalidOperationException("Unsupported type:" + targetType.FullName);
                }
            }
            else
            {
                var readStream = await strategy.OpenReadStreamAsync();
                onComplete = obj => Task.FromResult(0); // Nop
                if (targetType == typeof(Stream))
                {
                    userObj = readStream;
                }
                else if (targetType == typeof(TextReader))
                {
                    userObj = new StreamReader(readStream);
                }
                else if (targetType == typeof(StreamReader))
                {
                    userObj = new StreamReader(readStream);
                }
                else if (targetType == typeof(string))
                {
                    userObj = new StreamReader(readStream).ReadToEnd();
                }
                else if (targetType == typeof(byte[]) && readStream is MemoryStream)
                {
                    userObj = ((MemoryStream)readStream).ToArray();
                }
                else
                {
                    throw new InvalidOperationException("Unspported type:" + targetType.FullName);
                }
            }
                        
            valueProvider = new ConstantObj
            {
                Type = targetType,
                Value = userObj,
                OnCompleted = onComplete
            };
            return valueProvider;
        }

        // Get a stream binder from a resolved path
        private async Task<IFileStreamProvider> GetStreamHelper(string path)
        {
            TAttribute clone = JsonConvert.DeserializeObject<TAttribute>(JsonConvert.SerializeObject(_source));
            clone.Path = path;

            TFile nativeFile = await this._strategyBuilder(clone);
            var func = this._converterManager.GetConverter<TFile, IFileStreamProvider, TAttribute>();
            IFileStreamProvider strategy = func(nativeFile, null);
            return strategy;
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor
            {
                Name = _parameter.Name
            };
        }
    }
}