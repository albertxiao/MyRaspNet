using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MQTTnet.Adapter;
using MQTTnet.AspNetCore;
using MQTTnet.Client.Publishing;
using MQTTnet.Implementations;
using MQTTnet.Protocol;
using MyRaspNet.Configuration;
using MQTTnet.Server.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using MQTTnet.Server;
using MQTTnet;
using Newtonsoft.Json;

namespace MyRaspNet.Mqtt
{
    public class MqttServerService
    {
        readonly ILogger<MqttServerService> _logger;

        readonly MqttSettingsModel _mqttsettings;
        readonly MqttApplicationMessageInterceptor _mqttApplicationMessageInterceptor;
        readonly MqttServerStorage _mqttServerStorage;
        readonly MqttClientConnectedHandler _mqttClientConnectedHandler;
        readonly MqttClientDisconnectedHandler _mqttClientDisconnectedHandler;
        readonly MqttClientSubscribedTopicHandler _mqttClientSubscribedTopicHandler;
        readonly MqttClientUnsubscribedTopicHandler _mqttClientUnsubscribedTopicHandler;
        readonly MqttServerConnectionValidator _mqttConnectionValidator;
        readonly IMqttServer _mqttServer;
        readonly MqttSubscriptionInterceptor _mqttSubscriptionInterceptor;
        readonly MqttUnsubscriptionInterceptor _mqttUnsubscriptionInterceptor;
        //readonly PythonScriptHostService _pythonScriptHostService;
        readonly MqttWebSocketServerAdapter _webSocketServerAdapter;

        readonly AppSettings settings;
        readonly RaspberryDevice device;

        public MqttServerService(
            AppSettings settings,
            MqttSettingsModel mqttSettings,
            CustomMqttFactory mqttFactory,
            MqttClientConnectedHandler mqttClientConnectedHandler,
            MqttClientDisconnectedHandler mqttClientDisconnectedHandler,
            MqttClientSubscribedTopicHandler mqttClientSubscribedTopicHandler,
            MqttClientUnsubscribedTopicHandler mqttClientUnsubscribedTopicHandler,
            MqttServerConnectionValidator mqttConnectionValidator,
            MqttSubscriptionInterceptor mqttSubscriptionInterceptor,
            MqttUnsubscriptionInterceptor mqttUnsubscriptionInterceptor,
            MqttApplicationMessageInterceptor mqttApplicationMessageInterceptor,
            MqttServerStorage mqttServerStorage,
            RaspberryDevice device,
            ILogger<MqttServerService> logger)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _mqttsettings = mqttSettings ?? throw new ArgumentNullException(nameof(mqttSettings));
            _mqttClientConnectedHandler = mqttClientConnectedHandler ?? throw new ArgumentNullException(nameof(mqttClientConnectedHandler));
            _mqttClientDisconnectedHandler = mqttClientDisconnectedHandler ?? throw new ArgumentNullException(nameof(mqttClientDisconnectedHandler));
            _mqttClientSubscribedTopicHandler = mqttClientSubscribedTopicHandler ?? throw new ArgumentNullException(nameof(mqttClientSubscribedTopicHandler));
            _mqttClientUnsubscribedTopicHandler = mqttClientUnsubscribedTopicHandler ?? throw new ArgumentNullException(nameof(mqttClientUnsubscribedTopicHandler));
            _mqttConnectionValidator = mqttConnectionValidator ?? throw new ArgumentNullException(nameof(mqttConnectionValidator));
            _mqttSubscriptionInterceptor = mqttSubscriptionInterceptor ?? throw new ArgumentNullException(nameof(mqttSubscriptionInterceptor));
            _mqttUnsubscriptionInterceptor = mqttUnsubscriptionInterceptor ?? throw new ArgumentNullException(nameof(mqttUnsubscriptionInterceptor));
            _mqttApplicationMessageInterceptor = mqttApplicationMessageInterceptor ?? throw new ArgumentNullException(nameof(mqttApplicationMessageInterceptor));
            _mqttServerStorage = mqttServerStorage ?? throw new ArgumentNullException(nameof(mqttServerStorage));
            //_pythonScriptHostService = pythonScriptHostService ?? throw new ArgumentNullException(nameof(pythonScriptHostService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _webSocketServerAdapter = new MqttWebSocketServerAdapter(mqttFactory.Logger);
            this.device = device;
            var adapters = new List<IMqttServerAdapter>
            {
                new MqttTcpServerAdapter(mqttFactory.Logger)
                {
                    TreatSocketOpeningErrorAsWarning = true // Opening other ports than for HTTP is not allows in Azure App Services.
                },
                _webSocketServerAdapter
            };
            _mqttServer = mqttFactory.CreateMqttServer(adapters);

        }

        private async void device_OnDiagUpdated(RaspberryDevice value)
        {
            string data = JsonConvert.SerializeObject(new
            {
                Time = DateTime.Now,
                value.Info.CPULoad,
                value.Info.CPUTemp,
                value.Info.MemoryLoad
            });
            await PublishAsync(new MqttApplicationMessage()
            {
                Topic = string.Format("{0}/{1}/diag", settings.MQTTTopic, AppSettings.TeleTopic),
                Payload = Encoding.UTF8.GetBytes(data)
            }).ConfigureAwait(false);
            //_logger.LogInformation("Mqtt Server - {0}/{1}/diag : {2}", settings.MQTTTopic, AppSettings.TeleTopic, data);
        }

        public void Configure()
        {
            //_pythonScriptHostService.RegisterProxyObject("publish", new Action<PythonDictionary>(Publish));

            _mqttServerStorage.Configure();

            _mqttServer.ClientConnectedHandler = _mqttClientConnectedHandler;
            _mqttServer.ClientDisconnectedHandler = _mqttClientDisconnectedHandler;
            _mqttServer.ClientSubscribedTopicHandler = _mqttClientSubscribedTopicHandler;
            _mqttServer.ClientUnsubscribedTopicHandler = _mqttClientUnsubscribedTopicHandler;
        }

        public bool IsStarted()
        {
            return _mqttServer.IsStarted;
        }

        public Task Stop()
        {
            device.OnDiagUpdated -= device_OnDiagUpdated;
            if (_mqttServer.IsStarted)
            {
                return _mqttServer.StopAsync();
            }
            else
                return Task.Delay(0);
        }
        public Task Start()
        {
            if (_mqttServer.IsStarted)
            {
                return Task.Delay(0);
            }
            else
            {
                _mqttServer.StartAsync(CreateMqttServerOptions()).GetAwaiter().GetResult();
                if (settings.MQTTMode == MQTTMode.Bridge || settings.MQTTMode == MQTTMode.Server)
                    device.OnDiagUpdated += device_OnDiagUpdated;

                return _mqttServer.StartAsync(CreateMqttServerOptions());
            }
        }

        public Task RunWebSocketConnectionAsync(WebSocket webSocket, HttpContext httpContext)
        {
            return _webSocketServerAdapter.RunWebSocketConnectionAsync(webSocket, httpContext);
        }

        public Task<IList<IMqttClientStatus>> GetClientStatusAsync()
        {
            return _mqttServer.GetClientStatusAsync();
        }

        public Task<IList<IMqttSessionStatus>> GetSessionStatusAsync()
        {
            return _mqttServer.GetSessionStatusAsync();
        }

        public Task ClearRetainedApplicationMessagesAsync()
        {
            return _mqttServer.ClearRetainedApplicationMessagesAsync();
        }

        public Task<IList<MqttApplicationMessage>> GetRetainedApplicationMessagesAsync()
        {
            return _mqttServer.GetRetainedApplicationMessagesAsync();
        }

        public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null) throw new ArgumentNullException(nameof(applicationMessage));
            if (_mqttServer.IsStarted)
                return _mqttServer.PublishAsync(applicationMessage);
            else
                return Task.Run(() => new MqttClientPublishResult() { ReasonCode = MqttClientPublishReasonCode.UnspecifiedError, ReasonString = "MQTT Server Down" });
        }

        //void Publish(PythonDictionary parameters)
        //{
        //    try
        //    {
        //        var applicationMessageBuilder = new MqttApplicationMessageBuilder()
        //            .WithTopic((string)parameters.get("topic", null))
        //            .WithRetainFlag((bool)parameters.get("retain", false))
        //            .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)(int)parameters.get("qos", 0));

        //        var payload = parameters.get("payload", null);
        //        byte[] binaryPayload;

        //        if (payload == null)
        //        {
        //            binaryPayload = new byte[0];
        //        }
        //        else if (payload is string stringPayload)
        //        {
        //            binaryPayload = Encoding.UTF8.GetBytes(stringPayload);
        //        }
        //        else if (payload is ByteArray byteArray)
        //        {
        //            binaryPayload = byteArray.ToArray();
        //        }
        //        else if (payload is IEnumerable<int> intArray)
        //        {
        //            binaryPayload = intArray.Select(Convert.ToByte).ToArray();
        //        }
        //        else
        //        {
        //            throw new NotSupportedException("Payload type not supported.");
        //        }

        //        applicationMessageBuilder = applicationMessageBuilder
        //            .WithPayload(binaryPayload);

        //        var applicationMessage = applicationMessageBuilder.Build();

        //        _mqttServer.PublishAsync(applicationMessage).GetAwaiter().GetResult();
        //    }
        //    catch (Exception exception)
        //    {
        //        _logger.LogError(exception, "Error while publishing application message from server.");
        //    }
        //}

        IMqttServerOptions CreateMqttServerOptions()
        {
            var options = new MqttServerOptionsBuilder()
                .WithMaxPendingMessagesPerClient(_mqttsettings.MaxPendingMessagesPerClient)
                .WithDefaultCommunicationTimeout(TimeSpan.FromSeconds(_mqttsettings.CommunicationTimeout))
                .WithConnectionValidator(_mqttConnectionValidator)
                .WithApplicationMessageInterceptor(_mqttApplicationMessageInterceptor)
                .WithSubscriptionInterceptor(_mqttSubscriptionInterceptor)
                .WithUnsubscriptionInterceptor(_mqttUnsubscriptionInterceptor)
                .WithStorage(_mqttServerStorage);

            // Configure unencrypted connections
            if (_mqttsettings.TcpEndPoint.Enabled)
            {
                options.WithDefaultEndpoint();

                if (_mqttsettings.TcpEndPoint.TryReadIPv4(out var address4))
                {
                    options.WithDefaultEndpointBoundIPAddress(address4);
                }

                if (_mqttsettings.TcpEndPoint.TryReadIPv6(out var address6))
                {
                    options.WithDefaultEndpointBoundIPV6Address(address6);
                }

                if (_mqttsettings.TcpEndPoint.Port > 0)
                {
                    options.WithDefaultEndpointPort(_mqttsettings.TcpEndPoint.Port);
                }
            }
            else
            {
                options.WithoutDefaultEndpoint();
            }

            // Configure encrypted connections
            if (_mqttsettings.EncryptedTcpEndPoint.Enabled)
            {
#if NETCOREAPP3_1 || NET5_0
                options
                    .WithEncryptedEndpoint()
                    .WithEncryptionSslProtocol(SslProtocols.Tls13);
#else
                options
                    .WithEncryptedEndpoint()
                    .WithEncryptionSslProtocol(SslProtocols.Tls12);
#endif

                if (!string.IsNullOrEmpty(_mqttsettings.EncryptedTcpEndPoint?.Certificate?.Path))
                {
                    IMqttServerCertificateCredentials certificateCredentials = null;

                    if (!string.IsNullOrEmpty(_mqttsettings.EncryptedTcpEndPoint?.Certificate?.Password))
                    {
                        certificateCredentials = new MqttServerCertificateCredentials
                        {
                            Password = _mqttsettings.EncryptedTcpEndPoint.Certificate.Password
                        };
                    }

                    options.WithEncryptionCertificate(_mqttsettings.EncryptedTcpEndPoint.Certificate.ReadCertificate(), certificateCredentials);
                }

                if (_mqttsettings.EncryptedTcpEndPoint.TryReadIPv4(out var address4))
                {
                    options.WithEncryptedEndpointBoundIPAddress(address4);
                }

                if (_mqttsettings.EncryptedTcpEndPoint.TryReadIPv6(out var address6))
                {
                    options.WithEncryptedEndpointBoundIPV6Address(address6);
                }

                if (_mqttsettings.EncryptedTcpEndPoint.Port > 0)
                {
                    options.WithEncryptedEndpointPort(_mqttsettings.EncryptedTcpEndPoint.Port);
                }
            }
            else
            {
                options.WithoutEncryptedEndpoint();
            }

            if (_mqttsettings.ConnectionBacklog > 0)
            {
                options.WithConnectionBacklog(_mqttsettings.ConnectionBacklog);
            }

            if (_mqttsettings.EnablePersistentSessions)
            {
                options.WithPersistentSessions();
            }

            return options.Build();
        }
    }
}