using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KanoopCommon.Addresses;
using KanoopCommon.CommonObjects;
using KanoopCommon.Logging;
using KanoopCommon.TCP.Clients;
using KanoopCommon.Threading;
using MQTT;
using MQTT.Examples;
using MQTT.Packets;
using RaspiCommon.Lidar;
using RaspiCommon.Lidar.Environs;

namespace RaspiCommon.Server
{
	public class TelemetryClient : SubscribeThread
	{
		public const Double VectorSize = .25;

		public LidarVector[] Vectors;

		public FuzzyPath FuzzyPath { get; private set; }
		public BarrierList Barriers { get; private set; }
		public LandmarkList Landmarks { get; private set; }
		public Double Bearing { get; private set; }

		public TelemetryClient(String host, String clientID, List<String> topics)
			: base(host, clientID, topics)
		{
			Vectors = new LidarVector[(int)(360 / VectorSize)];
			for(int offset = 0;offset < Vectors.Length;offset++)
			{
				Vectors[offset] = new LidarVector()
				{
					Bearing = (Double)offset * VectorSize,
					Range = 0,
					RefreshTime = DateTime.UtcNow
				};
			}
			InboundSubscribedMessage += OnLidarClientInboundSubscribedMessage;
		}

		private void OnLidarClientInboundSubscribedMessage(MqttClient client, PublishMessage packet)
		{
			if(packet.Topic == TelemetryServer.RangeBlobTopic)
			{
				using(BinaryReader br = new BinaryReader(new MemoryStream(packet.Message)))
				{
					for(int offset = 0;offset < Vectors.Length;offset++)
					{
						Double range = br.ReadDouble();
						Vectors[offset].Range = range;
						Vectors[offset].RefreshTime = DateTime.UtcNow;
					}

				}
			}
			else
			{
				if(packet.Topic == TelemetryServer.CurrentPathTopic)
				{
					FuzzyPath path = new FuzzyPath(packet.Message);
					FuzzyPath = path;
				}
				else if(packet.Topic == TelemetryServer.BarriersTopic)
				{
					BarrierList barriers = new BarrierList(packet.Message);
					Barriers = barriers;
				}
				else if(packet.Topic == TelemetryServer.LandmarksTopic)
				{
					LandmarkList landmarks = new LandmarkList(packet.Message);
					Landmarks = landmarks;
				}
				else if(packet.Topic == TelemetryServer.BearingTopic)
				{
					Bearing = BitConverter.ToDouble(packet.Message, 0);
				}
			}
		}

	}
}