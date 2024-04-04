using GiggleSharp;
using VRC.OSCQuery;
using System.Net.Http;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Rug.Osc;
using System.Text.Json;
using System.Net;
using System.Collections;


namespace GiggleSharpOscSim
{
    internal class Program
    {
        public static OSCQueryServiceProfile? target = null;
        public static Config cfg = null;
        public static IPAddress oscIp;
        public static int oscPort;
        static void Main(string[] args)
        {
            var queryService = new OSCQueryServiceBuilder()
                .WithServiceName("GiggleSharp OSC Sim")
                .WithDiscovery(new MeaModDiscovery())
                .Build();

            var services = queryService.GetOSCQueryServices();

            queryService.OnOscQueryServiceAdded += OnServiceAdded;

            foreach (var profile in services)
            {
                OnServiceAdded(profile);
            }

            while (target == null) Thread.Sleep(500);
            queryService.OnOscQueryServiceAdded -= OnServiceAdded;

            Console.WriteLine($"Connecting to GiggleSharp OSC Service at {target.address}:{target.port}");
            var task = GetTargetInfo();
            task.Wait();

            Console.WriteLine($"Preparing to send commands to {oscIp}:{oscPort}");
            cfg = new Config(".\\config.ini");

            string motorSpeedParam = $"/avatar/parameters/{cfg.GetSection(cfg.Sections.First())["parameter_max_speed"]}";

            HashSet<string> oscParams = new HashSet<string>();

            foreach (string sectionName in cfg.Sections)
            {
                var section = cfg.GetSection(sectionName);
                foreach (string k in section.Keys)
                {
                    if (k.StartsWith("parameter") && k != "parameter_max_speed")
                    {
                        oscParams.Add($"/avatar/parameters/{section[k]}");
                    }
                }
            }

            foreach (string oscParam in oscParams)
            {
                Console.WriteLine($"About to sweep {oscParam} at 20% motor speed. Press Enter to continue.");
                Console.ReadLine();
                SweepParameter(motorSpeedParam, .1f, 1f, .2f);
                SweepParameter(oscParam, .1f, 0f, 1f, 0f);
                Console.WriteLine();
                Console.WriteLine($"About to sweep {oscParam} at 100% motor speed. Press Enter to continue.");
                Console.ReadLine();
                SweepParameter(motorSpeedParam, .1f, .2f, 1f);
                SweepParameter(oscParam, .1f, 0f, 1f, 0f);
                Console.WriteLine();
            }
        }

        public static async Task GetTargetInfo()
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri($"http://{target.address}:{target.port}");
            var result = await httpClient.GetAsync("/?HOST_INFO");
            var response = await result.EnsureSuccessStatusCode().Content.ReadAsStringAsync();
            Console.WriteLine(response);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
            oscIp = IPAddress.Parse((string)dict["OSC_IP"]);
            oscPort = (int)((long)dict["OSC_PORT"]);
        }

        public static void SweepParameter(string address, float step, params float[] targets)
        {
            List<float> sweep = new List<float>();
            sweep.Add(targets[0]);
            foreach (int i in Enumerable.Range(0, targets.Length - 1))
            {
                float u = targets[i];
                float v = targets[i + 1];
                float signedStep = Math.Sign(v - u);
                for (float x = u + (signedStep * step); x * signedStep <= v * signedStep; x = (float)Math.Round((x + (signedStep * step)) * 10) / 10f)
                {
                    sweep.Add(x);
                }
            }

            var sender = new OscSender(oscIp, oscPort);
            sender.Connect();
            foreach (float x in sweep)
            {
                Console.WriteLine($"- Setting {address} to {x}");
                sender.Send(new OscMessage(address, x));
                Thread.Sleep(100);
            }
        }

        public static void OnServiceAdded(OSCQueryServiceProfile profile)
        {
            if (profile.name == "GiggleTech OSC Router CSharp Edition")
            {
                target = profile;
            }
        }
    }
}