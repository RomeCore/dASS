using AsyncLua;
using AsyncLua.Values;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Vector primitives of <see cref="LuaImage"/>: rectangles, circles, lines and polygons, rasterized by hand and clipped
	/// to the image.
	/// </summary>
	/// <remarks>
	/// Every shape exists twice: a hex colour string with an optional thickness, or the channels as
	/// <c>r, g, b, [a], [thickness]</c>. Alpha sits before thickness in the numeric form, so a thickness always comes
	/// with an explicit alpha.
	/// </remarks>
	public sealed partial class LuaImage
	{
		/// <summary>
		/// Fills a rectangle, clipping the part outside the image.
		/// </summary>
		public void FillRect(int x, int y, int width, int height, string color)
			=> FillRectCore(x, y, width, height, ReadColor(new LuaString(color), "image:fill_rect(...)"));

		/// <summary>
		/// Fills a rectangle with the given channels.
		/// </summary>
		public void FillRect(int x, int y, int width, int height, byte r, byte g, byte b, byte a = 255)
			=> FillRectCore(x, y, width, height, new Rgba32(r, g, b, a));

		/// <summary>
		/// Strokes the outline of a rectangle.
		/// </summary>
		public void DrawRect(int x, int y, int width, int height, string color, int thickness = 1)
			=> DrawRectCore(x, y, width, height, ReadColor(new LuaString(color), "image:draw_rect(...)"), thickness);

		/// <summary>
		/// Strokes the outline of a rectangle with the given channels.
		/// </summary>
		public void DrawRect(int x, int y, int width, int height, byte r, byte g, byte b, byte a = 255, int thickness = 1)
			=> DrawRectCore(x, y, width, height, new Rgba32(r, g, b, a), thickness);

		/// <summary>
		/// Fills a circle.
		/// </summary>
		public void FillCircle(int x, int y, int radius, string color)
			=> FillCircleCore(x, y, radius, ReadColor(new LuaString(color), "image:fill_circle(...)"));

		/// <summary>
		/// Fills a circle with the given channels.
		/// </summary>
		public void FillCircle(int x, int y, int radius, byte r, byte g, byte b, byte a = 255)
			=> FillCircleCore(x, y, radius, new Rgba32(r, g, b, a));

		/// <summary>
		/// Strokes a circle.
		/// </summary>
		public void DrawCircle(int x, int y, int radius, string color, int thickness = 1)
			=> DrawCircleCore(x, y, radius, ReadColor(new LuaString(color), "image:draw_circle(...)"), thickness);

		/// <summary>
		/// Strokes a circle with the given channels.
		/// </summary>
		public void DrawCircle(int x, int y, int radius, byte r, byte g, byte b, byte a = 255, int thickness = 1)
			=> DrawCircleCore(x, y, radius, new Rgba32(r, g, b, a), thickness);

		/// <summary>
		/// Fills an axis-aligned ellipse with the given radii.
		/// </summary>
		public void FillEllipse(int x, int y, int radiusX, int radiusY, string color)
			=> FillEllipseCore(x, y, radiusX, radiusY, ReadColor(new LuaString(color), "image:fill_ellipse(...)"));

		/// <summary>
		/// Fills an axis-aligned ellipse with the given channels.
		/// </summary>
		public void FillEllipse(int x, int y, int radiusX, int radiusY, byte r, byte g, byte b, byte a = 255)
			=> FillEllipseCore(x, y, radiusX, radiusY, new Rgba32(r, g, b, a));

		/// <summary>
		/// Strokes an axis-aligned ellipse with the given radii.
		/// </summary>
		public void DrawEllipse(int x, int y, int radiusX, int radiusY, string color, int thickness = 1)
			=> DrawEllipseCore(x, y, radiusX, radiusY, ReadColor(new LuaString(color), "image:draw_ellipse(...)"), thickness);

		/// <summary>
		/// Strokes an axis-aligned ellipse with the given channels.
		/// </summary>
		public void DrawEllipse(int x, int y, int radiusX, int radiusY, byte r, byte g, byte b, byte a = 255, int thickness = 1)
			=> DrawEllipseCore(x, y, radiusX, radiusY, new Rgba32(r, g, b, a), thickness);

		/// <summary>
		/// Draws a line.
		/// </summary>
		public void DrawLine(int x1, int y1, int x2, int y2, string color, int thickness = 1)
			=> DrawLineCore(x1, y1, x2, y2, ReadColor(new LuaString(color), "image:draw_line(...)"), thickness);

		/// <summary>
		/// Draws a line with the given channels.
		/// </summary>
		public void DrawLine(int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a = 255, int thickness = 1)
			=> DrawLineCore(x1, y1, x2, y2, new Rgba32(r, g, b, a), thickness);

		/// <summary>
		/// Fills a polygon from a list of points, using the even-odd rule.
		/// </summary>
		public void FillPolygon(LuaTable points, string color)
			=> FillPolygonCore(ReadPoints(points, "image:fill_polygon(...)"),
				ReadColor(new LuaString(color), "image:fill_polygon(...)"));

		/// <summary>
		/// Fills a polygon with the given channels.
		/// </summary>
		public void FillPolygon(LuaTable points, byte r, byte g, byte b, byte a = 255)
			=> FillPolygonCore(ReadPoints(points, "image:fill_polygon(...)"), new Rgba32(r, g, b, a));

		/// <summary>
		/// Strokes a closed polygon.
		/// </summary>
		public void DrawPolygon(LuaTable points, string color, int thickness = 1)
		{
			var parsed = ReadPoints(points, "image:draw_polygon(...)");
			var stroke = ReadColor(new LuaString(color), "image:draw_polygon(...)");
			for (var index = 0; index < parsed.Count; index++)
			{
				var from = parsed[index];
				var to = parsed[(index + 1) % parsed.Count];
				DrawLineCore(from.X, from.Y, to.X, to.Y, stroke, thickness);
			}

			Revision++;
		}

		/// <summary>
		/// Strokes a closed polygon with the given channels.
		/// </summary>
		public void DrawPolygon(LuaTable points, byte r, byte g, byte b, byte a = 255, int thickness = 1)
		{
			var parsed = ReadPoints(points, "image:draw_polygon(...)");
			var stroke = new Rgba32(r, g, b, a);
			for (var index = 0; index < parsed.Count; index++)
			{
				var from = parsed[index];
				var to = parsed[(index + 1) % parsed.Count];
				DrawLineCore(from.X, from.Y, to.X, to.Y, stroke, thickness);
			}

			Revision++;
		}

		/// <summary>
		/// Strokes an open polyline: the points are connected, but the last one is not joined back to the first.
		/// </summary>
		public void DrawPolyline(LuaTable points, string color, int thickness = 1)
		{
			var parsed = ReadPoints(points, "image:draw_polyline(...)");
			var stroke = ReadColor(new LuaString(color), "image:draw_polyline(...)");
			for (var index = 0; index + 1 < parsed.Count; index++)
				DrawLineCore(parsed[index].X, parsed[index].Y, parsed[index + 1].X, parsed[index + 1].Y, stroke, thickness);

			Revision++;
		}

		/// <summary>
		/// Strokes an open polyline with the given channels.
		/// </summary>
		public void DrawPolyline(LuaTable points, byte r, byte g, byte b, byte a = 255, int thickness = 1)
		{
			var parsed = ReadPoints(points, "image:draw_polyline(...)");
			var stroke = new Rgba32(r, g, b, a);
			for (var index = 0; index + 1 < parsed.Count; index++)
				DrawLineCore(parsed[index].X, parsed[index].Y, parsed[index + 1].X, parsed[index + 1].Y, stroke, thickness);

			Revision++;
		}

		/// <summary>
		/// Fills a rectangle, clipped to the image, without touching <see cref="Revision"/>.
		/// </summary>
		private void FillRectCore(int x, int y, int width, int height, Rgba32 fill)
		{
			if (width <= 0 || height <= 0)
				return;

			var pixels = Pixels;
			var left = Math.Max(0, x);
			var top = Math.Max(0, y);
			var right = Math.Min(pixels.Width, x + width);
			var bottom = Math.Min(pixels.Height, y + height);
			if (right <= left || bottom <= top)
				return;

			pixels.ProcessPixelRows(accessor =>
			{
				for (var row = top; row < bottom; row++)
					accessor.GetRowSpan(row)[left..right].Fill(fill);
			});

			Revision++;
		}

		/// <summary>
		/// Strokes a rectangle as four bars, each one clipped to the image.
		/// </summary>
		private void DrawRectCore(int x, int y, int width, int height, Rgba32 fill, int thickness)
		{
			var stroke = Math.Max(1, thickness);
			if (width <= 0 || height <= 0)
				return;

			FillRectCore(x, y, width, stroke, fill);
			FillRectCore(x, y + height - stroke, width, stroke, fill);
			FillRectCore(x, y, stroke, height, fill);
			FillRectCore(x + width - stroke, y, stroke, height, fill);
		}

		/// <summary>
		/// Fills a circle, clipped to the image.
		/// </summary>
		private void FillCircleCore(int x, int y, int radius, Rgba32 fill)
			=> FillEllipseCore(x, y, radius, radius, fill);

		/// <summary>
		/// Fills an axis-aligned ellipse row by row, clipped to the image.
		/// </summary>
		private void FillEllipseCore(int x, int y, int radiusX, int radiusY, Rgba32 fill)
		{
			if (radiusX < 0 || radiusY < 0)
				return;

			// A flat ellipse degenerates into a line, which the row loop below cannot express.
			if (radiusY == 0)
			{
				FillRectCore(x - radiusX, y, (2 * radiusX) + 1, 1, fill);
				return;
			}

			var pixels = Pixels;
			var top = Math.Max(0, y - radiusY);
			var bottom = Math.Min(pixels.Height - 1, y + radiusY);
			var squared = (double)radiusY * radiusY;

			pixels.ProcessPixelRows(accessor =>
			{
				for (var row = top; row <= bottom; row++)
				{
					var offset = row - y;
					var halfWidth = (int)Math.Floor(radiusX * Math.Sqrt(1.0 - (offset * offset / squared)));
					var left = Math.Max(0, x - halfWidth);
					var right = Math.Min(pixels.Width - 1, x + halfWidth);
					if (right >= left)
						accessor.GetRowSpan(row)[left..(right + 1)].Fill(fill);
				}
			});

			Revision++;
		}

		/// <summary>
		/// Strokes a circle.
		/// </summary>
		private void DrawCircleCore(int x, int y, int radius, Rgba32 fill, int thickness)
			=> DrawEllipseCore(x, y, radius, radius, fill, thickness);

		/// <summary>
		/// Strokes an axis-aligned ellipse by keeping the pixels within half a thickness of the curve.
		/// </summary>
		/// <remarks>
		/// The distance is measured from the implicit equation and its gradient, so a constant pixel thickness holds even
		/// where the ellipse is strongly elongated; a plain normalised distance would thin the ends.
		/// </remarks>
		private void DrawEllipseCore(int x, int y, int radiusX, int radiusY, Rgba32 fill, int thickness)
		{
			if (radiusX < 0 || radiusY < 0)
				return;

			var stroke = Math.Max(1, thickness);
			if (radiusX == 0 || radiusY == 0)
			{
				// A degenerate ellipse is a line, or a single pixel when both radii are zero.
				if (radiusX == 0 && radiusY == 0)
					FillRectCore(x, y, 1, 1, fill);
				else if (radiusY == 0)
					DrawLineCore(x - radiusX, y, x + radiusX, y, fill, stroke);
				else
					DrawLineCore(x, y - radiusY, x, y + radiusY, fill, stroke);
				return;
			}

			var pixels = Pixels;
			var top = Math.Max(0, y - radiusY - stroke);
			var bottom = Math.Min(pixels.Height - 1, y + radiusY + stroke);
			var left = Math.Max(0, x - radiusX - stroke);
			var right = Math.Min(pixels.Width - 1, x + radiusX + stroke);
			var half = stroke / 2.0;
			var squaredX = (double)radiusX * radiusX;
			var squaredY = (double)radiusY * radiusY;

			pixels.ProcessPixelRows(accessor =>
			{
				for (var row = top; row <= bottom; row++)
				{
					var rowSpan = accessor.GetRowSpan(row);
					var deltaY = row - y;
					for (var column = left; column <= right; column++)
					{
						var deltaX = column - x;
						var value = (((double)deltaX * deltaX) / squaredX) + (((double)deltaY * deltaY) / squaredY) - 1.0;
						var gradientX = deltaX / squaredX;
						var gradientY = deltaY / squaredY;
						var gradient = 2.0 * Math.Sqrt((gradientX * gradientX) + (gradientY * gradientY));
						// At the centre the gradient vanishes, and that pixel is as far from the curve as it gets.
												var distance = gradient > 1e-9 ? Math.Abs(value) / gradient : double.PositiveInfinity;
						if (distance <= half)
							rowSpan[column] = fill;
					}
				}
			});

			Revision++;
		}

		/// <summary>
		/// Draws a line with Bresenham's algorithm, stamping a small disc at every step for thickness.
		/// </summary>
		private void DrawLineCore(int x1, int y1, int x2, int y2, Rgba32 fill, int thickness)
		{
			var stamp = Math.Max(0, (Math.Max(1, thickness) - 1) / 2);
			var deltaX = Math.Abs(x2 - x1);
			var stepX = x1 < x2 ? 1 : -1;
			var deltaY = -Math.Abs(y2 - y1);
			var stepY = y1 < y2 ? 1 : -1;
			var error = deltaX + deltaY;

			while (true)
			{
				FillCircleCore(x1, y1, stamp, fill);
				if (x1 == x2 && y1 == y2)
					break;

				var doubled = 2 * error;
				if (doubled >= deltaY)
				{
					error += deltaY;
					x1 += stepX;
				}

				if (doubled <= deltaX)
				{
					error += deltaX;
					y1 += stepY;
				}
			}
		}

		/// <summary>
		/// Fills a polygon with a scanline pass, pairing edge crossings on every row.
		/// </summary>
		private void FillPolygonCore(List<(int X, int Y)> points, Rgba32 fill)
		{
			if (points.Count < 3)
				return;

			var pixels = Pixels;
			var top = Math.Max(0, points.Min(point => point.Y));
			var bottom = Math.Min(pixels.Height - 1, points.Max(point => point.Y));
			if (bottom < top)
				return;

			var crossings = new List<double>();
			pixels.ProcessPixelRows(accessor =>
			{
				for (var row = top; row <= bottom; row++)
				{
					crossings.Clear();
					for (var index = 0; index < points.Count; index++)
					{
						var from = points[index];
						var to = points[(index + 1) % points.Count];
						if ((from.Y <= row && to.Y > row) || (to.Y <= row && from.Y > row))
							crossings.Add(from.X + ((double)(row - from.Y) * (to.X - from.X) / (to.Y - from.Y)));
					}

					crossings.Sort();
					var rowSpan = accessor.GetRowSpan(row);
					for (var index = 0; index + 1 < crossings.Count; index += 2)
					{
						var left = Math.Max(0, (int)Math.Ceiling(crossings[index]));
						var right = Math.Min(pixels.Width - 1, (int)Math.Floor(crossings[index + 1]));
						if (right >= left)
							rowSpan[left..(right + 1)].Fill(fill);
					}
				}
			});

			Revision++;
		}

		/// <summary>
		/// Reads a Lua list of points, each either <c>{ x = ..., y = ... }</c> or <c>{ x, y }</c>.
		/// </summary>
		private static List<(int X, int Y)> ReadPoints(LuaTable table, string call)
		{
			if (table.Length == 0)
				throw new LuaRuntimeException($"{call}: points must be a non-empty array of points.");

			var points = new List<(int X, int Y)>(table.Length);
			for (var index = 1; index <= table.Length; index++)
			{
				if (table.Get(index) is not LuaTable point)
					throw new LuaRuntimeException($"{call}: every point must be a table.");

				points.Add((ReadCoordinate(point, "x", 1, call), ReadCoordinate(point, "y", 2, call)));
			}

			return points;
		}

		/// <summary>
		/// Reads one coordinate of a point, accepting either the named or the positional form.
		/// </summary>
		private static int ReadCoordinate(LuaTable point, string key, int index, string call)
		{
			if (point.Get(key) is LuaNumber named)
				return (int)Math.Round(named.Value);
			if (point.Get(index) is LuaNumber positional)
				return (int)Math.Round(positional.Value);

			throw new LuaRuntimeException($"{call}: every point must be {{ x = ..., y = ... }} or {{ x, y }}.");
		}
	}
}
