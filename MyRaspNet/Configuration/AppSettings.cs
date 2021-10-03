using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MyRaspNet.Configuration
{
    public class AppSettings
    {
        public const string TeleTopic = "tele";
        public const string CommandTopic = "cmnd";
        public const string IoTTopic = "iot";
        private string _Version = string.Empty;

        /// <summary>Contains the ConnectionString that determines how to connect to the targeted database engine.</summary>
        /// <remarks>Note: The value is stored unprotected and should be kept secured outside of development!</remarks>
        public Dictionary<string, DatabaseServer> ConnectionStrings { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public MQTTMode MQTTMode { get; set; } = MQTTMode.Server;

        public string DeviceName { get; set; } = "myrasp";
        public string MQTTTopicTemplate { get; set; } = "/{DeviceName}/{Serial}";
        public bool EnableScheduler { get; set; } = true;

        [JsonIgnore]
        public string MQTTTopic { get; private set; }



        [JsonIgnore]
        public string Version { get { return _Version; } }

        public void UpdateMqttTopic(RaspberryDevice device)
        {
            MQTTTopic = SmartFormat.Smart.Format(MQTTTopicTemplate,
               new
               {
                   DeviceName = DeviceName,
                   Serial = device.Info.Serial,
                   Hardware = device.Info.Hardware
               });
            if (MQTTTopic.EndsWith("/"))
                MQTTTopic = MQTTTopic.Remove(MQTTTopic.Length - 1, 1);
        }
        public AppSettings()
        {
            _Version = Assembly.GetEntryAssembly().GetName().Version.ToString();
        }
    }

    public class DatabaseServer
    {
        public string ConnectionString { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public DatabaseProvider Provider { get; set; }
    }

    public enum DatabaseProvider
    {
        SqlServer,
        Postgre,
        MySql
    }
    public enum MQTTMode
    {
        Disabled,
        Server,
        Bridge,
        Client
    }
}
