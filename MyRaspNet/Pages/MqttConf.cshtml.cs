using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using MyRaspNet.Configuration;

namespace MyRaspNet.Pages
{
    public class MqttConfModel : PageModel
    {
        private readonly AppSettings settings;
        private readonly MqttSettingsModel serverSettings;
        private readonly MqttClientSettings clientSettings;
        private readonly WritableConfiguration writer;

        private readonly RaspberryDevice device;

        private readonly Mqtt.MqttServerService server;
        private readonly Mqtt.MqttClientService client;

        [BindProperty]
        public AppSettings Setting { get; set; }
        [BindProperty]
        public MqttClientSettings ClientSetting { get; set; }
        [BindProperty]
        public MqttSettingsModel ServerSetting { get; set; }

        public MqttConfModel(AppSettings settings,
            MqttSettingsModel serverSettings,
            MqttClientSettings clientSettings,
            RaspberryDevice device,
            WritableConfiguration writer,
            Mqtt.MqttServerService server,
            Mqtt.MqttClientService client)
        {
            this.settings = settings;
            this.serverSettings = serverSettings;
            this.clientSettings = clientSettings;
            this.writer = writer;
            this.device = device;
            this.server = server;
            this.client = client;
        }
        public void OnGet()
        {
            Setting = settings.Clone();
            ClientSetting = clientSettings.Clone();
            ServerSetting = serverSettings.Clone();
        }
        public async void OnPostAsync()
        {
            var settingsLatest = writer.Update<AppSettings>(o =>
            {
                o.DeviceName = Setting.DeviceName;
                o.MQTTMode = Setting.MQTTMode;
                o.MQTTTopicTemplate = Setting.MQTTTopicTemplate;
            }, Utils.CONFIGKEY_APPSETTINGS);
            var clientSettingsLatest = writer.Update<MqttClientSettings>(o =>
           {
               o.Host = ClientSetting.Host;
               o.Port = ClientSetting.Port;
               o.ClientIdTemplate = ClientSetting.ClientIdTemplate;
               o.User = ClientSetting.User;
               o.Password = ClientSetting.Password;
           }, Utils.CONFIGKEY_MQTTClient);

            await server.Stop();
            await client.Stop();

            // copy to singleton config service
            settingsLatest.CopyPropertiesTo(settings);
            clientSettingsLatest.CopyPropertiesTo(clientSettings);

            settings.UpdateMqttTopic(device);
            clientSettings.UpdateClientId(settings, device);


            Program.Shutdown();
            //int wait = 0;
            //int timelimit = 5000;
            //switch (settings.MQTTMode)
            //{
            //    case MQTTMode.Disabled:
            //        break;
            //    case MQTTMode.Server:
            //        while (wait <= timelimit)
            //        {
            //            Thread.Sleep(200);
            //            wait += 200;
            //            if (!server.IsStarted())
            //            {
            //                await server.Start();
            //                break;
            //            }
            //        }
            //        break;
            //    case MQTTMode.Bridge:
            //        wait = 0;

            //        while (wait < timelimit)
            //        {
            //            Thread.Sleep(200);
            //            wait += 200;
            //            if (!server.IsStarted())
            //            {
            //                await server.Start();
            //                break;
            //            }
            //        }
            //        wait = 0;
            //        while (wait <= timelimit)
            //        {
            //            Thread.Sleep(200);
            //            wait += 200;
            //            if (!client.IsStarted())
            //            {
            //                await client.Start();
            //                break;
            //            }
            //        }
            //        break;
            //    case MQTTMode.Client:
            //        wait = 0;
            //        while (wait <= timelimit)
            //        {
            //            Thread.Sleep(200);
            //            wait += 200;
            //            if (!client.IsStarted())
            //            {
            //                await client.Start();
            //                break;
            //            }
            //        }
            //        break;
            //    default:
            //        break;
            //}

        }
    }
}
