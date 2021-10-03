using System;
using MyRaspNet.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;

namespace MyRaspNet.Mqtt
{
    public class MqttApplicationMessageInterceptor : IMqttServerApplicationMessageInterceptor
    {
        private readonly ILogger logger;
        private readonly AppSettings settings;
        private readonly MqttSettingsModel mqttSettings;
        private MqttClientService client;
        private readonly IServiceProvider provider;
        private RaspberryDevice device;

        public MqttApplicationMessageInterceptor(IServiceProvider provider, AppSettings settings, MqttSettingsModel mqttSettings, ILogger<MqttApplicationMessageInterceptor> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.mqttSettings = mqttSettings ?? throw new ArgumentNullException(nameof(mqttSettings));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.provider = provider;
        }


        public Task InterceptApplicationMessagePublishAsync(MqttApplicationMessageInterceptorContext context)
        {
            try
            {
                // This might be not set when a message was published by the server instead of a client.
                context.SessionItems.TryGetValue(MqttServerConnectionValidator.WrappedSessionItemsKey, out var sessionItems);

                if (settings.MQTTMode == MQTTMode.Bridge)
                {
                    if (client == null)
                        client = provider.GetService<MqttClientService>();
                    if (client != null && client.IsStarted())
                    {
                        if (string.IsNullOrEmpty(context.ClientId))
                        {
                            if (MQTTnet.Server.MqttTopicFilterComparer.IsMatch(context.ApplicationMessage.Topic, string.Format("{0}/{1}/#", settings.MQTTTopic, AppSettings.TeleTopic)))
                            {
                                // Raspberry Telemetry
                                client.PublishAsync(context.ApplicationMessage.Topic, context.ApplicationMessage.Payload, context.ApplicationMessage.QualityOfServiceLevel, context.ApplicationMessage.Retain);
                            }
                        }
                        else if (MQTTnet.Server.MqttTopicFilterComparer.IsMatch(context.ApplicationMessage.Topic, string.Format("{0}/{1}/#", settings.MQTTTopic, AppSettings.CommandTopic)))
                        {
                            if (device == null)
                                device = provider.GetService<RaspberryDevice>();
                            if (device != null)
                            {
                                var cmdNames = context.ApplicationMessage.Topic.Remove(0, string.Format("{0}/{1}/", settings.MQTTTopic, AppSettings.CommandTopic).Length);
                                device.ExecuteCommandAsync(cmdNames, context.ApplicationMessage.ConvertPayloadToString()).ConfigureAwait(false);
                            }
                           
                        }
                        else if (!MQTTnet.Server.MqttTopicFilterComparer.IsMatch(context.ApplicationMessage.Topic, string.Format("{0}/#", settings.MQTTTopic)))
                        {
                            // IoT Telemetry
                            var newTopic = string.Format("{0}/{1}/out", settings.MQTTTopic, AppSettings.IoTTopic);
                            if (context.ApplicationMessage.Topic.StartsWith("/"))
                                newTopic += context.ApplicationMessage.Topic;
                            else
                                newTopic += string.Format("/{0}", context.ApplicationMessage.Topic);
                            client.PublishAsync(newTopic, context.ApplicationMessage.Payload, context.ApplicationMessage.QualityOfServiceLevel, context.ApplicationMessage.Retain);
                        }
                    }
                }
                else if (MQTTnet.Server.MqttTopicFilterComparer.IsMatch(context.ApplicationMessage.Topic, string.Format("{0}/{1}/#", settings.MQTTTopic, AppSettings.CommandTopic)))
                {
                    if (device == null)
                        device = provider.GetService<RaspberryDevice>();
                    if (device != null)
                    {
                        var cmdNames = context.ApplicationMessage.Topic.Remove(0, string.Format("{0}/{1}/", settings.MQTTTopic, AppSettings.CommandTopic).Length);
                        device.ExecuteCommandAsync(cmdNames, context.ApplicationMessage.ConvertPayloadToString()).ConfigureAwait(false);
                    }

                }
                //if (client.IsStarted)
                //{

                //}
                //var pythonContext = new PythonDictionary
                //{
                //    { "client_id", context.ClientId },
                //    { "session_items", sessionItems },
                //    { "retain", context.ApplicationMessage.Retain },
                //    { "accept_publish", context.AcceptPublish },
                //    { "close_connection", context.CloseConnection },
                //    { "topic", context.ApplicationMessage.Topic },
                //    { "qos", (int)context.ApplicationMessage.QualityOfServiceLevel }
                //};

                //_pythonScriptHostService.InvokeOptionalFunction("on_intercept_application_message", pythonContext);

                //context.AcceptPublish = (bool)pythonContext.get("accept_publish", context.AcceptPublish);
                //context.CloseConnection = (bool)pythonContext.get("close_connection", context.CloseConnection);
                //context.ApplicationMessage.Topic = (string)pythonContext.get("topic", context.ApplicationMessage.Topic);
                //context.ApplicationMessage.QualityOfServiceLevel = (MqttQualityOfServiceLevel)(int)pythonContext.get("qos", (int)context.ApplicationMessage.QualityOfServiceLevel);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error while intercepting application message.");
            }

            return Task.CompletedTask;
        }
    }
}