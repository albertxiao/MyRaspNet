using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyRaspNet.Hubs
{
    public class GPIOHub : Hub
    {
        public async Task SendModeChanged(int pinNo, string pinMode, bool enabled)
        {
            var jsonData = JsonConvert.SerializeObject(new { PinNo = pinNo, PinMode = pinMode, Enabled = enabled });
            if (Clients != null)
                await Clients.All.SendAsync("ModeChanged", jsonData).ConfigureAwait(false);
        }
        public async Task SendValueChanged(int pinNo, bool pinValue)
        {
            var jsonData = JsonConvert.SerializeObject(new { PinNo = pinNo, PinValue = pinValue });
            if (Clients != null)
                await Clients.All.SendAsync("ValueChanged", jsonData).ConfigureAwait(false);
        }
    }
}
