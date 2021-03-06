using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RaspiCommon;

namespace TrackBot.TTY
{
	[CommandText("cs")]
	class CalibrateSpeed : CommandBase
	{
		public CalibrateSpeed()
			: base(true) { }

		public override bool Execute(List<String> commandParts)
		{
			Program.Config.MetersPerSecondAtPower.Clear();

			TimeSpan runtime = TimeSpan.FromMilliseconds(2000);

			for(int motorSpeed = 70;motorSpeed <= 100;motorSpeed += 5)
			{
				Console.WriteLine("Calibrating speed at {0}", motorSpeed);

				Double startRange = Widgets.Instance.ImageEnvironment.Range;

				// move out
				Widgets.Instance.Tracks.Speed = motorSpeed;
				Thread.Sleep(runtime);
				Widgets.Instance.Tracks.Stop();

				Thread.Sleep(1000);
				Double endRange = Widgets.Instance.ImageEnvironment.Range;
				Double speed = (startRange - endRange) / runtime.Seconds;
				Console.WriteLine("travelled {0:00} meters in {1}ms for speed of {2:0.00}m/s", (startRange - endRange), runtime, speed);

				Program.Config.MetersPerSecondAtPower.Add(motorSpeed, speed);

				// move back
				Widgets.Instance.Tracks.Speed = -motorSpeed;
				Thread.Sleep(runtime);
				Widgets.Instance.Tracks.Stop();

				Thread.Sleep(1000);
			}
			return true;
		}

		public override void Usage(out String commandSyntax, out String description)
		{
			commandSyntax = "cs";
			description = "Calibrate speed";
		}
	}
}
