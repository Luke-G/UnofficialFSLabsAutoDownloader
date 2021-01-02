using System;
using System.Collections.Generic;
using System.Linq;
using FlightSimLabsDownloader.Entities;
using Microsoft.Win32;

namespace FlightSimLabsDownloader.Services
{
    public class LicenceManager
    {
        private readonly IEnumerable<Licence> licences;

        public LicenceManager()
        {
            licences = GetLicencesFromRegistry();
        }

        private static IEnumerable<Licence> GetLicencesFromRegistry()
        {
            var licencesFound = new List<Licence>();
            var registryKeys = new List<string>();

            var aircraft = new[] { "A319X", "A320X", "A321X" };
            var simulators = new[] { "P3Dv4", "P3Dv5" };

            foreach (string simulator in simulators)
                registryKeys.AddRange(aircraft.Select(q => $"{q}-{simulator}"));

            var map = new Dictionary<string, FlightSimLabsProduct>
            {
                { "A319X", FlightSimLabsProduct.A319X },
                { "A320X", FlightSimLabsProduct.A320X },
                { "A321X", FlightSimLabsProduct.A321X },
            };

            foreach (string registryKey in registryKeys)
            {
                try
                {
                    RegistryKey subKey = Registry.CurrentUser.OpenSubKey($@"SOFTWARE\FlightSimLabs\{registryKey}");

                    if (subKey == null)
                        continue;

                    var licence = new Licence
                    {
                        EmailAddress = subKey.GetValue("Email").ToString(),
                        OrderId = subKey.GetValue("OrderId").ToString(),
                        SerialKey = subKey.GetValue("SerialNumber_FS").ToString(),
                        Product = map.GetValueOrDefault(registryKey.Split('-').First()),
                        Simulator = registryKey.Split('-').Last()
                    };

                    if (licence.IsValid())
                        licencesFound.Add(licence);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return licencesFound;
        }

        public IEnumerable<Licence> GetLicences()
        {
            return licences;
        }
    }
}