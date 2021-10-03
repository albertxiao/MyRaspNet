using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyRaspNet.Pages
{
    public class GPIOModel : PageModel
    {
        private readonly ILogger<GPIOModel> logger;
        private readonly RaspberryDevice rasp;

        public string[] SerialPortNames { get; set; }

        public RaspberryPinMap[] PinMaps
        {
            get { return rasp.PinMap; }
        }

        public GPIOModel(ILogger<GPIOModel> logger, RaspberryDevice rasp)
        {
            this.logger = logger;
            this.rasp = rasp;
        }

        public void OnGet()
        {
            SerialPortNames = rasp.GetAvailablePort();
        }
        public IActionResult OnPostOpenPin(int pinNo, string pinMode)
        {
            var pin = rasp.PinMap.Where(t => t.PinNo == pinNo).FirstOrDefault();
            var mode = System.Device.Gpio.PinMode.Input;
            bool result = false;
            bool? enabled = null;
            if (Enum.TryParse<System.Device.Gpio.PinMode>(pinMode, out mode) && pin != null)
            {
                result = pin.OpenPin(mode);
                enabled = pin.IsPinModeEnabled;
                rasp.UpdateConfig();
            }
            return new JsonResult(new { pinNo = pinNo, pinMode = pinMode, enabled = enabled, result = result });
        }
        public IActionResult OnPostClosePin(int pinNo)
        {
            bool result = false;
            var pin = rasp.PinMap.Where(t => t.PinNo == pinNo).FirstOrDefault();
            bool? enabled = null;
            if (pin != null)
            {
                result = pin.ClosePin();
                enabled = pin.IsPinModeEnabled;
                rasp.UpdateConfig();
            }
            return new JsonResult(new { pinNo = pinNo, enabled = enabled, result = result });
        }
        public IActionResult OnPostSetPinValue(int pinNo, bool pinValue)
        {
            bool result = false;
            var pin = rasp.PinMap.Where(t => t.PinNo == pinNo).FirstOrDefault();
            bool? enabled = null;
            if (pin != null && pin.IsPinModeEnabled)
            {
                enabled = pin.IsPinModeEnabled;
                if (pin.CurrentPinMode() == System.Device.Gpio.PinMode.Output)
                {
                    if (pinValue)
                        pin.WritePinValue(System.Device.Gpio.PinValue.High);
                    else
                        pin.WritePinValue(System.Device.Gpio.PinValue.Low);
                    rasp.UpdateConfig();
                    result = true;
                }
            }
            return new JsonResult(new { pinNo = pinNo, enabled = enabled, pinValue = pinValue, result = result });
        }
    }
}
