using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using KanoopCommon.Database;
using KanoopCommon.Geometry;
using KanoopCommon.Logging;
using KanoopCommon.PersistentConfiguration;
using RaspiCommon;
using RaspiCommon.Data.DataSource;
using RaspiCommon.Data.Entities.Track;
using RaspiCommon.Devices.MotorControl;
using RaspiCommon.Devices.Optics;
using RaspiCommon.Devices.RobotArms;
using RaspiCommon.Devices.Spatial;
using TrackBot.Spatial;
using TrackBot.Tracks;
using TrackBot.TTY;

namespace TrackBot
{
	class Program
	{
		[DllImport("libgpiosharp.so")]
		public static extern void PulsePin(GpioPin pin, UInt32 microseconds);

		public static Log Log { get; private set; }
		public static RaspiConfig Config { get; private set; }

		static void Main(string[] args)
		{
			Console.WriteLine("Initializing...");

			OpenLog(args.Contains("-c"));

			Console.WriteLine("Opening config...");
			OpenConfig();

			Console.WriteLine("Running pretests...");
			Test();

			Console.WriteLine("Starting Widgets...");
			Widgets.Instance.StartWidgets();

			Console.WriteLine("Starting TTY...");
			Terminal.Run();

			Widgets.Instance.StopWidgets();

		}

		private static void Test()
		{
			try
			{
				Defaults();
				Console.WriteLine("{0}", Program.Config.CameraParameters);
			}
			catch(Exception e)
			{
				Console.WriteLine("TEST CODE EXCEPTION: {0}", e.Message);
			}
		}

		static void Defaults()
		{
#if zero
			Program.Config.RemoteImageDirectory = "/home/pi/images";
			Program.Config.RadarHost = "192.168.0.50";
			Program.Config.BlueThresholds = new ColorThreshold(Color.Blue, 150, 70);
			Program.Config.GreenThresholds = new ColorThreshold(Color.Green, 150, 100);
			Program.Config.RedThresholds = new ColorThreshold(Color.Red, 150, 70);
			Program.Config.CameraParameters = new RaspiCameraParameters();

			Program.Config.CameraBearingOffset = 4;

#endif
			Program.Config.LidarComPort = "/dev/ttyS0";

			Program.Config.ClawRotationPin = GpioPin.Pin18;
			Program.Config.ClawLeftPin = GpioPin.Pin22;
			Program.Config.ClawRightPin = GpioPin.Pin27;
			Program.Config.ClawPin = GpioPin.Pin17;

			Program.Config.ClawRotationPinMin = 1000;
			Program.Config.ClawRotationPinMax = 2000;
			Program.Config.ClawLeftPinMin = 600;
			Program.Config.ClawLeftPinMax = 1400;
			Program.Config.ClawRightPinMin = 900;
			Program.Config.ClawRightPinMax = 2400;
			Program.Config.ClawPinMin = 1000;
			Program.Config.ClawPinMax = 2000;

			Program.Config.PanPin = GpioPin.Pin06;
			Program.Config.TiltPin = GpioPin.Pin19;

			Program.Config.LidarSpinEnablePin = GpioPin.Pin24;

			Program.Config.SnapshotUrl = "http://127.0.0.1:8085/?action=snapshot";

			Program.Config.Save();
#if zero
			Program.Config.CameraParameters.SnapshotDelay = TimeSpan.FromMilliseconds(1500);
#endif
		}

		private static void OpenConfig()
		{
			String      configFileName = RaspiConfig.GetDefaultConfigFileName();

			if(Directory.Exists(Path.GetDirectoryName(configFileName)))
				Directory.SetCurrentDirectory(Path.GetDirectoryName(configFileName));
			else
				Directory.CreateDirectory(Path.GetDirectoryName(configFileName));

			Log.LogText(LogLevel.DEBUG, "Opening config...");

			ConfigFile  configFile = new ConfigFile(configFileName);
			Config = (RaspiConfig)configFile.GetConfiguration(typeof(RaspiConfig));

			Config.Save();
		}

		private static void OpenLog(bool console)
		{
			Console.WriteLine("Opening log");
			Log = new Log();
			UInt32 flags = OpenFlags.CONTENT_TIMESTAMP | OpenFlags.OUTPUT_TO_FILE;
			if(console)
			{
				flags |= OpenFlags.OUTPUT_TO_CONSOLE;
			}
			Log.Open(LogLevel.ALWAYS, "/var/log/robo/robo.log", flags);
			Log.SystemLog = Log;
			Console.WriteLine("Log opened");
		}
	}
}
