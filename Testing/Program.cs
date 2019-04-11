using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using KanoopCommon.Extensions;
using KanoopCommon.Geometry;
using KanoopCommon.Logging;
using KanoopCommon.Queueing;
using KanoopCommon.Threading;
using RaspiCommon;
using RaspiCommon.Devices.Chassis;
using RaspiCommon.Devices.MotorControl;
using RaspiCommon.Devices.Spatial;
using RaspiCommon.Extensions;
using RaspiCommon.Lidar;
using RaspiCommon.Lidar.Environs;
using RaspiCommon.Server;
using RaspiCommon.Spatial;

namespace Testing
{

	class Program
	{
		public static Log Log { get; private set; }

		static void Main(string[] args)
		{
			OpenLog();

			PointCloudTest();
//			EMGUTest();

			RunLidar();
		}

		private static void PointCloudTest()
		{
			Chassis chassis = new XiaorTankTracks();
			chassis.Points.Add(ChassisParts.Lidar, new PointD(chassis.Points[ChassisParts.RearLeft].X + 150, chassis.Points[ChassisParts.CenterPoint].Y + 140));
			chassis.Points.Add(ChassisParts.FrontRangeFinder, new PointD(chassis.Width / 2, 0));
			chassis.Points.Add(ChassisParts.RearRangeFinder, new PointD(chassis.Width / 2, chassis.Length));

			BearingAndRange toFrontLeft = chassis.GetBearingAndRange(ChassisParts.Lidar, ChassisParts.FrontLeft, 0);
			BearingAndRange toFrontRight = chassis.GetBearingAndRange(ChassisParts.Lidar, ChassisParts.FrontRight, 0);
			Mat input = new Mat(@"f:\tmp\1.png");

			PointCloud2D cloud = input.ToPointCloud(.25);

			Mat output = cloud.ToBitmap(new Size(500, 500), Color.Blue);
			output.Save(@"f:\tmp\output.png");

			PointCloud2D cloud2 = cloud.Move(new BearingAndRange(45, 10));
			cloud2.PlaceOnBitmap(output, output.Center(), Color.Green);
			output.Save(@"f:\tmp\output1.png");

		}

		public Double FuzzyRangeAtBearing(Mat bitmap, PointCloud2D vectorsFromLidar, BearingAndRange frontLeftWheelOffset, BearingAndRange frontRightWheelOffset, Double bearingStraightAhead, Double angularWidth, out PointCloud2D fromFrontLeft, out PointCloud2D fromFrontRight)
		{
			PointD center = bitmap.Center();
			bitmap.DrawCross(center, 5, Color.White);

			if(angularWidth == 0)
			{
				angularWidth = 25;
			}

			Log.SysLogText(LogLevel.DEBUG, "Getting vectors from lidar");

			fromFrontLeft = vectorsFromLidar.Move(frontLeftWheelOffset);
			fromFrontRight = vectorsFromLidar.Move(frontRightWheelOffset);

			Log.SysLogText(LogLevel.DEBUG, "FL: {0} FR: {1}", fromFrontLeft, fromFrontRight);

			Double start = bearingStraightAhead.SubtractDegrees(angularWidth / 2);
			Double end = bearingStraightAhead.AddDegrees((angularWidth / 2) + 1);

			List<Double> allDistances = new List<double>();
			Double angle = start;
			while(angle.IsWithinDegressOf(end, Lidar.VectorSize) == false)
			{
				Double d1 = fromFrontLeft.GetRangeAtBearing(angle);
				Double d2 = fromFrontRight.GetRangeAtBearing(angle);
				Log.SysLogText(LogLevel.DEBUG, "At {0:0.000}� range is {1:0.000}  and  {2:0.000}", angle, d1, d2);
				if(d1 != 0)
					allDistances.Add(d1);
				if(d2 != 0)
					allDistances.Add(d1);

				angle = angle.AddDegrees(Lidar.VectorSize);
			}

			return allDistances.Count > 0 ? allDistances.Average() : 0;
		}

		private static void EMGUTest()
		{
			String root = Environment.OSVersion.Platform == PlatformID.Unix ? "./" : @"c:\pub\tmp";

			while(true)
			{
				foreach(String file in Directory.GetFiles(root, "grid*png"))
				{
					//if(file.Contains("000") == false)
					//	continue;

					Log.SysLogText(LogLevel.DEBUG, "\n\n\n\n\n");
					int index = file.IndexOf("grid");
					if(index > 0 && Char.IsDigit(file[index + 4]))
					{
						String part = file.Substring(index + 4, 3);
						index = int.Parse(part);
					}

					String inFile = root + file;

					Double PixelsPerMeter = 50;

					Mat image = new Mat(file);
					PointD currentPoint = new PointD(image.Width / 2, image.Height / 2);

					LidarEnvironment env = new LidarEnvironment(10, PixelsPerMeter);
					env.Location = currentPoint;
					env.ProcessImage(image, 0, PixelsPerMeter);

					Mat output = env.CreateImage(SpatialObjects.Everything);
					String outFile = String.Format(@"{0}\output_{1:000}-a.png", root, index);
					output.Save(outFile);
					output = env.CreateImage(SpatialObjects.Everything);
					outFile = String.Format(@"{0}\output_{1:000}-b.png", root, index);
					output.Save(outFile);

				}
			}
		
			Log.LogText(LogLevel.DEBUG, "Done");
		}

		private static void TestImage()
		{
		}

		static void RunMotor()
		{
			PWMMotorDriver motor = new PWMMotorDriver(GpioPin.Pin12,  GpioPin.Pin26, GpioPin.Pin16, GpioPin.Pin20);
			motor.Speed = MotorSpeed.Fast;
			motor.Start();

			motor.Rotate(Direction.Forward, 400);

			GpioSharp.Sleep(10000);

			motor.Stop();

			Console.WriteLine("done");
		}

		static void RunLidar()
		{

//			RPLidar lidar = new RPLidar("/dev/ttyUSB0");
			RPLidar lidar = new RPLidar("COM5", .25);
			lidar.Start();

#if zero
			{
			{
				LidarCommand command = new ResetCommand();
				lidar.SendCommand(command);

				LidarResponse response;
				if(lidar.TryGetResponse(TimeSpan.FromSeconds(1), out response))
				{
					Log.SysLogText(LogLevel.DEBUG, "Reset Response successful");
				}
				else
				{
					Log.SysLogText(LogLevel.DEBUG, "Reset Response Unsuccessful");
				}
			}
			Thread.Sleep(1000);

			{
				LidarCommand command = new GetSampleRateCommand();
				lidar.SendCommand(command);

				LidarResponse response;
				if(lidar.TryGetResponse(TimeSpan.FromSeconds(1), out response))
				{
					Log.SysLogText(LogLevel.DEBUG, "Response successful");
				}
				else
				{
					Log.SysLogText(LogLevel.DEBUG, "Response Unsuccessful");
				}
			}
			Thread.Sleep(1000);

			{
				LidarCommand command = new GetDeviceInfoCommand();
				lidar.SendCommand(command);

				LidarResponse response;
				if(lidar.TryGetResponse(TimeSpan.FromSeconds(1), out response))
				{
					Log.SysLogText(LogLevel.DEBUG, "Next Response successful");
				}
			}
			Thread.Sleep(1000);
				lidar.SendCommand(new StartExpressScanCommand());

				Log.SysLogText(LogLevel.DEBUG, "I'm anout to die!!");
				Thread.Sleep(100);
//				Environment.FailFast("I'm dying");
//				return;
				LidarResponse response;

				if(lidar.TryGetResponse(TimeSpan.FromSeconds(10), out response))
				{
					Log.SysLogText(LogLevel.DEBUG, "Response successful");
				}
			}
#else
			{
				lidar.SendCommand(new StartScanCommand());

				Thread.Sleep(100);
				LidarResponse response;

				if(lidar.TryGetResponse(TimeSpan.FromSeconds(10), out response))
				{
					Log.SysLogText(LogLevel.DEBUG, "Response successful");
				}
			}
#endif

			Thread.Sleep(1000);
			Console.WriteLine("Sending stop");
			{
				LidarCommand command = new StopCommand();
				lidar.SendCommand(command);

				LidarResponse response;
				if(lidar.TryGetResponse(TimeSpan.FromSeconds(1), out response))
				{
					Log.SysLogText(LogLevel.DEBUG, "Stop Response successful");
				}
			}

			Thread.Sleep(1000);
			Console.WriteLine("Sending stop motor");
			{
				LidarCommand command = new SetMotorPwm(0);
				lidar.SendCommand(command);

				LidarResponse response;
				if(lidar.TryGetResponse(TimeSpan.FromSeconds(1), out response))
				{
					Log.SysLogText(LogLevel.DEBUG, "Stop Response successful");
				}
			}

			Thread.Sleep(5000);

		}

		private static void OpenLog()
		{
			Log = new Log();
			Log.Open(LogLevel.ALWAYS, "lidar.log", OpenFlags.CONTENT_TIMESTAMP | OpenFlags.OUTPUT_TO_FILE | OpenFlags.OUTPUT_TO_DEBUG | OpenFlags.OUTPUT_TO_CONSOLE);
			Log.SystemLog = Log;
		}
	}
}
