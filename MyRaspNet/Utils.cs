using MyRaspNet.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyRaspNet
{
    public static class Utils
    {
        public static string CONFIGKEY_APPSETTINGS = "MyRaspNet";
        public static string CONFIGKEY_MQTTClient = "MQTTClient";
        public static string CONFIGKEY_MQTTServer = "MQTT";
        public static string CONFIGKEY_RASPBERRY = "Raspberry";

        public static void ReadFileLineByLine(string filePath, Func<string, bool> funReadLine)
        {
            using (var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var sr = new StreamReader(fs))
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if (funReadLine(line))
                            break;
                    }
                }
            }
        }

        public static T Clone<T>(this T value) where T : class, new()
        {
            T result = Activator.CreateInstance<T>();
            value.CopyTo(result);
            return result;
        }

        public static void CopyTo<T>(this T source, T target) where T : class
        {
            var piArr = typeof(T).GetProperties();
            foreach (var pi in piArr)
            {
                if (pi.CanWrite && pi.CanRead)
                    pi.SetValue(target, pi.GetValue(source));
            }
        }
        public static void CopyPropertiesTo<S, T>(this S source, T target) 
            where T : class
            where S : class
        {
            var piSArr = typeof(S).GetProperties();
            var piTArr = typeof(T).GetProperties();

            var piJoin = (from piS in piSArr
                          join piT in piTArr on new { piS.Name, piS.PropertyType } equals new { piT.Name, piT.PropertyType }
                          where piS.CanRead && piT.CanWrite
                          select new { piS, piT }).ToArray();
            foreach (var pi in piJoin)
            {
                pi.piT.SetValue(target, pi.piS.GetValue(source));
            }
        }

        public static System.Device.Gpio.PinValue ToPinValue(this PinValue2 val)
        {
            if (val == PinValue2.HIGH)
                return System.Device.Gpio.PinValue.High;
            else
                return System.Device.Gpio.PinValue.Low;
        }
        public static PinValue2 ToPinValue2(this System.Device.Gpio.PinValue val)
        {
            if (val == System.Device.Gpio.PinValue.High)
                return PinValue2.HIGH;
            else
                return PinValue2.LOW;
        }

        public static T ValueOrDefault<T>(this JToken value)
        {
            if (value != null)
                return value.Value<T>();
            else
                return default(T);
        }
    }
}
