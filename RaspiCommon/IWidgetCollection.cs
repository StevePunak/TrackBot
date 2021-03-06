﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RaspiCommon.Devices.Chassis;
using RaspiCommon.Devices.Compass;
using RaspiCommon.Devices.Locomotion;
using RaspiCommon.Devices.Optics;
using RaspiCommon.Devices.Spatial;
using RaspiCommon.Network;
using RaspiCommon.Spatial;
using RaspiCommon.Spatial.LidarImaging;

namespace RaspiCommon
{
	public interface IWidgetCollection
	{
		event NewBearingHandler BearingChanged;
		event RangeHandler ForwardPrimaryRange;
		event RangeHandler BackwardPrimaryRange;
		event RangeHandler ForwardSecondaryRange;
		event RangeHandler BackwardSecondaryRange;
		event NewDestinationBearingHandler NewDestinationBearing;
		event DistanceToTravelHandler DistanceToTravel;
		event DistanceLeftHandler DistanceLeft;
		event FuzzyPathChangedHandler FuzzyPathChanged;
		event LandmarksChangedHandler LandmarksChanged;
		event BarriersChangedHandler BarriersChanged;
		event DeadReckoningEnvironmentReceivedHandler DeadReckoningEnvironmentReceived;
		event CameraImagesAnalyzedHandler CameraImagesAnalyzed;

		ICompass Compass { get; }
		IImageEnvironment ImageEnvironment { get; }
		ILandscape Landscape { get; }
		Chassis Chassis { get; }
		Camera Camera { get; }
		TrackSpeed TrackSpeed { get; }
		PanTilt PanTilt { get; }

		bool HasImageEnvironment { get; }
	}
}
