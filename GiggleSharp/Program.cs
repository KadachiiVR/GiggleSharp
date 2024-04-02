using System;
using VRC.OSCQuery;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Configuration.Ini;
using Rug.Osc;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using GiggleSharp;

namespace Giggletech.OSCRouter
{
    class Router
    {
        private static Router _instance = null;
        public static Router Instance
        {
            get
            {
                if (_instance == null ) { _instance = new Router(); }
                return _instance;
            }
        }

        public OscListener listener;
        public OSCQueryService queryService;
        private HashSet<string> existingAddresses;
        public int tcpPort, udpPort;
        static void Main(string[] args)
        {
            Config cfg = new Config("./config.ini");

            Instance.queryService = new OSCQueryServiceBuilder()
                .WithTcpPort(Instance.tcpPort)
                .WithUdpPort(Instance.udpPort)
                .WithServiceName("GiggleTech OSC Router CSharp Edition")
                .WithDefaults()
                .Build();

            Instance.listener = new OscListener(Instance.udpPort);

            Dictionary<string, HapticZone> zones = new Dictionary<string, HapticZone>();

            foreach (string zName in cfg.Sections)
            {
                var cfgs = cfg.GetSection(zName);
                HapticZone zone;
                switch(cfgs["type"])
                {
                    case "VelocityZone":
                        zone = new VelocityZone(cfgs);
                        break;
                    case "SimpleProximityZone":
                        zone = new SimpleProximityZone(cfgs);
                        break;
                    default:
                        throw new InvalidDataException($"{cfgs["type"]} is not a valid haptic zone type.");
                }
                zones.Add(zName, zone);
            }
            ConsoleDisplay.Instance.prefix = new[]
            {
                "GiggleTech OSC Router C# Edition",
                $"OSCQuery Service active at http://localhost:{Instance.tcpPort}",
                $"Listening for OSC packets on port {Instance.udpPort}"
            };

            ConsoleDisplay.Instance.appendix = new[] { "To close, press Enter or Escape" };

            //var tcpPort = Extensions.GetAvailableTcpPort();
            //var udbPort = Extensions.GetAvailableUdpPort();
            //using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
            //var logger = factory.CreateLogger<OSCQueryService>();
            //var qservice = new OSCQueryServiceBuilder().WithDefaults().WithTcpPort(tcpPort).WithUdpPort(udbPort).WithServiceName("giggle test").WithLogger(logger).Build();
            //Console.WriteLine(tcpPort + ":" + udbPort);
            //Console.ReadKey();
            ////_receiver = new OscReceiver()



            //oscQuery.AddEndpoint<float>("/avatar/parameters/motor", Attributes.AccessValues.WriteOnly);
            //oscQuery.AddEndpoint<float>("/avatar/parameters/proximity_01", Attributes.AccessValues.WriteOnly);
            //var rec = new OscReceiver(udpPort);
            //rec.Connect();

            // Manually logging the ports to see them without a logger
            //Console.WriteLine($"Started OSCQueryService at TCP {tcpPort}");
            Instance.listener.Connect();
            ConsoleDisplay.Instance.RefreshDisplay();
            //Console.WriteLine($"Listening for OSC messages at UDP {udpPort}");
            //while (true)
            //{
            //    OscPacket packet = rec.Receive();
            //    Console.WriteLine(packet.ToString());
            //}

            // Stops the program from ending until a key is pressed
            while (true)
            {
                var key = Console.ReadKey();
                Console.WriteLine(key);
                if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Escape)
                {
                    Instance.listener.Dispose();
                    Instance.queryService.Dispose();
                    return;
                }
            }
        }

        public Router()
        {
            this.existingAddresses = new HashSet<string>();
            this.tcpPort = Extensions.GetAvailableTcpPort();
            this.udpPort = Extensions.GetAvailableUdpPort();
        }

        public void AddEndpoint<T>(string address, OscMessageEvent onMessage)
        {
            if (!this.existingAddresses.Contains(address))
            {
                this.queryService.AddEndpoint<T>(address, Attributes.AccessValues.WriteOnly);
                this.existingAddresses.Add(address);
            }
            this.listener.Attach(address, onMessage);
        }

    }
}