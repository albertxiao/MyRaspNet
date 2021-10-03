using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyRaspNet.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> logger;
        private readonly RaspberryDevice rasp;
        public RaspberryInfo SystemInfo => rasp != null ? rasp.Info : null;

        public IndexModel(ILogger<IndexModel> logger, RaspberryDevice rasp)
        {
            this.logger = logger;
            this.rasp = rasp;

        }
        public IActionResult OnPostUpdateDiag()
        {
            return new JsonResult(new { rasp.Info.CPULoad, rasp.Info.CPUTemp, rasp.Info.MemoryLoad });
        }
    }
}
