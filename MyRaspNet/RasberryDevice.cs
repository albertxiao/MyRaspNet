// uncomment this, for development/debugging on windows without raspberry
//#define DEV_ON_WINDOWS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Device.Gpio;
using System.Device.I2c;
using System.IO.Ports;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using MyRaspNet.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyRaspNet.Mqtt;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Device.Gpio.Drivers;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;

namespace MyRaspNet
{
    public class RaspberryDevice
    {
        public delegate void DiagUpdated(RaspberryDevice value);
        private Dictionary<int, int> dicPinNoVsLogical = new Dictionary<int, int>();
        private Dictionary<int, int> dicLogicalVsPinNo = new Dictionary<int, int>();

        private DiagUpdated _OnDiagUpdated;
        public event DiagUpdated OnDiagUpdated
        {
            add
            {
                if (_OnDiagUpdated == null || !_OnDiagUpdated.GetInvocationList().Contains(value))
                    _OnDiagUpdated += value;
            }
            remove
            {
                if (_OnDiagUpdated != null && _OnDiagUpdated.GetInvocationList().Contains(value))
                    _OnDiagUpdated -= value;
            }
        }

        private readonly IWebHostEnvironment environment;
        private readonly string basePath;
        private readonly WritableConfiguration writer;
        private readonly AppSettings settings;
        private readonly RaspberrySettings raspSettings;
        private readonly IServiceProvider provider;
        private readonly ILogger<RaspberryDevice> logger;
        private Stopwatch timer;
        private Task monitoringTask;
        private CancellationTokenSource monitoringTaskToken;
        public RaspberryPinMap[] PinMap { get; private set; }
        public RaspberryInfo Info { get; private set; }
        public GpioController Controller
        {
            get; private set;
        }

        private MqttClientService client;
        private MqttServerService server;
        private Hubs.GPIOHub gpioHub;
        private Hubs.DiagHub diagHub;

        public RaspberryDevice(IWebHostEnvironment environment, IServiceProvider provider, AppSettings settings, RaspberrySettings raspSettings, WritableConfiguration writer, ILogger<RaspberryDevice> logger)
        {
            Info = new RaspberryInfo();
            timer = new Stopwatch();
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            Info.BasePath = environment.WebRootPath;
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.raspSettings = raspSettings ?? throw new ArgumentNullException(nameof(raspSettings));
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Configure()
        {
            client = provider.GetService<MqttClientService>();
            server = provider.GetService<MqttServerService>();
#if !DEV_ON_WINDOWS
            Controller = new GpioController(PinNumberingScheme.Board);
#endif
            Info.UpdateCPUInfo();
            Info.UpdateOSVersion();


            settings.UpdateMqttTopic(this);
            BuildPinMap();

        }

        public string[] ScanI2CAddress()
        {
            List<string> Address = new List<string>();
            for (int i = 0; i < 127; i++)
            {
                try
                {
                    I2cDevice i2c = I2cDevice.Create(new I2cConnectionSettings(1, i));
                    Address.Add(i.ToString("x2"));
                }
                catch (Exception)
                {
                }

            }
            return Address.ToArray();
        }
        public string[] GetAvailablePort()
        {
            return SerialPort.GetPortNames();
        }

        public void StartMonitoring()
        {
            diagHub = provider.GetService<Hubs.DiagHub>();
            gpioHub = provider.GetService<Hubs.GPIOHub>();
            if (diagHub != null)
                OnDiagUpdated += diagHub_OnDiagUpdated;
            if (monitoringTask == null || monitoringTask.IsCanceled)
            {
                monitoringTaskToken = new CancellationTokenSource();
                monitoringTask = new Task(() =>
                {
                    while (!monitoringTaskToken.IsCancellationRequested)
                    {
                        Thread.Sleep(1000);
                        Info.UpdateCPULoad();
                        Info.UpdateCPUTemp();
                        Info.UpdateMemoryLoad();
                        _OnDiagUpdated?.Invoke(this);
                        //logger.LogInformation("Info: {0}", JsonConvert.SerializeObject(new { Info.ActiveMemory, Info.BufferMemory, Info.CachedMemory, Info.FreeMemory, Info.SharedMemory, Info.SReclaimable, Info.TotalMemory }));
                    }
                }, monitoringTaskToken.Token);
                monitoringTask.Start();
            }
        }

        private async void diagHub_OnDiagUpdated(RaspberryDevice value)
        {
            if (diagHub != null)
            {
                await diagHub.SendDiag(value.Info.CPULoad, value.Info.MemoryLoad, value.Info.CPUTemp).ConfigureAwait(false);
            }
        }

        public void StopMonitoring()
        {
            OnDiagUpdated -= diagHub_OnDiagUpdated;
            if (monitoringTask != null)
            {
                monitoringTaskToken.Cancel();
            }
        }
        ~RaspberryDevice()
        {
            if (Controller != null)
                Controller.Dispose();
        }

        private void BuildPinMap()
        {
            dicPinNoVsLogical.Clear();
            dicLogicalVsPinNo.Clear();
            dicPinNoVsLogical.Add(3, 2);
            dicPinNoVsLogical.Add(5, 3);
            dicPinNoVsLogical.Add(7, 4);
            dicPinNoVsLogical.Add(8, 14);
            dicPinNoVsLogical.Add(10, 15);
            dicPinNoVsLogical.Add(11, 17);
            dicPinNoVsLogical.Add(12, 18);
            dicPinNoVsLogical.Add(13, 27);
            dicPinNoVsLogical.Add(15, 22);
            dicPinNoVsLogical.Add(16, 23);
            dicPinNoVsLogical.Add(18, 24);
            dicPinNoVsLogical.Add(19, 10);
            dicPinNoVsLogical.Add(21, 9);
            dicPinNoVsLogical.Add(22, 25);
            dicPinNoVsLogical.Add(23, 11);
            dicPinNoVsLogical.Add(24, 8);
            dicPinNoVsLogical.Add(26, 7);
            dicPinNoVsLogical.Add(27, 0);
            dicPinNoVsLogical.Add(28, 1);
            dicPinNoVsLogical.Add(29, 5);
            dicPinNoVsLogical.Add(31, 6);
            dicPinNoVsLogical.Add(32, 12);
            dicPinNoVsLogical.Add(33, 13);
            dicPinNoVsLogical.Add(35, 19);
            dicPinNoVsLogical.Add(36, 16);
            dicPinNoVsLogical.Add(37, 26);
            dicPinNoVsLogical.Add(38, 20);
            dicPinNoVsLogical.Add(40, 21);
            foreach (var key in dicPinNoVsLogical.Keys)
                dicLogicalVsPinNo.Add(dicPinNoVsLogical[key], key);

            List<RaspberryPinMap> mapLst = new List<RaspberryPinMap>();
            mapLst.Add(new RaspberryPinMap(this, 1, "3.3V", "", "text-danger", true));
            mapLst.Add(new RaspberryPinMap(this, 2, "5V", "", "text-warning", true));
            mapLst.Add(new RaspberryPinMap(this, 3, "GPIO-02", "SDA.1", "text-info", true));
            mapLst.Add(new RaspberryPinMap(this, 4, "5V", "", "text-warning", true));
            mapLst.Add(new RaspberryPinMap(this, 5, "GPIO-03", "SCL.1", "text-info", true));
            mapLst.Add(new RaspberryPinMap(this, 6, "GND", "", "", true));
            mapLst.Add(new RaspberryPinMap(this, 7, "GPIO-04", "", "text-success", false));
            mapLst.Add(new RaspberryPinMap(this, 8, "GPIO-14", "TxD", "text-info", true));
            mapLst.Add(new RaspberryPinMap(this, 9, "GND", "", "", true));
            mapLst.Add(new RaspberryPinMap(this, 10, "GPIO-15", "RxD", "text-info", true));

            mapLst.Add(new RaspberryPinMap(this, 11, "GPIO-17", "", "text-success", false));
            mapLst.Add(new RaspberryPinMap(this, 12, "GPIO-18", "", "text-success", false));
            mapLst.Add(new RaspberryPinMap(this, 13, "GPIO-27", "", "text-success", false));
            mapLst.Add(new RaspberryPinMap(this, 14, "GND", "", "", true));
            mapLst.Add(new RaspberryPinMap(this, 15, "GPIO-22", "", "text-success", false));
            mapLst.Add(new RaspberryPinMap(this, 16, "GPIO-23", "", "text-success", false));
            mapLst.Add(new RaspberryPinMap(this, 17, "3.3V", "", "text-danger", true));
            mapLst.Add(new RaspberryPinMap(this, 18, "GPIO-24", "", "", false));
            mapLst.Add(new RaspberryPinMap(this, 19, "GPIO-10", "SPI-0 MOSI", "text-info", true));
            mapLst.Add(new RaspberryPinMap(this, 20, "GND", "", "", true));



            mapLst.Add(new RaspberryPinMap(this, 21, "GPIO-09", "SPI-0 MISO", "text-info", true));
            mapLst.Add(new RaspberryPinMap(this, 22, "GPIO-25", "", "text-success", false));
            mapLst.Add(new RaspberryPinMap(this, 23, "GPIO-11", "SPI-0 SCLK", "text-info", true));
            mapLst.Add(new RaspberryPinMap(this, 24, "GPIO-08", "SPI-0 CS0", "text-info", true));
            mapLst.Add(new RaspberryPinMap(this, 25, "GND", "", "", true));
            mapLst.Add(new RaspberryPinMap(this, 26, "GPIO-07", "SPI-0 CS1", "text-info", true));
            mapLst.Add(new RaspberryPinMap(this, 27, "GPIO-00", "ID-SC", "text-primary", true));
            mapLst.Add(new RaspberryPinMap(this, 28, "GPIO-01", "ID-SD", "text-primary", true));
            mapLst.Add(new RaspberryPinMap(this, 29, "GPIO-05", "", "text-success", false));
            mapLst.Add(new RaspberryPinMap(this, 30, "GND", "", "", true));


            mapLst.Add(new RaspberryPinMap(this, 31, "GPIO-06", "", "text-success", false));
            mapLst.Add(new RaspberryPinMap(this, 32, "GPIO-12", "", "text-success", false));
            mapLst.Add(new RaspberryPinMap(this, 33, "GPIO-13", "", "text-success", false));
            mapLst.Add(new RaspberryPinMap(this, 34, "GND", "", "", true));
            mapLst.Add(new RaspberryPinMap(this, 35, "GPIO-19", "SPI-1 MISO", "text-info", true));
            mapLst.Add(new RaspberryPinMap(this, 36, "GPIO-16", "SPI-1 CS0", "text-info", true));
            mapLst.Add(new RaspberryPinMap(this, 37, "GPIO-26", "", "", false));
            mapLst.Add(new RaspberryPinMap(this, 38, "GPIO-20", "SPI-1 MOSI", "text-info", true));
            mapLst.Add(new RaspberryPinMap(this, 39, "GND", "", "", true));
            mapLst.Add(new RaspberryPinMap(this, 40, "GPIO-21", "SPI-1 SCLK", "text-info", true));

            PinMap = mapLst.ToArray();
            foreach (var item in PinMap)
                item.OnPinMapRegistryChanged += PinMap_OnPinMapRegistryChanged;
            foreach (var map in mapLst)
                map.LoadAvailableMode();

            if (raspSettings.GPIOs == null || raspSettings.GPIOs.Length == 0)
            {
                UpdateConfig();
            }
            else
            {
                LoadConfig();
                UpdateConfig();
            }
        }
        private async void PinMap_OnPinMapRegistryChanged(RaspberryPinMap value)
        {
            if (value.IsPinModeEnabled)
            {
                Controller.RegisterCallbackForPinValueChangedEvent(value.PinNo, PinEventTypes.Rising, OnPinValueRising);
                Controller.RegisterCallbackForPinValueChangedEvent(value.PinNo, PinEventTypes.Falling, OnPinValueFalling);
            }
            else
            {
                Controller.UnregisterCallbackForPinValueChangedEvent(value.PinNo, OnPinValueRising);
                Controller.UnregisterCallbackForPinValueChangedEvent(value.PinNo, OnPinValueFalling);
            }
            if (gpioHub != null)
            {
                await gpioHub.SendModeChanged(value.PinNo, value.CurrentPinMode().ToString(), value.IsPinModeEnabled).ConfigureAwait(false);
            }
        }

        public void LoadConfig()
        {
            foreach (var item in PinMap)
            {
                if (item.Disabled)
                    continue;
                RaspberryGPIOConfig setting = null;
                if (raspSettings.GPIOs != null)
                    setting = raspSettings.GPIOs.Where(t => t.PinNo == item.PinNo).FirstOrDefault();
                if (setting == null)
                {
                    if (item.IsPinModeEnabled)
                        item.ClosePin();
                }
                else
                {
                    if (!setting.Enabled && item.IsPinModeEnabled)
                        item.ClosePin();
                    else if (setting.Enabled)
                    {
                        item.OpenPin(setting.PinMode.Value);
                        if (setting.PinMode == PinMode.Output)
                        {
                            if (setting.PinValue != null)
                                item.WritePinValue(setting.PinValue.Value.ToPinValue());
                            else
                                item.WritePinValue(PinValue.Low);
                        }
                    }
                }
            }
        }
        public void UpdateConfig()
        {
            // Generate config
            raspSettings.GPIOs = (from t in PinMap
                                  where !t.Disabled && t.IsPinModeEnabled
                                  orderby t.PinNo
                                  select new RaspberryGPIOConfig()
                                  {
                                      PinNo = t.PinNo,
                                      Enabled = t.IsPinModeEnabled,
                                      PinMode = t.CurrentPinMode(),
                                      PinValue = t.CurrentPinMode() == PinMode.Output ? t.ReadPinValue().ToPinValue2() : null
                                  }).ToArray();
            writer.Update<RaspberrySettings>(x =>
            {
                x.GPIOs = raspSettings.GPIOs;
            }, Utils.CONFIGKEY_RASPBERRY);
        }
        private void OnPinValueRising(object sender, PinValueChangedEventArgs e)
        {
            if (e.ChangeType == PinEventTypes.Rising)
                OnPinValueChanged(e.PinNumber, true);
        }
        private void OnPinValueFalling(object sender, PinValueChangedEventArgs e)
        {
            if (e.ChangeType == PinEventTypes.Falling)
                OnPinValueChanged(e.PinNumber, false);
        }
        private async void OnPinValueChanged(int pinNo, bool value)
        {
            var data = value ? "HIGH" : "LOW";
            int _pinNo = dicLogicalVsPinNo[pinNo];
            if (settings.MQTTMode != MQTTMode.Disabled)
            {
                var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { PinNo = _pinNo, GPIONo = pinNo, PinValue = data }));
                var msg = new MQTTnet.MqttApplicationMessageBuilder()
                     .WithTopic(string.Format("{0}/{1}/gpio", settings.MQTTTopic, AppSettings.TeleTopic))
                     .WithPayload(payload)
                     .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                     .WithRetainFlag(false)
                     .Build();
                switch (settings.MQTTMode)
                {
                    case MQTTMode.Server:
                    case MQTTMode.Bridge:
                        if (server.IsStarted())
                            await server.PublishAsync(msg).ConfigureAwait(false);
                        break;
                    case MQTTMode.Client:
                        if (client.IsStarted())
                            await client.PublishAsync(msg).ConfigureAwait(false);
                        break;
                    default:
                        break;
                }
            }

            if (gpioHub != null)
            {
                await gpioHub.SendValueChanged(_pinNo, value).ConfigureAwait(false);
            }

        }

        public async Task RunWebSocketConnectionAsync(WebSocket webSocket, HttpContext httpContext)
        {
            if (webSocket == null) 
                throw new ArgumentNullException(nameof(webSocket));

            var endpoint = $"{httpContext.Connection.RemoteIpAddress}:{httpContext.Connection.RemotePort}";
            var clientCertificate = await httpContext.Connection.GetClientCertificateAsync().ConfigureAwait(false);
            try
            {
                var isSecureConnection = clientCertificate != null;

            }
            finally
            {
                clientCertificate?.Dispose();
            }
        }

        public Task ExecuteCommandAsync(string cmdNames, string data)
        {
            if (string.IsNullOrEmpty(cmdNames))
                return Task.Delay(0);
            string[] cmdNameArr = cmdNames.Split("/", StringSplitOptions.RemoveEmptyEntries);
            if (cmdNameArr == null)
                return Task.Delay(0);
            if (cmdNameArr.Length == 0)
                return Task.Delay(0);
            var cmdName = cmdNameArr[0].ToLower();
            switch (cmdName)
            {
                case "gpio":
                    List<JObject> jObjLst = new List<JObject>();
                    if (string.IsNullOrEmpty(data))
                        return Task.Delay(0);
                    try
                    {
                        if (cmdNameArr.Length == 1)
                        {
                            data = data.ToLower();
                            var obj = JsonConvert.DeserializeObject(data);
                            if (obj is JArray)
                            {
                                var jObjArr = obj as JArray;
                                foreach (var jObj in jObjArr)
                                    jObjLst.Add(jObj as JObject);
                            }
                            else
                                jObjLst.Add(obj as JObject);
                        }
                        else
                        {
                            int pinNo = 0;
                            if (int.TryParse(cmdNameArr[1], out pinNo))
                            {
                                JObject jObj = new JObject();
                                jObj.Add("pinno", pinNo);
                                jObj.Add("pinvalue", data);
                                jObjLst.Add(jObj);
                            }
                        }
                        ExecuteGPIOCommand(jObjLst.ToArray());
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.Message);
                    }
                    break;
                case "reboot":
                    return Task.Run(() => { Program.Shutdown(); });
                default:
                    return Task.Delay(0);
            }
            return Task.CompletedTask;
        }

        private void ExecuteGPIOCommand(JObject[] jObjArr)
        {
            bool IsChanged = false;
            foreach (var jObj in jObjArr)
            {
                int? pinNo = jObj.ContainsKey("pinno") ? jObj["pinno"].ValueOrDefault<int?>() : null;
                int? gpioNo = jObj.ContainsKey("gpiono") ? jObj["gpiono"].ValueOrDefault<int?>() : null;
                string pinValue = (jObj.ContainsKey("pinvalue") ? jObj["pinvalue"].ValueOrDefault<string>() : "").ToLower();
                PinValue value = PinValue.Low;
                switch (pinValue)
                {
                    case "high":
                    case "1":
                    case "true":
                        value = PinValue.High;
                        break;
                    default:
                        value = PinValue.Low;
                        break;
                }
                if (!string.IsNullOrEmpty(pinValue) && (pinNo.HasValue || gpioNo.HasValue))
                {
                    if (pinNo == null && dicLogicalVsPinNo.ContainsKey(gpioNo.GetValueOrDefault()))
                        pinNo = dicLogicalVsPinNo[gpioNo.Value];
                    var pin = PinMap.Where(t => t.PinNo == pinNo.GetValueOrDefault()).FirstOrDefault();
                    if (pin != null)
                    {
                        if (pin.IsPinModeEnabled && pin.CurrentPinMode() == PinMode.Output)
                        {
                            pin.WritePinValue(value);
                            IsChanged = true;
                        }
                    }
                }
            }
            if (IsChanged)
                UpdateConfig();
        }
    }
    public class RaspberryPinMap
    {
        public delegate void PinMapRegistryChanged(RaspberryPinMap value);
        private PinMapRegistryChanged _OnPinMapRegistryChanged;
        public event PinMapRegistryChanged OnPinMapRegistryChanged
        {
            add
            {
                if (_OnPinMapRegistryChanged == null || !_OnPinMapRegistryChanged.GetInvocationList().Contains(value))
                    _OnPinMapRegistryChanged += value;
            }
            remove
            {
                if (_OnPinMapRegistryChanged != null && _OnPinMapRegistryChanged.GetInvocationList().Contains(value))
                    _OnPinMapRegistryChanged -= value;
            }
        }
        private RaspberryDevice device;

        public int PinNo { get; private set; }
        public string Label { get; set; }
        public string AlternateLabel { get; set; }
        public string Style { get; set; }
        public bool Disabled { get; private set; }
        public bool IsPinModeEnabled { get; private set; }
        public PinMode[] AvaiableModes { get; private set; } = new PinMode[] { };


        public PinMode CurrentPinMode()
        {
#if !DEV_ON_WINDOWS
            return device.Controller.GetPinMode(PinNo);
#else
            return PinMode.Input;
#endif
        }
        public PinValue ReadPinValue()
        {
#if Linux
            return device.Controller.Read(PinNo);
#else
            return PinValue.Low;
#endif
        }
        public void WritePinValue(PinValue value)
        {
#if Linux
            device.Controller.Write(PinNo, value);
#endif
        }
        public bool OpenPin(PinMode mode)
        {

            if (this.Disabled || !AvaiableModes.Contains(mode))
                return false;

            bool flag = false;
#if !DEV_ON_WINDOWS
            if (!device.Controller.IsPinOpen(PinNo))
            {
                device.Controller.OpenPin(PinNo);
                flag = true;
            }
            device.Controller.SetPinMode(PinNo, mode);
#endif
            this.IsPinModeEnabled = true;
            if (flag)
                _OnPinMapRegistryChanged?.Invoke(this);
            return true;
        }
        public bool ClosePin()
        {
            if (this.Disabled)
                return false;
            bool flag = false;
#if !DEV_ON_WINDOWS
            if (device.Controller.IsPinOpen(PinNo))
            {
                device.Controller.ClosePin(PinNo);
                flag = true;
            }
#endif
            this.IsPinModeEnabled = false;
            if (flag)
                _OnPinMapRegistryChanged?.Invoke(this);
            return true;
        }

        public RaspberryPinMap(RaspberryDevice device, int PinNo, string Label, string AlternateLabel, string Style, bool Disabled)
        {
            this.device = device;
            this.PinNo = PinNo;
            this.Label = Label;
            this.AlternateLabel = AlternateLabel;
            this.Style = Style;
            this.Disabled = Disabled;
        }
        internal void LoadAvailableMode()
        {
            if (this.Disabled)
                return;
            List<PinMode> modelst = new List<PinMode>();

#if !DEV_ON_WINDOWS
            if (device.Controller.IsPinModeSupported(PinNo, PinMode.Input))
                modelst.Add(PinMode.Input);
            if (device.Controller.IsPinModeSupported(PinNo, PinMode.Output))
                modelst.Add(PinMode.Output);
            if (device.Controller.IsPinModeSupported(PinNo, PinMode.InputPullUp))
                modelst.Add(PinMode.InputPullUp);
            if (device.Controller.IsPinModeSupported(PinNo, PinMode.InputPullDown))
                modelst.Add(PinMode.InputPullDown);
            AvaiableModes = modelst.ToArray();
            IsPinModeEnabled = device.Controller.IsPinOpen(PinNo);
            if (IsPinModeEnabled)
                _OnPinMapRegistryChanged?.Invoke(this);
#else
            modelst.Add(PinMode.Input);
            modelst.Add(PinMode.Output);
            modelst.Add(PinMode.InputPullUp);
            modelst.Add(PinMode.InputPullDown);
            AvaiableModes = modelst.ToArray();
            IsPinModeEnabled = true;
#endif
        }
    }

    public class RaspberryInfo
    {
        public const int MaxHistoryLength = 60;

        public string BasePath { get; set; }
        public double CPULoad { get; private set; }
        public double CPUTemp { get; private set; }
        public double MemoryLoad { get; private set; }


        public List<DataHistory> CPULoadHistory { get; private set; } = new List<DataHistory>();
        public List<DataHistory> CPUTempHistory { get; private set; } = new List<DataHistory>();
        public List<DataHistory> MemoryLoadHistory { get; private set; } = new List<DataHistory>();


        public double TotalMemory { get; private set; }
        public double FreeMemory { get; private set; }
        public double ActiveMemory { get; private set; }
        public double SharedMemory { get; private set; }
        public double SReclaimable { get; private set; }
        public double CachedMemory { get; private set; }
        public double BufferMemory { get; private set; }

        public string CPUInfo { get; private set; }
        public string OSVersion { get; private set; }
        public string Hardware { get; private set; }
        public string Revision { get; set; }
        public string Serial { get; set; }
        public string Model { get; set; }

        double lastTotalUser, lastTotalUserLow, lastTotalSys, lastTotalIdle;

        public RaspberryInfo()
        {
            CPULoad = 0;
            CPUTemp = 0;
        }

        public void UpdateCPULoad()
        {
            // calculation reference
            // https://www.raspberrypi.org/forums/viewtopic.php?p=1438922&sid=bf2751a7dd4b85ceb76fe1a3358217db#p1438922
#if !DEV_ON_WINDOWS
            var filePath = "/proc/stat";
#else
            var filePath = Path.Combine(BasePath, "test/stat.txt");
#endif
            double totalUser = 0, totalUserLow = 0, totalSys = 0, totalIdle = 0, total = 0;

            Utils.ReadFileLineByLine(filePath, (line) =>
            {
                if (!string.IsNullOrEmpty(line) && line.StartsWith("cpu "))
                {
                    var valArr = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    totalUser = Convert.ToDouble(valArr[1]);
                    totalUserLow = Convert.ToDouble(valArr[2]);
                    totalSys = Convert.ToDouble(valArr[3]);
                    totalIdle = Convert.ToDouble(valArr[4]);
                    return true;
                }
                return false;
            });
            if (totalUser < lastTotalUser || totalUserLow < lastTotalUserLow || totalSys < lastTotalSys || totalIdle < lastTotalIdle)
            {
                CPULoad = 0;
            }
            else
            {
                total = (totalUser - lastTotalUser) + (totalUserLow - lastTotalUserLow) + (totalSys - lastTotalSys);
                CPULoad = total * 100d;
                total += (totalIdle - lastTotalIdle);
                CPULoad /= total;
            }

            lastTotalUser = totalUser;
            lastTotalUserLow = totalUserLow;
            lastTotalSys = totalSys;
            lastTotalIdle = totalIdle;


            DataHistory hist = new DataHistory() { Time = DateTime.Now, Data = CPULoad };
            if (CPULoadHistory.Count >= MaxHistoryLength)
                CPULoadHistory.RemoveAt(0);
            CPULoadHistory.Add(hist);
        }
        public void UpdateCPUTemp()
        {
#if !DEV_ON_WINDOWS
            var filePath = "/sys/class/thermal/thermal_zone0/temp";
#else
            var filePath = Path.Combine(BasePath, "test/temp.txt");
#endif
            double _CPUTemp = 0;
            Utils.ReadFileLineByLine(filePath, (line) =>
            {
                if (!string.IsNullOrEmpty(line))
                {
                    _CPUTemp = Convert.ToDouble(line);
                }
                return false;
            });
            CPUTemp = _CPUTemp / 1000d;
            DataHistory hist = new DataHistory() { Time = DateTime.Now, Data = CPUTemp };
            if (CPUTempHistory.Count >= MaxHistoryLength)
                CPUTempHistory.RemoveAt(0);
            CPUTempHistory.Add(hist);
        }
        public void UpdateMemoryLoad()
        {
#if !DEV_ON_WINDOWS
            var filePath = "/proc/meminfo";
#else
            var filePath = Path.Combine(BasePath, "test/meminfo.txt");
#endif

            //calculation reference htop
            //https://github.com/hishamhm/htop/blob/8af4d9f453ffa2209e486418811f7652822951c6/linux/LinuxProcessList.c#L802-L833
            //https://github.com/hishamhm/htop/blob/1f3d85b6174f690a7e354bbadac19404d5e75e78/linux/Platform.c#L198-L208

            string _totalMemory = string.Empty;
            string _freeMemory = string.Empty;
            string _activeMemory = string.Empty;
            string _sharedMemory = string.Empty;
            string _sReclaimable = string.Empty;
            string _cachedMemory = string.Empty;
            string _bufferMemory = string.Empty;

            TotalMemory = 0;
            FreeMemory = 0;
            MemoryLoad = 0;
            ActiveMemory = 0;
            SharedMemory = 0;
            SReclaimable = 0;
            CachedMemory = 0;
            BufferMemory = 0;

            Utils.ReadFileLineByLine(filePath, (line) =>
            {
                if (!string.IsNullOrEmpty(line))
                {
                    var valArr = line.Split(new string[] { " ", ":" }, StringSplitOptions.RemoveEmptyEntries);
                    if (valArr.Length > 0)
                    {
                        if (valArr[0] == "MemTotal")
                            _totalMemory = valArr[1];
                        if (valArr[0] == "MemFree")
                            _freeMemory = valArr[1];
                        if (valArr[0] == "Buffers")
                            _bufferMemory = valArr[1];
                        if (valArr[0] == "Active")
                            _activeMemory = valArr[1];
                        if (valArr[0] == "Cached")
                            _cachedMemory = valArr[1];
                        if (valArr[0] == "Shmem")
                            _sharedMemory = valArr[1];
                        if (valArr[0] == "SReclaimable")
                            _sReclaimable = valArr[1];
                    }
                    if (!string.IsNullOrEmpty(_totalMemory)
                        && !string.IsNullOrEmpty(_freeMemory)
                        && !string.IsNullOrEmpty(_bufferMemory)
                        && !string.IsNullOrEmpty(_activeMemory)
                        && !string.IsNullOrEmpty(_cachedMemory)
                        && !string.IsNullOrEmpty(_sharedMemory)
                        && !string.IsNullOrEmpty(_sReclaimable))
                        return true;
                }
                return false;
            });
            if (!string.IsNullOrEmpty(_totalMemory))
                TotalMemory = Convert.ToDouble(_totalMemory);
            if (!string.IsNullOrEmpty(_freeMemory))
                FreeMemory = Convert.ToDouble(_freeMemory);
            if (!string.IsNullOrEmpty(_bufferMemory))
                BufferMemory = Convert.ToDouble(_bufferMemory);
            if (!string.IsNullOrEmpty(_activeMemory))
                ActiveMemory = Convert.ToDouble(_activeMemory);
            if (!string.IsNullOrEmpty(_cachedMemory))
                CachedMemory = Convert.ToDouble(_cachedMemory);
            if (!string.IsNullOrEmpty(_sharedMemory))
                SharedMemory = Convert.ToDouble(_sharedMemory);
            if (!string.IsNullOrEmpty(_sReclaimable))
                SReclaimable = Convert.ToDouble(_sReclaimable);

            CachedMemory += (SReclaimable - SharedMemory);
            var usageMemory = TotalMemory - FreeMemory;
            usageMemory -= (BufferMemory + CachedMemory);
            if (TotalMemory != 0)
                MemoryLoad = (usageMemory / TotalMemory) * 100;

            DataHistory hist = new DataHistory() { Time = DateTime.Now, Data = MemoryLoad };
            if (MemoryLoadHistory.Count >= MaxHistoryLength)
                MemoryLoadHistory.RemoveAt(0);
            MemoryLoadHistory.Add(hist);
        }
        public void UpdateOSVersion()
        {
            if (string.IsNullOrEmpty(OSVersion))
            {
#if !DEV_ON_WINDOWS
                var filePath = "/proc/version";
#else
                var filePath = Path.Combine(BasePath, "test/version.txt");
#endif
                Utils.ReadFileLineByLine(filePath, (line) =>
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        OSVersion = line;
                    }
                    return false;
                });
            }
        }
        public void UpdateCPUInfo()
        {
            if (string.IsNullOrEmpty(CPUInfo))
            {
#if !DEV_ON_WINDOWS
                var filePath = "/proc/cpuinfo";
#else
                var filePath = Path.Combine(BasePath, "test/cpuinfo.txt");
#endif
                StringBuilder sb = new StringBuilder();
                Utils.ReadFileLineByLine(filePath, (line) =>
                {
                    if (String.IsNullOrEmpty(line))
                        return false;
                    sb.AppendLine(line);
                    string[] valArr = line.Split(new string[] { " ", ":" }, StringSplitOptions.RemoveEmptyEntries);
                    string propName = valArr[0].Trim();
                    switch (propName)
                    {
                        case "Hardware":
                            Hardware = string.Join(" ", valArr.Skip(1).ToArray()).Trim();
                            break;
                        case "Revision":
                            Revision = string.Join(" ", valArr.Skip(1).ToArray()).Trim();
                            break;
                        case "Serial":
                            Serial = string.Join(" ", valArr.Skip(1).ToArray()).Trim();
                            ulong serialNo = Convert.ToUInt64(Serial, 16);
                            Serial = serialNo.ToString("X");
                            break;
                        case "Model":
                            Model = string.Join(" ", valArr.Skip(1).ToArray()).Trim();
                            break;
                        default:
                            Console.WriteLine("PropName: {0}", propName);
                            Console.WriteLine(line);
                            break;
                    }
                    return false;
                });
                CPUInfo = sb.ToString();
            }
        }
    }

    public class DataHistory
    {
        public DateTime Time { get; set; }
        public object Data { get; set; }
    }
}
