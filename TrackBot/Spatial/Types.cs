using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrackBot.Spatial
{
	public enum ActivityType
	{
		None,
		RoamAndSeekUS,
		RoamAndSeek,
		TravelLongestPath,
		GoToDestination
	}

	public enum CellContents
	{
		Unknown = 0,
		Empty = 1,
		Barrier = 2,

		Special
	}
}
