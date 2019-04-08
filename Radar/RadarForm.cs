using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using KanoopCommon.Addresses;
using KanoopCommon.CommonObjects;
using KanoopCommon.Database;
using KanoopCommon.Extensions;
using KanoopCommon.Geometry;
using KanoopCommon.Logging;
using KanoopCommon.TCP.Clients;
using KanoopCommon.Threading;
using KanoopControls.SimpleTextSelection;
using Radar.Properties;
using RaspiCommon.Data.DataSource;
using RaspiCommon.Data.Entities;
using RaspiCommon.Lidar;
using RaspiCommon.Lidar.Environs;
using RaspiCommon.Server;
using RaspiCommon.Spatial;
using RaspiCommon.Spatial.Imaging;
using TrackBotCommon.Environs;

namespace Radar
{
	public partial class RadarForm : Form
	{
		const String COL_LABEL = "Label";
		const String COL_POSITION = "Position";
		const String COL_RANGE = "Range";
		const String COL_BEARING = "Bearing";

		const Double METERS_SQUARE = 10;
		Double PixelsPerMeterImage
		{
			get
			{
				Double value = _client != null && _client.ImageMetrics != null
					? _client.ImageMetrics.PixelsPerMeter : 50;
				return value;
			}
		}

		Double PixelsPerMeterLandscape
		{
			get { return 50; }
		}

		TelemetryClient _client;
		String Host { get; set; }

		Timer _drawTimer;

		Bitmap _radarBitmap;
		Mat _workBitmap;
		Mat _sharpened;

		Double _lastAngle;

		Image _tank;

		bool _layoutComplete;

		TrackDataSource _ds;
		public TrackBotLandscape Landscape { get; private set; }

		public LandscapeMetrics LandscapeMetrics { get; set; }
		public ImageMetrics ImageMetrics { get; set; }
		public EnvironmentInfo EnvironmentInfo { get; set; }

		public int AnalysisPixelsPerMeter { get { return 100; } }

		public RadarForm()
		{
			_client = null;
			Host = String.IsNullOrEmpty(Program.Config.RadarHost) ? "raspi" : Program.Config.RadarHost;
			InitializeComponent();
		}

		private void OnFormLoad(object sender, EventArgs args)
		{
			try
			{
				SetupBitmap();

				GetConnected();
				Log.SysLogText(LogLevel.DEBUG, "Got connectd");

				_drawTimer = new Timer();
				_drawTimer.Interval = 500;
				_drawTimer.Enabled = true;
				_drawTimer.Tick += OnDrawTimer;
				_drawTimer.Start();

				_tank = Resources.tank.Resize(6, 10);
				Log.SysLogText(LogLevel.DEBUG, "Started draw timer");

				if(Program.Config.LastRadarWindowSize.Height > 200)
				{
					Size = Program.Config.LastRadarWindowSize;
					splitTopToBottom.SplitterDistance = Program.Config.SplitTopToBottomPosition;
					splitRadar.SplitterDistance = Program.Config.SplitRadarPosition;
					Location = Program.Config.LastRadarWindowLocation;
				}
				LoadLandscape();

				flowBitmap.Height = 40;

				_layoutComplete = true;
			}
			catch(Exception e)
			{
				MessageBox.Show(e.Message);
				Close();
			}
		}

		void RepopulateLandmarks()
		{
			listLandmarks.Clear();
			listLandmarks.AddColumnHeader(COL_POSITION, 80);
			listLandmarks.AddColumnHeader(COL_LABEL, 80);
			listLandmarks.AddColumnHeader(COL_RANGE, 80);
			listLandmarks.AddColumnHeader(COL_BEARING, -2);

			foreach(Landmark landmark in Landscape.Landmarks)
			{
				ListViewItem item = listLandmarks.AddRow(landmark.Location, landmark.Location, landmark);
				listLandmarks.SetListViewColumn(item, COL_LABEL, landmark.Label);
				//listLandmarks.SetListViewColumn(item, COL_RANGE, landmark.Label);
				//listLandmarks.SetListViewColumn(item, COL_LABEL, landmark.Label);
			}
		}

		void LoadLandscape()
		{
			try
			{
				_ds = DataSourceFactory.Create<TrackDataSource>(Program.Config.DBCredentials);
				TrackBotLandscape landscape;
				if(_ds.LandscapeGet<TrackBotLandscape>(Program.Config.LandscapeName, out landscape).ResultCode == DBResult.Result.Success)
				{
					Landscape = landscape;
					Landscape.DataSource = _ds;
				}
			}
			catch(Exception e)
			{
				MessageBox.Show(e.Message);
			}
		}

		void SetupBitmap()
		{
			int size = (int)(METERS_SQUARE * PixelsPerMeterImage);
			_radarBitmap = new Bitmap(size, size);
			using(Graphics g = Graphics.FromImage(_radarBitmap))
			{
				g.FillRectangle(new SolidBrush(Color.Black), new Rectangle(0, 0, size, size));
				Point center = _radarBitmap.Center().ToPoint();
				for(int radius = 1;radius < METERS_SQUARE / 2;radius++)
				{
					g.DrawEllipse(new Pen(Color.Green), _radarBitmap.RectangleForCircle(center, radius * (int)PixelsPerMeterImage));
				}

				g.DrawLine(new Pen(Color.Green), new Point(center.X - 5, center.Y), new Point(center.X + 5, center.Y));
				g.DrawLine(new Pen(Color.Green), new Point(center.X, center.Y - 5), new Point(center.X, center.Y + 5));
				picLidar.BackgroundImage = _radarBitmap;
			}
		}

		private void OnDrawTimer(object sender, EventArgs e)
		{
			if(_client == null || _client.Connected == false)
				return;

			Image bitmap = new Bitmap(_radarBitmap);
			using(Graphics g = Graphics.FromImage(bitmap))
			{
				Pen redPen = new Pen(Color.Red);
				Pen barrierPen = new Pen(Color.Firebrick);
				Pen landmarkPen = new Pen(Color.Blue);
				PointD center = bitmap.Center();

				// draw all the dots
				for(Double angle = _lastAngle.AddDegrees(TelemetryClient.VectorSize);angle != _lastAngle;angle = angle.AddDegrees(TelemetryClient.VectorSize))
				{
					int offset = (int)(angle / TelemetryClient.VectorSize);
					LidarVector vector = _client.Vectors[offset];
					if(vector != null && vector.Range != 0)
					{
						PointD where = center.GetPointAt(vector.Bearing, vector.Range * PixelsPerMeterImage) as PointD;
						Rectangle rect = new Rectangle(where.ToPoint(), new Size(1, 1));
						g.DrawRectangle(redPen, rect);
						//Log.SysLogText(LogLevel.DEBUG, "Drawing {0} at {1:0.00}°  {2}", vector, angle, where);
					}
				}

				// draw fuzzy path
				if(_client.FuzzyPath != null)
				{
					FuzzyPath path = _client.FuzzyPath.Clone();
					Pen brownPen = new Pen(Color.RosyBrown);
					if(path.Elements.Count > 1)
					{
						PointD p1 = center.GetPointAt(path.Elements.First().Bearing, path.Elements.First().Range * PixelsPerMeterImage);
						g.DrawLine(brownPen, center.ToPoint(), p1.ToPoint());
						PointD p2 = center.GetPointAt(path.Elements.Last().Bearing, path.Elements.Last().Range * PixelsPerMeterImage);
						g.DrawLine(brownPen, center.ToPoint(), p2.ToPoint());
					}
				}

				if(_client.ImageBarriers != null)
				{
					BarrierList barriers = _client.ImageBarriers.Clone();
					Pen orangePen = new Pen(Color.Orange);
					foreach(ImageBarrier barrier in barriers)
					{
						Line line = barrier.GetLine();
						g.DrawLine(orangePen, line.P1.ToPoint(), line.P2.ToPoint());
					}
				}

				if(_client.ImageLandmarks != null)
				{
					ImageVectorList landmarks = _client.ImageLandmarks.Clone();
					Pen bluePen = new Pen(Color.Blue, 2);
					foreach(ImageVector landmark in landmarks)
					{
						RectangleD rectangle = RectangleD.InflatedFromPoint(landmark.GetPoint(), 6);
						g.DrawEllipse(bluePen, rectangle.ToRectangle());
					}
				}

				Image tank = new Bitmap(Resources.tank).Rotate(_client.Bearing);
//				tank.Save(@"c:\tmp\output.png");

				PointD tankPoint = PointD.UpperLeftFromCenter(center, tank.Size);
				g.DrawImage(tank, tankPoint.ToPoint());
			}

			//_bitmap.Save(@"c:\tmp\output.png");
			picLidar.BackgroundImage = bitmap;

			RefreshMetrics();
		}

		void RefreshMetrics()
		{
			textBearing.Text = String.Format("{0:0.0}°", EnvironmentInfo.Bearing);
			textDestinationBearing.Text = String.Format("{0}°", EnvironmentInfo.DestinationBearing);
			textForwardRange.Text = String.Format("{0:0.00}m", EnvironmentInfo.ForwardPrimaryRange);
			textBackwardRange.Text = String.Format("{0:0.00}m", EnvironmentInfo.BackwardPrimaryRange);
			textForwardSecondary.Text = String.Format("{0:0.00}m", EnvironmentInfo.ForwardSecondaryRange);
			textBackwardSecondary.Text = String.Format("{0:0.00}m", EnvironmentInfo.BackwardSecondaryRange);
		}

		void GetConnected()
		{
#if zero
			ChooseBotForm dlg = new ChooseBotForm()
			{
				Host = Program.Config.RadarHost
			};
			if(dlg.ShowDialog() != DialogResult.OK)
			{
				throw new CommonException("No host selected");
			}
			Program.Config.RadarHost = dlg.Host;
			Program.Config.Save();
#endif
			_client = new TelemetryClient(Program.Config.RadarHost, "radarform",
				new List<string>()
				{
					MqttTypes.CurrentPathTopic,
					MqttTypes.RangeBlobTopic,
					MqttTypes.BarriersTopic,
					MqttTypes.LandmarksTopic,
					MqttTypes.BearingTopic,
					MqttTypes.ImageMetricsTopic,
					MqttTypes.DistanceAndBearingTopic
				});
			_client.LandscapeMetricsReceived += OnLandscapeMetricsReceived;
			_client.ImageMetricsReceived += OnImageMetricsReceived;
			_client.EnvironmentInfoReceived += OnEnvironmentInfoReceived;
			_client.Start();

			_lastAngle = 0;
		}

		private void OnEnvironmentInfoReceived(EnvironmentInfo metrics)
		{
			EnvironmentInfo = metrics.Clone() as EnvironmentInfo;
		}

		private void OnImageMetricsReceived(ImageMetrics metrics)
		{
			ImageMetrics = metrics;
		}

		private void OnLandscapeMetricsReceived(LandscapeMetrics metrics)
		{
			LandscapeMetrics = metrics;
		}

		private void OnFormClosing(object sender, FormClosingEventArgs e)
		{
			if(_client != null)
			{
				_client.Disconnect();
				_client = null;
			}

			ThreadBase.AbortAllRunningThreads();
		}

		private void OnFormResizeEnd(object sender, EventArgs e)
		{
			if(_layoutComplete)
			{
				Program.Config.LastRadarWindowSize = Size;
				Program.Config.Save();
			}

		}

		private void OnSplitterMoved(object sender, SplitterEventArgs e)
		{
			if(_layoutComplete)
			{
				Program.Config.SplitTopToBottomPosition = splitTopToBottom.SplitterDistance;
				Program.Config.SplitRadarPosition = splitRadar.SplitterDistance;
				Program.Config.Save();
			}
		}

		private void OnWindowMoved(object sender, EventArgs e)
		{
			if(_layoutComplete)
			{
				Program.Config.LastRadarWindowLocation = Location;
			}
		}

		private void OnGrabInitialLandmarksClicked(object sender, EventArgs e)
		{
			MessageBox.Show("No way");
//			return;
			Landscape.CurrentLocation = Landscape.Center;
			LandmarkList landmarks = Landscape.CreateLandmarksFromImageVectors(Landscape.Center, _client.ImageMetrics.Scale, _client.ImageLandmarks);

			Landscape.Landmarks = landmarks;
			Landscape.ReplaceAllLandmarks();
		}

		private void OnLoadLandmarksClicked(object sender, EventArgs e)
		{
			ReloadLandmarks();
		}

		private void ReloadLandmarks()
		{
			Landscape.LoadLandmarks();

			Landscape.PreliminaryAnalysis();
			Mat bitmap = Landscape.CreateImage(PixelsPerMeterImage, SpatialObjects.Landmarks | SpatialObjects.GridLines | SpatialObjects.Labels);

			Bitmap output = bitmap.Bitmap;
			picFullEnvironment.BackgroundImage = output;
			picFullEnvironment.Size = output.Size;
		}

		private void OnSavePointMarkersClicked(object sender, EventArgs e)
		{
			_ds.PointMarkersClear(Landscape);

			LandmarkList landmarks = Landscape.CreateLandmarksFromImageVectors(Landscape.Center, _client.ImageMetrics.Scale, _client.ImageLandmarks);
			foreach(Landmark landmark in landmarks)
			{
				_ds.PointMarkerInsert(landmark);
			}

		}

		private void OnRunAnalysisClicked(object sender, EventArgs e)
		{
			LandmarkList pointMarkers;
			if(_ds.PointMarkersGet(Landscape, out pointMarkers).ResultCode == DBResult.Result.Success)
			{
				Landscape.PointMarkers = pointMarkers;
				Landscape.AnalyzeImageLandmarks();

				Mat bitmap = Landscape.CreateImage(PixelsPerMeterImage, SpatialObjects.Landmarks | SpatialObjects.GridLines | SpatialObjects.Labels | SpatialObjects.Circles);

				Bitmap output = bitmap.Bitmap;
				picFullEnvironment.BackgroundImage = output;
				picFullEnvironment.Size = output.Size;
			}
		}

		private void OnRadarMouseMove(object sender, MouseEventArgs e)
		{
			PointD cart = new PointD(e.Location);
			BearingAndRange vector = new BearingAndRange(picLidar.BackgroundImage.Center(), cart);
			vector.Range /= PixelsPerMeterImage;
			String text = String.Format("{0}, {1}  [{2:0.000}m {3:0.0}°]", cart.X, cart.Y, vector.Range, vector.Bearing);
			textRadarCateresian.Text = text;
		}

		private void OnLandscapeMouseMove(object sender, MouseEventArgs e)
		{
			PointD point = new PointD(e.Location);
			point.Scale(1 / PixelsPerMeterLandscape);
			textLandscapeCoords.Text = String.Format("{0}", point.ToString(3));
		}

		private void OnLandscapeMouseClick(object sender, MouseEventArgs e)
		{
			if(e.Button == MouseButtons.Right)
			{
				PointD point = new PointD(e.Location);
				point.Scale(1 / PixelsPerMeterLandscape);

				Landmark landmark;
				if((landmark = Landscape.Landmarks.GetLandmarkNear(point, .1)) != null)
				{
					cmenuLandscape.Tag = landmark;
					cmenuLandscape.Show(picFullEnvironment.PointToScreen(e.Location));
				}
			}
		}

		private void OnAssignLandmarkLabelClicked(object sender, EventArgs e)
		{
			SimpleTextSelectionDialog dlg = new SimpleTextSelectionDialog("Enter Label");
			if(dlg.ShowDialog() == DialogResult.OK)
			{
				Landmark landmark = cmenuLandscape.Tag as Landmark;
				landmark.Label = dlg.Answer;
				_ds.LandmarkUpdate(landmark);
			}
		}

		const String PULL_LOCATION = @"\\raspi\pi\tmp\rangeblob.bin";
		private void OnPullClicked(object sender, EventArgs args)
		{
			try
			{
				if(File.Exists(PULL_LOCATION))
				{
					byte[] blob = File.ReadAllBytes(PULL_LOCATION);
					LidarVector[] vectors = new LidarVector[blob.Length / sizeof(Double)];
					LidarVector.LoadFromRangeBlob(vectors, blob);

					Size bitmapSize = new Size((int)(_client.ImageMetrics.MetersSquare * AnalysisPixelsPerMeter), (int)(_client.ImageMetrics.MetersSquare * AnalysisPixelsPerMeter));
					_workBitmap = LidarVector.MakeBitmap(vectors, bitmapSize, AnalysisPixelsPerMeter, Color.White);
					picWorkBitmap.BackgroundImage = _workBitmap.Bitmap;
					picWorkBitmap.Size = bitmapSize;
					picWorkBitmap.BackgroundImage.Save(@"c:\pub\tmp\output.png");

					_sharpened = new Mat();
					CvInvoke.GaussianBlur(_workBitmap, _sharpened, new Size(0, 0), 1);
					_sharpened.Save(@"c:\pub\tmp\sharpened.png");
					CvInvoke.AddWeighted(_workBitmap, 1.5, _sharpened, -0.5, 0, _sharpened);
				}
			}
			catch(Exception e)
			{
				MessageBox.Show(e.Message);
			}
		}

		private void OnParmsClicked(object sender, EventArgs e)
		{
			ProcessingMetricsDialog dlg = new ProcessingMetricsDialog()
			{
				ProcessingMetrics = Program.Config.ProcessingMetrics
			};
			if(dlg.ShowDialog() == DialogResult.OK)
			{
				Program.Config.ProcessingMetrics = dlg.ProcessingMetrics;
				Program.Config.Save();
			}
		}

		private void OnAnalyzeClicked(object sender, EventArgs e)
		{
			LidarEnvironment env = new LidarEnvironment(ImageMetrics.MetersSquare, AnalysisPixelsPerMeter)
			{
				ProcessingMetrics = Program.Config.ProcessingMetrics
			};

			env.Location = picWorkBitmap.BackgroundImage.Center();
			env.ProcessImage(_sharpened, 0, AnalysisPixelsPerMeter);

			Mat output = env.CreateImage(SpatialObjects.Everything);
			picWorkBitmap.Image = output.Bitmap;
		}
	}
}
