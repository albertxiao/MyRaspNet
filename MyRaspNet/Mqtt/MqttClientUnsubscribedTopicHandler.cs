using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet.Server;

namespace MyRaspNet.Mqtt
{
    public class MqttClientUnsubscribedTopicHandler : IMqttServerClientUnsubscribedTopicHandler
    {
        private readonly ILogger _logger;

        public MqttClientUnsubscribedTopicHandler(ILogger<MqttClientUnsubscribedTopicHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task HandleClientUnsubscribedTopicAsync(MqttServerClientUnsubscribedTopicEventArgs eventArgs)
        {
            try
            {
                //var pythonEventArgs = new PythonDictionary
                //{
                //    { "client_id", eventArgs.ClientId },
                //    { "topic", eventArgs.TopicFilter }
                //};

                //_pythonScriptHostService.InvokeOptionalFunction("on_client_unsubscribed_topic", pythonEventArgs);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error while handling client unsubscribed topic event.");
            }

            return Task.CompletedTask;
        }
    }
}
