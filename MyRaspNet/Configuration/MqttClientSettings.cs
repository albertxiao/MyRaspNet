using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyRaspNet.Configuration
{
    public class MqttClientSettings
    {
        public string Host { get; set; } = "broker.hivemq.com";
        public int Port { get; set; } = 1883;
        public string ClientIdTemplate { get; set; } = "myrasp_{Serial}";
        public string User { get; set; } = "";
        public string Password { get; set; } = "";

        [JsonIgnore]
        public string ClientId { get; private set; }

        public void UpdateClientId(AppSettings setting, RaspberryDevice device)
        {
            ClientId = SmartFormat.Smart.Format(ClientIdTemplate,
               new
               {
                   DeviceName = setting.DeviceName,
                   Serial = device.Info.Serial,
                   Hardware = device.Info.Hardware
               });
        }
    }
}
