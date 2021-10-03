using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyRaspNet.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace MyRaspNet.Mqtt
{
    public class MqttServerConnectionValidator : IMqttServerConnectionValidator
    {
        public const string WrappedSessionItemsKey = "WRAPPED_ITEMS";

        //private readonly PythonScriptHostService _pythonScriptHostService;
        private readonly ILogger _logger;
        private readonly MqttSettingsModel _settings;

        public MqttServerConnectionValidator(MqttSettingsModel mqttSettings, ILogger<MqttServerConnectionValidator> logger)
        {
            //_pythonScriptHostService = pythonScriptHostService ?? throw new ArgumentNullException(nameof(pythonScriptHostService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = mqttSettings ?? throw new ArgumentNullException(nameof(mqttSettings));
        }

        public Task ValidateConnectionAsync(MqttConnectionValidatorContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(context.ClientId))
                {
                    context.ReasonCode = MqttConnectReasonCode.ClientIdentifierNotValid;
                }
                //context.SessionItems[WrappedSessionItemsKey]
                //var sessionItems = new PythonDictionary();

                //var pythonContext = new PythonDictionary
                //{
                //    { "endpoint", context.Endpoint },
                //    { "is_secure_connection", context.IsSecureConnection },
                //    { "client_id", context.ClientId },
                //    { "username", context.Username },
                //    { "password", context.Password },
                //    { "raw_password", new Bytes(context.RawPassword ?? new byte[0]) },
                //    { "clean_session", context.CleanSession},
                //    { "authentication_method", context.AuthenticationMethod},
                //    { "authentication_data", new Bytes(context.AuthenticationData ?? new byte[0]) },
                //    { "session_items", sessionItems },

                //    { "result", PythonConvert.Pythonfy(context.ReasonCode) }
                //};

                //_pythonScriptHostService.InvokeOptionalFunction("on_validate_client_connection", pythonContext);

                //context.ReasonCode = PythonConvert.ParseEnum<MqttConnectReasonCode>((string)pythonContext["result"]);

                //context.SessionItems[WrappedSessionItemsKey] = sessionItems;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error while validating client connection.");

                context.ReasonCode = MqttConnectReasonCode.UnspecifiedError;
            }

            return Task.CompletedTask;
        }
    }
}
