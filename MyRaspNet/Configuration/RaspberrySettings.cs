using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Device.Gpio;
using System.Linq;
using System.Threading.Tasks;

namespace MyRaspNet.Configuration
{
    public class RaspberrySettings
    {
        public RaspberryGPIOConfig[] GPIOs { get; set; } = new RaspberryGPIOConfig[] { };
    }

    public class RaspberryGPIOConfig
    {
        public int PinNo { get; set; }
        [DefaultValue(null)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public PinMode? PinMode { get; set; }

        [DefaultValue(null)]
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public PinValue2? PinValue { get; set; }

        public bool Enabled { get; set; }
    }

    public enum PinValue2
    {
        HIGH,
        LOW,
    }
}
