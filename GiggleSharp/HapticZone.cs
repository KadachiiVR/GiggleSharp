using Giggletech.OSCRouter;
using Microsoft.Extensions.Configuration;
using Rug.Osc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.Intrinsics.X86;

namespace GiggleSharp
{
    internal abstract class OSCParameter<T>
    {
        protected const string RX_PREFIX = "/avatar/parameters/";

        public delegate void OSCParameterUpdateHandler(T value);
        public T Value { get; set; }
        public DateTime lastUpdated { get; protected set; }
        public string Address { get; protected set; }
        private OSCParameterUpdateHandler onUpdated;
        public OSCParameter(Dictionary<string, string> cfg, string cfg_key, OSCParameterUpdateHandler onUpdated, bool optional = false)
        {
            this.Address = RX_PREFIX + cfg.GetValueOrDefault(cfg_key, string.Empty);
            this.onUpdated = onUpdated;
            Router.Instance.AddEndpoint<T>(this.Address, this.OnMessage);
        }

        public abstract float NormalizeValue(T value);

        public virtual void OnMessage(OscMessage message)
        {
            T oldValue = this.Value;
            this.Value = (T)message.First();

            if (!this.Value.Equals(oldValue))
            {
                this.lastUpdated = DateTime.UtcNow;
                this.onUpdated.Invoke(this.Value);
                ConsoleDisplay.Instance.UpdateChannel(this.Address, this.NormalizeValue(this.Value));
            }
        }
    }

    internal class OSCBoolParameter : OSCParameter<bool>
    {
        public OSCBoolParameter(Dictionary<string, string> cfg, string cfg_key, OSCParameterUpdateHandler onUpdated, bool optional = false) : base(cfg, cfg_key, onUpdated, optional)
        {
            ConsoleDisplay.Instance.RegisterChannel(this.Address, 1f);
        }

        public override float NormalizeValue(bool value)
        {
            return value ? 1f : 0f;
        }
    }

    internal class OSCIntParameter : OSCParameter<int>
    {
        public OSCIntParameter(Dictionary<string, string> cfg, string cfg_key, OSCParameterUpdateHandler onUpdated, bool optional = false) : base(cfg, cfg_key, onUpdated, optional)
        {
            ConsoleDisplay.Instance.RegisterChannel(this.Address, 255f);
        }

        public override float NormalizeValue(int value)
        {
            return value;
        }
    }

    internal class OSCFloatParameter : OSCParameter<float>
    {
        public float Velocity { get; private set; }
        public OSCFloatParameter(Dictionary<string, string> cfg, string cfg_key, OSCParameterUpdateHandler onUpdated, bool optional = false) : base(cfg, cfg_key, onUpdated, optional)
        {
            ConsoleDisplay.Instance.RegisterChannel(this.Address, 1f);
        }

        public override float NormalizeValue(float value)
        {
            return value;
        }

        public override void OnMessage(OscMessage message)
        {
            float oldValue = this.Value;
            float newValue = (float)message.First();
            float dt = (float)(DateTime.UtcNow - this.lastUpdated).TotalSeconds;
            Console.Write(dt);
            this.Velocity = (newValue - oldValue) / dt;
            base.OnMessage(message);
        }
    }

    internal abstract class HapticZone
    {
        protected string name;
        protected float timeout;
        protected DateTime lastTimedOut = DateTime.MinValue;
        protected DateTime lastUpdate = DateTime.MinValue;
        protected Thread timeoutThread;
        protected const float MOTOR_SPEED_SCALE = 0.66f;
        protected const float MAX_SPEED_LOW_LIMIT = 0.05f;
        protected const int MOTOR_PORT = 8888;
        protected const string TX_ADDR_1 = "/avatar/parameters/motor";
        protected const string TX_ADDR_2 = "/motor";

        public HapticZone(Dictionary<string, string> cfg)
        {
            this.name = cfg["name"];
            this.timeout = float.Parse(cfg["timeout"]);
            this.timeoutThread = new Thread(this.CheckTimeoutLoop);
            this.timeoutThread.Start();
        }

        public void MultiBroadcastZero()
        {
            Thread spammer = new Thread(() =>
            {
                foreach (var _ in Enumerable.Range(0, 5))
                {
                    BroadcastZero();
                    Thread.Sleep(10);
                }
            });

            spammer.Start();
        }

        public abstract void BroadcastZero();

        public void CheckTimeoutLoop()
        {
            while (true)
            {
                var now = DateTime.UtcNow;
                if (lastTimedOut != lastUpdate && (now - lastUpdate).TotalSeconds > timeout)
                {
                    Console.WriteLine("Zeroing out motors");
                    MultiBroadcastZero();
                    lastTimedOut = lastUpdate;
                }
                Thread.Sleep(500);
            }
        }
    }

    internal abstract class SingleChannelHapticZone : HapticZone
    {
        protected List<OscSender> senders;
        protected OSCFloatParameter proximity;
        protected OSCFloatParameter maxSpeed;

        public SingleChannelHapticZone(Dictionary<string, string> cfg) : base(cfg)
        {
            this.proximity = new OSCFloatParameter(cfg, "parameter_proximity", this.OnProximityUpdate);
            this.maxSpeed = new OSCFloatParameter(cfg, "parameter_max_speed", this.OnMaxSpeedUpdate);
            this.maxSpeed.Value = float.Parse(cfg["max_speed"]) / 100f;

            var ips_string = cfg.GetValueOrDefault("device_ips", string.Empty);
            this.senders = new List<OscSender>();
            foreach (string s in ips_string.Split(' ', StringSplitOptions.TrimEntries))
            {
                var ip = IPAddress.Parse(s);
                var sender = new OscSender(ip, MOTOR_PORT);
                sender.Connect();
                this.senders.Add(sender);
            }
            ConsoleDisplay.Instance.RegisterChannel($"Zone: {this.name}", 255f);
        }

        public override void BroadcastZero()
        {
            TransmitToMotors(0);
        }
        public void TransmitToMotors()
        {
            int value = CalculateTxSignal();
            TransmitToMotors(value);
        }

        public void TransmitToMotors(int value)
        {
            ConsoleDisplay.Instance.UpdateChannel($"Zone: {this.name}", value);
            foreach (OscSender sender in this.senders)
            {
                sender.Send(new OscMessage(TX_ADDR_1, value));
                sender.Send(new OscMessage(TX_ADDR_2, value));
            }
        }

        public void OnProximityUpdate(float value)
        {
            this.lastUpdate = DateTime.UtcNow;
            TransmitToMotors();
        }
        public void OnMaxSpeedUpdate(float value)
        {
            if (value < MAX_SPEED_LOW_LIMIT)
            {
                this.maxSpeed.Value = MAX_SPEED_LOW_LIMIT;
            }
            TransmitToMotors();
        }
        public abstract int CalculateTxSignal();

    }

    internal class SimpleProximityZone: SingleChannelHapticZone
    {
        protected float minSpeed;
        protected float maxSpeedScale;

        public SimpleProximityZone(Dictionary<string, string> cfg) : base(cfg)
        {
            this.minSpeed = float.Parse(cfg["min_speed"]) / 100f;
            this.maxSpeedScale = float.Parse(cfg["max_speed_scale"]) / 100f;
        }

        public override int CalculateTxSignal()
        {
            float signal = Math.Clamp(((this.maxSpeed.Value - this.minSpeed) * this.proximity.Value + this.minSpeed) * MOTOR_SPEED_SCALE * this.maxSpeedScale, 0f, 1f);
            return (int)Math.Round(signal * 255.0f);
        }
    }

    internal class VelocityZone : SingleChannelHapticZone
    {
        protected OSCBoolParameter cutoff;
        protected OSCFloatParameter lateralX;
        protected float prevLateralX;
        protected float prevProximity;
        protected float limitProximityOuter;
        protected float limitProximityInner;
        protected float velocityScale;
        protected DateTime lastCalculated = DateTime.MinValue;
        protected float minSpeed;
        protected float maxSpeedScale;

        public VelocityZone(Dictionary<string, string> cfg) : base(cfg)
        {
            this.cutoff = new OSCBoolParameter(cfg, "parameter_cutoff", x => this.TransmitToMotors(), optional: true);
            this.lateralX = new OSCFloatParameter(cfg, "parameter_lateral_x", x => this.TransmitToMotors(), optional: true);
            this.cutoff.Value = false;
            this.lateralX.Value = 0f;

            this.limitProximityOuter = float.Parse(cfg.GetValueOrDefault("limit_proximity_outer", "0"));
            this.limitProximityInner = float.Parse(cfg.GetValueOrDefault("limit_proximity_inner", "1"));
            this.velocityScale = float.Parse(cfg.GetValueOrDefault("velocity_scale", "20"));
            this.minSpeed = float.Parse(cfg["min_speed"]) / 100f;
            this.maxSpeedScale = float.Parse(cfg["max_speed_scale"]) / 100f;
        }

        public override int CalculateTxSignal()
        {
            if (this.cutoff.Value || this.limitProximityOuter > this.proximity.Value || this.proximity.Value > this.limitProximityInner)
            {
                this.lastCalculated = DateTime.UtcNow;
                return 0;
            }

            float proxV = Math.Max(this.proximity.Velocity, 0);
            float v = (float)Math.Sqrt(Math.Pow(this.lateralX.Velocity, 2) + Math.Pow(proxV, 2)) * this.velocityScale;
            int tx = (int)Math.Round(((this.maxSpeed.Value - this.minSpeed) * v + this.minSpeed) * MOTOR_SPEED_SCALE * this.maxSpeedScale * 255f);

            return tx;
        }
    }
}
