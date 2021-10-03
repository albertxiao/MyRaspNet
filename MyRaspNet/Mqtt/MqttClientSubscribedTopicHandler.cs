using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet.Server;

namespace MyRaspNet.Mqtt
{
    public class MqttClientSubscribedTopicHandler : IMqttServerClientSubscribedTopicHandler
    {
        private readonly ILogger _logger;

        public MqttClientSubscribedTopicHandler(ILogger<MqttClientSubscribedTopicHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task HandleClientSubscribedTopicAsync(MqttServerClientSubscribedTopicEventArgs eventArgs)
        {
            try
            {
                //var pythonEventArgs = new PythonDictionary
                //{
                //    { "client_id", eventArgs.ClientId },
                //    { "topic", eventArgs.TopicFilter.Topic },
                //    { "qos", (int)eventArgs.TopicFilter.QualityOfServiceLevel }
                //};

                //_pythonScriptHostService.InvokeOptionalFunction("on_client_subscribed_topic", pythonEventArgs);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error while handling client subscribed topic event.");
            }

            return Task.CompletedTask;
        }
    }
}
