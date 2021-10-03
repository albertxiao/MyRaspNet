using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyRaspNet.Configuration
{
    //public interface IWritableOptions<out T> : IOptions<T> where T : class, new()
    //{
    //    void Update(Action<T> applyChanges);
    //}
    //public class WritableOptions<T> : IWritableOptions<T> where T : class, new()
    //{
    //    private readonly IWebHostEnvironment _environment;
    //    private readonly IOptionsMonitor<T> _options;
    //    private readonly IConfigurationRoot _configuration;
    //    private readonly string _section;
    //    private readonly string _file;

    //    public WritableOptions(
    //        IWebHostEnvironment environment,
    //        IOptionsMonitor<T> options,
    //        IConfigurationRoot configuration,
    //        string section,
    //        string file)
    //    {
    //        _environment = environment;
    //        _options = options;
    //        _configuration = configuration;
    //        _section = section;
    //        _file = file;
    //    }

    //    public T Value => _options.CurrentValue;
    //    public T Get(string name) => _options.Get(name);

    //    public void Update(Action<T> applyChanges)
    //    {
    //        var fileProvider = _environment.ContentRootFileProvider;
    //        var fileInfo = fileProvider.GetFileInfo(_file);
    //        var physicalPath = fileInfo.PhysicalPath;

    //        var jObject = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(physicalPath));
    //        var sectionObject = jObject.TryGetValue(_section, out JToken section) ?
    //            JsonConvert.DeserializeObject<T>(section.ToString()) : (Value ?? new T());

    //        applyChanges(sectionObject);

    //        jObject[_section] = JObject.Parse(JsonConvert.SerializeObject(sectionObject));
    //        File.WriteAllText(physicalPath, JsonConvert.SerializeObject(jObject, Formatting.Indented));
    //        _configuration.Reload();
    //    }
    //}
    //public static class ServiceCollectionExtensions
    //{
    //    public static void ConfigureWritable<T>(
    //        this IServiceCollection services,
    //        IConfigurationSection section,
    //        string file = "appsettings.json") where T : class, new()
    //    {
    //        services.Configure<T>(section);
    //        services.AddTransient<IWritableOptions<T>>(provider =>
    //        {
    //            var configuration = (IConfigurationRoot)provider.GetService<IConfiguration>();
    //            var environment = provider.GetService<IWebHostEnvironment>();
    //            var options = provider.GetService<IOptionsMonitor<T>>();
    //            return new WritableOptions<T>(environment, options, configuration, section.Key, file);
    //        });
    //    }
    //}


    public class WritableConfiguration
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment environment;
        private readonly IServiceProvider provider;
        public WritableConfiguration(IServiceProvider provider, IConfiguration configuration, IWebHostEnvironment environment)
        {
            this.configuration = configuration;
            this.environment = environment;
            this.provider = provider;
        }

        public T Update<T>(Action<T> applyChanges, string section, string file = "appsettings.json") where T : class, new()
        {
            var conf = (IConfigurationRoot)configuration;
            var fileProvider = environment.ContentRootFileProvider;
            var fileInfo = fileProvider.GetFileInfo(file);
            var physicalPath = fileInfo.PhysicalPath;

            var jObject = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(physicalPath));
            var sectionObject = jObject.TryGetValue(section, out JToken _section) ? JsonConvert.DeserializeObject<T>(_section.ToString()) : new T();

            applyChanges(sectionObject);

            jObject[section] = JObject.Parse(JsonConvert.SerializeObject(sectionObject));
            File.WriteAllText(physicalPath, JsonConvert.SerializeObject(jObject, Formatting.Indented));
            conf.Reload();

            return sectionObject;
        }
        public T GetService<T>()
        {
            return provider.GetService<T>();
        }
    }
}
