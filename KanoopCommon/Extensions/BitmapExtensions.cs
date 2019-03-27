﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KanoopCommon.Extensions
{
	public static class BitmapExtensions
	{
		public static Bitmap Resize(this Bitmap bitmap, SizeF size)
		{
			return bitmap.Resize(size.Width, size.Height);
		}

		public static Bitmap Resize(this Bitmap bitmap, float width, float height)
		{
			return Resize(bitmap, Color.Transparent, width, height);
		}

		public static Bitmap Resize(this Bitmap bitmap, Color backColor, float width, float height)
		{
			SolidBrush brush = new SolidBrush(backColor);
			float scale = Math.Min(width / bitmap.Width, height / bitmap.Height);

			Bitmap ret = new Bitmap((int) width, (int) height);
			using(Graphics gr = Graphics.FromImage(ret))
			{
				gr.InterpolationMode = InterpolationMode.High;
				gr.CompositingQuality = CompositingQuality.HighQuality;
				gr.SmoothingMode = SmoothingMode.AntiAlias;

				int scaleWidth = (int)(bitmap.Width * scale);
				int scaleHeight = (int)(bitmap.Height * scale);

				gr.FillRectangle(brush, new RectangleF(0, 0, width, height));
				gr.DrawImage(bitmap, ((int)width - scaleWidth) / 2, ((int)height / scaleHeight) / 2, scaleWidth, scaleHeight);
			}

			return ret;
		}

		public static Icon GetIcon(Assembly assembly, String resourceName)
		{
			List<String> names = new List<string>(assembly.GetManifestResourceNames());
			Stream stream = assembly.GetManifestResourceStream(resourceName);
			Bitmap bitmap = new Bitmap(stream);
			IntPtr handle = bitmap.GetHicon();
			Icon icon = Icon.FromHandle(handle);
			return icon;
		}
	}
}