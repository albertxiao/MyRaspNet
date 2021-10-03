using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using MyRaspNet.Configuration;
using Newtonsoft.Json;

namespace MyRaspNet.Mqtt
{
    public class MqttClientService
    {
        private IManagedMqttClient managedMqttClient;
        private readonly AppSettings settings;
        private readonly MqttClientSettings clientSettings;
        private readonly MqttServerService server;
        private readonly RaspberryDevice device;
        private readonly ILogger<MqttClientService> logger;

        public bool IsStarted()
        {
            if (managedMqttClient == null)
                return false;
            return managedMqttClient.IsStarted;
        }


        public MqttClientService(AppSettings settings, MqttClientSettings clientSettings, MqttServerService server, RaspberryDevice device, ILogger<MqttClientService> logger)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.clientSettings = clientSettings ?? throw new ArgumentNullException(nameof(clientSettings));
            this.server = server ?? throw new ArgumentNullException(nameof(server));
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var mqttFactory = new MQTTnet.MqttFactory();
            this.managedMqttClient = mqttFactory.CreateManagedMqttClient();
        }

        public Task Start()
        {
            var tlsOptions = new MQTTnet.Client.Options.MqttClientTlsOptions
            {
                UseTls = false,
                IgnoreCertificateChainErrors = true,
                IgnoreCertificateRevocationErrors = true,
                AllowUntrustedCertificates = true
            };

            var options = new MQTTnet.Client.Options.MqttClientOptions
            {
                ClientId = clientSettings.ClientId,
                ProtocolVersion = MQTTnet.Formatter.MqttProtocolVersion.V311,
                ChannelOptions = new MQTTnet.Client.Options.MqttClientTcpOptions
                {
                    Server = clientSettings.Host,
                    Port = clientSettings.Port,
                    TlsOptions = tlsOptions
                }
            };

            if (options.ChannelOptions == null)
            {
                throw new InvalidOperationException();
            }

            options.Credentials = new MQTTnet.Client.Options.MqttClientCredentials
            {
                Username = clientSettings.User,
                Password = Encoding.UTF8.GetBytes(clientSettings.Password)
            };

            options.CleanSession = true;
            options.KeepAlivePeriod = TimeSpan.FromSeconds(5);
            this.managedMqttClient.UseApplicationMessageReceivedHandler(this.OnApplicationMessageReceived);
            this.managedMqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnPublisherConnected);
            this.managedMqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnPublisherDisconnected);

            var filters = new MqttTopicFilter[]
            {
                new MqttTopicFilter() { Topic = string.Format("{0}/{1}/#", settings.MQTTTopic, AppSettings.CommandTopic) },
                new MqttTopicFilter() { Topic = string.Format("{0}/{1}/in/#", settings.MQTTTopic, AppSettings.IoTTopic) },
            };
            Task tsk = this.managedMqttClient.StartAsync(
                new ManagedMqttClientOptions
                {
                    ClientOptions = options
                }).ContinueWith(t =>
                {
                    this.managedMqttClient.SubscribeAsync(filters);
                });
            if (settings.MQTTMode == MQTTMode.Client)
                device.OnDiagUpdated += device_OnDiagUpdated;
            return tsk;
        }

        private async void device_OnDiagUpdated(RaspberryDevice value)
        {
            string data = JsonConvert.SerializeObject(new
            {
                Time = DateTime.Now,
                value.Info.CPULoad,
                value.Info.CPUTemp,
                value.Info.MemoryLoad,
            });
            //logger.LogInformation("Mqtt Client - {0}/{1}/diag : {2}", settings.MQTTTopic, AppSettings.TeleTopic, data);
            await PublishAsync(string.Format("{0}/{1}/diag", settings.MQTTTopic, AppSettings.TeleTopic), data, MqttQualityOfServiceLevel.AtLeastOnce, false);
        }

        public Task Stop()
        {
            device.OnDiagUpdated -= device_OnDiagUpdated;
            if (this.managedMqttClient != null && this.managedMqttClient.IsStarted)
            {
                return this.managedMqttClient.StopAsync();
            }
            else
                return Task.Delay(0);
        }

        public Task<MqttClientPublishResult> PublishAsync(string topic, string message, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, bool retain = false)
        {
            if (IsStarted())
            {
                try
                {
                    var payload = Encoding.UTF8.GetBytes(message);
                    var msg = new MqttApplicationMessageBuilder()
                        .WithTopic(topic)
                        .WithPayload(payload)
                        .WithQualityOfServiceLevel(qos)
                        .WithRetainFlag(retain)
                        .Build();
                    if (this.managedMqttClient != null)
                        return this.managedMqttClient.PublishAsync(msg);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while publishing application message to server.");
                }
            }
            return Task.Run(() => new MqttClientPublishResult() { ReasonCode = MqttClientPublishReasonCode.UnspecifiedError, ReasonString = "MQTT Client Down" });
        }
        public Task<MqttClientPublishResult> PublishAsync(string topic, byte[] payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, bool retain = false)
        {
            if (IsStarted())
            {
                try
                {
                    var msg = new MqttApplicationMessageBuilder()
                        .WithTopic(topic)
                        .WithPayload(payload)
                        .WithQualityOfServiceLevel(qos)
                        .WithRetainFlag(retain)
                        .Build();

                    if (this.managedMqttClient != null)
                        return this.managedMqttClient.PublishAsync(msg);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while publishing application message to server.");
                }
            }
            return Task.Run(() => new MqttClientPublishResult() { ReasonCode = MqttClientPublishReasonCode.UnspecifiedError, ReasonString = "MQTT Client Down" });
        }
        public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage msg)
        {
            if (IsStarted())
            {
                try
                {
                    if (this.managedMqttClient != null)
                        return this.managedMqttClient.PublishAsync(msg);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while publishing application message to server.");
                }
            }
            return Task.Run(() => new MqttClientPublishResult() { ReasonCode = MqttClientPublishReasonCode.UnspecifiedError, ReasonString = "MQTT Client Down" });
        }

        private void OnPublisherConnected(MqttClientConnectedEventArgs e)
        {
            logger.LogInformation("Mqtt Client Connected: {0}", Newtonsoft.Json.JsonConvert.SerializeObject(e));
        }
        private void OnPublisherDisconnected(MqttClientDisconnectedEventArgs e)
        {
            logger.LogInformation("Mqtt Client Disconnected: {0}", Newtonsoft.Json.JsonConvert.SerializeObject(e));
        }
        private async void OnApplicationMessageReceived(MQTTnet.MqttApplicationMessageReceivedEventArgs e)
        {
            
            if (MQTTnet.Server.MqttTopicFilterComparer.IsMatch(e.ApplicationMessage.Topic, string.Format("{0}/{1}/#", settings.MQTTTopic, AppSettings.CommandTopic)))
            {
                // command
                var cmdNames = e.ApplicationMessage.Topic.Remove(0, string.Format("{0}/{1}/", settings.MQTTTopic, AppSettings.CommandTopic).Length);
                await device.ExecuteCommandAsync(cmdNames, e.ApplicationMessage.ConvertPayloadToString());
            }
            else if (MQTTnet.Server.MqttTopicFilterComparer.IsMatch(e.ApplicationMessage.Topic, string.Format("{0}/{1}/in/#", settings.MQTTTopic, AppSettings.IoTTopic)))
            {
                // IoT Input
                string filter = string.Format("{0}/{1}/in/", settings.MQTTTopic, AppSettings.IoTTopic);
                if (settings.MQTTMode == MQTTMode.Bridge)
                {
                    string newTopic = e.ApplicationMessage.Topic.Remove(0, filter.Length);
                    // Republish to Local Server
                    await server.PublishAsync(new MqttApplicationMessage()
                    {
                        Topic = newTopic,
                        Payload = e.ApplicationMessage.Payload,
                        ContentType = e.ApplicationMessage.ContentType,
                        CorrelationData = e.ApplicationMessage.CorrelationData,
                        Dup = e.ApplicationMessage.Dup,
                        MessageExpiryInterval = e.ApplicationMessage.MessageExpiryInterval,
                        PayloadFormatIndicator = e.ApplicationMessage.PayloadFormatIndicator,
                        QualityOfServiceLevel = e.ApplicationMessage.QualityOfServiceLevel,
                        ResponseTopic = e.ApplicationMessage.ResponseTopic,
                        Retain = e.ApplicationMessage.Retain,
                        SubscriptionIdentifiers = e.ApplicationMessage.SubscriptionIdentifiers,
                        TopicAlias = e.ApplicationMessage.TopicAlias
                    });
                }
            }
        }
    }
}
