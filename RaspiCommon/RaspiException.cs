﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KanoopCommon.CommonObjects;

namespace RaspiCommon
{
	public class RaspiException : CommonException
	{
		public RaspiException(String format, params object[] parms)
			: base(format, parms) {}
	}
}