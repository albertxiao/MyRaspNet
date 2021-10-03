using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyRaspNet.Hubs
{
    public class DiagHub : Hub
    {
        public async Task SendDiag(double CPULoad, double MemoryLoad, double CPUTemp)
        {
            var jsonData = JsonConvert.SerializeObject(new { Time = DateTime.Now, CPULoad, MemoryLoad, CPUTemp });
            if (Clients != null)
                await Clients.All.SendAsync("Diag", jsonData).ConfigureAwait(false);
        }

    }
}
