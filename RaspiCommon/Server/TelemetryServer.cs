using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using KanoopCommon.Addresses;
using KanoopCommon.CommonObjects;
using KanoopCommon.Extensions;
using KanoopCommon.Geometry;
using KanoopCommon.Logging;
using KanoopCommon.Serialization;
using KanoopCommon.TCP;
using KanoopCommon.Threading;
using MQTT;
using RaspiCommon.Lidar;
using RaspiCommon.Lidar.Environs;
using RaspiCommon.Spatial;
using RaspiCommon.Spatial.Imaging;

namespace RaspiCommon.Server
{
	public class TelemetryServer : ThreadBase
	{
		public const int RangeBlobPacketSize = sizeof(Double);
		static readonly TimeSpan SlowRunInterval = TimeSpan.FromSeconds(1);
		static readonly TimeSpan StandardRunInterval = TimeSpan.FromMilliseconds(250);

		public IImageEnvironment Environment { get; set; }
		public ILandscape Landscape { get; set; }
		
		public MqttClient Client { get; private set; }

		public String MqqtClientID { get; private set; }
		public ICompass Compass { get; private set; }

		public IPAddress Address { get; private set; }

		private FuzzyPath _fuzzyPath;
		private bool _fuzzyPathChanged;

		private ImageVectorList _landmarks;
		private bool _landmarksChanged;
		private BarrierList _barriers;
		private bool _barriersChanged;

		public TelemetryServer(IImageEnvironment environment, ILandscape landscape, ICompass compass, String mqqtBrokerAddress, String mqqtClientID)
			: base(typeof(TelemetryServer).Name)
		{
			IPAddress address;

			if( IPAddress.TryParse(mqqtBrokerAddress, out address) == false &&
				IPAddressExtensions.TryResolve(mqqtBrokerAddress, out address) == false)
			{
				throw new CommonException("Could not resolve '{0}' to a host", mqqtBrokerAddress);
			}
			Address = address;
			MqqtClientID = mqqtClientID;
			Environment = environment;
			Landscape = landscape;
			Compass = compass;

			Environment.FuzzyPathChanged += OnEnvironment_FuzzyPathChanged;
			Environment.BarriersChanged += OnEnvironment_BarriersChanged;
			Environment.LandmarksChanged += OnEnvironment_LandmarksChanged;
			_fuzzyPathChanged = false;
			_barriersChanged = false;
			_landmarksChanged = false;

			Interval = TimeSpan.FromMilliseconds(250);
		}

		protected override bool OnStart()
		{
			return base.OnStart();
		}

		protected override bool OnRun()
		{
			try
			{
				if(Client == null || Client.Connected == false)
				{
					GetConnected();
					SendInitialData();
				}

				if(Client.Connected)
				{
					SendRangeData();

					SendBearing();
					if(_fuzzyPathChanged)
					{
						SendFuzzyPath();
					}
					if(_barriersChanged)
					{
						SendBarriers();
					}
					if(_landmarksChanged)
					{
						SendLandmarks();
					}
				}

			}
			catch(Exception e)
			{
				Log.SysLogText(LogLevel.ERROR, "{0} EXCEPTION: {1}", Name, e.Message);
				Interval = TimeSpan.FromSeconds(1);
			}
			return true;
		}

		private void SendBearing()
		{
			byte[] output = BitConverter.GetBytes(Compass.Bearing);
			Client.Publish(MqttTypes.BearingTopic, output, true);
		}

		private void SendFuzzyPath()
		{
			Log.LogText(LogLevel.DEBUG, "Sending fuzzy path");

			byte[] output = _fuzzyPath.Serizalize();
			Client.Publish(MqttTypes.CurrentPathTopic, output, true);
			_fuzzyPathChanged = false;
		}

		private void SendLandmarks()
		{
			byte[] output = _landmarks.Serialize();
			Log.LogText(LogLevel.DEBUG, "Sending {0} bytes of {1} landmarks", output.Length, _landmarks.Count);
			Client.Publish(MqttTypes.LandmarksTopic, output, true);
			_landmarksChanged = false;
		}

		private void SendBarriers()
		{
			byte[] output = _barriers.Serialize();
			Log.LogText(LogLevel.DEBUG, "Sending {0} bytes of {1} barriers", output.Length, _barriers.Count);
			Client.Publish(MqttTypes.BarriersTopic, output, true);
			_barriersChanged = false;
		}

		private void SendInitialData()
		{
			Log.LogText(LogLevel.DEBUG, "Sending initial data");

			ImageMetrics imageMetrics = new ImageMetrics()
			{
				MetersSquare = Environment.MetersSquare,
				PixelsPerMeter = Environment.PixelsPerMeter
			};
			String output = KVPSerializer.Serialize(imageMetrics);
			Client.Publish(MqttTypes.ImageMetricsTopic, output, true);

			LandscapeMetrics landscapeMetrics = new LandscapeMetrics()
			{
				MetersSquare = Landscape.MetersSquare,
				Name =  Landscape.Name
			};
			output = KVPSerializer.Serialize(imageMetrics);
			Client.Publish(MqttTypes.LandscapeMetricsTopic, output, true);
		}

		void SendRangeData()
		{
			byte[] output = new byte[RangeBlobPacketSize * Environment.Vectors.Length];

			using(BinaryWriter bw = new BinaryWriter(new MemoryStream(output)))
			{
				for(int offset = 0;offset < Environment.Vectors.Length;offset++)
				{
					IVector vector = Environment.Vectors[offset];
					bw.Write(vector.Range);
				}
			}

			Client.Publish(MqttTypes.RangeBlobTopic, output);
		}

		private void OnEnvironment_FuzzyPathChanged(FuzzyPath path)
		{
			Log.LogText(LogLevel.DEBUG, "FuzzyPath path changed");
			_fuzzyPath = path.Clone();
			_fuzzyPathChanged = true;
		}

		private void OnEnvironment_LandmarksChanged(ImageVectorList landmarks)
		{
			Log.LogText(LogLevel.DEBUG, "Landmarks changed");

			_landmarks = landmarks.Clone();
			_landmarksChanged = true;
		}

		private void OnEnvironment_BarriersChanged(BarrierList barriers)
		{
			Log.LogText(LogLevel.DEBUG, "{0} Barriers changed", barriers.Count);

			_barriers = barriers.Clone();
			_barriersChanged = true;
		}

		void GetConnected()
		{
			Client = new MqttClient(Address, MqqtClientID)
			{
				QOS = QOSTypes.Qos0
			};
			Client.Connect();
		}
	}
}
