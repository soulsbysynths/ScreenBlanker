namespace ScreenBlanker.Blackout
{
	/// <summary>
	/// A decoded, already-scaled image for the blackout, in the two forms the native hosts need.
	/// </summary>
	public sealed class BlackoutGraphic
	{
		/// <summary>
		/// Creates the graphic.
		/// </summary>
		/// <param name="png">PNG encoding of the scaled image (macOS loads this into an NSImage).</param>
		/// <param name="premultipliedBgra">Scaled pixels, rows top to bottom, BGRA with colour already
		/// multiplied by alpha (the layout GDI's AlphaBlend expects).</param>
		public BlackoutGraphic(int pixelWidth, int pixelHeight, byte[] png, byte[] premultipliedBgra)
		{
			PixelWidth = pixelWidth;
			PixelHeight = pixelHeight;
			Png = png;
			PremultipliedBgra = premultipliedBgra;
		}

		/// <summary>
		/// Width of the scaled image in pixels.
		/// </summary>
		public int PixelWidth { get; }

		/// <summary>
		/// Height of the scaled image in pixels.
		/// </summary>
		public int PixelHeight { get; }

		/// <summary>
		/// PNG-encoded scaled image.
		/// </summary>
		public byte[] Png { get; }

		/// <summary>
		/// Premultiplied BGRA pixels, top row first.
		/// </summary>
		public byte[] PremultipliedBgra { get; }
	}
}
