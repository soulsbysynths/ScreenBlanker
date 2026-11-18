using System;
using System.IO;
using UnityEngine;

namespace ScreenBlanker.Blackout
{
	/// <summary>
	/// Loads the blackout graphic (PNG or JPEG) and scales it to fit a square box, so the native hosts
	/// only ever draw a ready-made image at 1:1.
	/// </summary>
	public static class GraphicLoader
	{
		/// <summary>
		/// File name of the built-in graphic in StreamingAssets.
		/// </summary>
		public const string DefaultGraphicFileName = "DefaultGraphic.png";

		/// <summary>
		/// Full path of the built-in graphic.
		/// </summary>
		public static string DefaultGraphicPath => Path.Combine(Application.streamingAssetsPath, DefaultGraphicFileName);

		/// <summary>
		/// Resolves the user's setting: an empty path means the built-in graphic.
		/// </summary>
		public static string ResolvePath(string configuredPath)
		{
			return string.IsNullOrEmpty(configuredPath) ? DefaultGraphicPath : configuredPath;
		}

		/// <summary>
		/// Decodes an image file into a texture for previews. Returns null and an error message if the
		/// file is missing or not a PNG / JPEG. The caller owns (and must destroy) the texture.
		/// </summary>
		public static Texture2D LoadTexture(string path, out string error)
		{
			error = null;
			try
			{
				if (!File.Exists(path))
				{
					error = "File not found";
					return null;
				}

				Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
				if (!texture.LoadImage(File.ReadAllBytes(path)))
				{
					UnityEngine.Object.Destroy(texture);
					error = "Not a PNG or JPEG image";
					return null;
				}

				texture.hideFlags = HideFlags.HideAndDontSave;
				return texture;
			}
			catch (Exception exception)
			{
				error = exception.Message;
				return null;
			}
		}

		/// <summary>
		/// Loads and scales an image so its longer side is at most <paramref name="maxPixels"/>.
		/// Images are scaled down only, never up. Returns null if the file cannot be read.
		/// </summary>
		public static BlackoutGraphic Load(string path, int maxPixels)
		{
			Texture2D source = LoadTexture(path, out string error);
			if (source == null)
			{
				Debug.LogWarning("ScreenBlanker: blackout graphic not loaded (" + error + "): " + path);
				return null;
			}

			try
			{
				float scale = Mathf.Min(1.0f, maxPixels / (float)Mathf.Max(source.width, source.height));
				int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
				int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

				// GetPixels32 rows run bottom to top.
				Color32[] scaled = AreaResample(source.GetPixels32(), source.width, source.height, width, height);

				Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
				output.SetPixels32(scaled);
				output.Apply();
				byte[] png = output.EncodeToPNG();
				UnityEngine.Object.Destroy(output);

				return new BlackoutGraphic(width, height, png, ToPremultipliedBgraTopDown(scaled, width, height));
			}
			finally
			{
				UnityEngine.Object.Destroy(source);
			}
		}

		/// <summary>
		/// Box-filter downscale: each output pixel averages the source area it covers, weighted by
		/// coverage, in premultiplied space so transparent edges don't pick up dark fringes.
		/// </summary>
		private static Color32[] AreaResample(Color32[] source, int sourceWidth, int sourceHeight, int width, int height)
		{
			Color32[] result = new Color32[width * height];
			float stepX = sourceWidth / (float)width;
			float stepY = sourceHeight / (float)height;

			for (int y = 0; y < height; ++y)
			{
				float top = y * stepY;
				float bottom = top + stepY;
				for (int x = 0; x < width; ++x)
				{
					float left = x * stepX;
					float right = left + stepX;
					float red = 0.0f;
					float green = 0.0f;
					float blue = 0.0f;
					float alpha = 0.0f;
					float weightSum = 0.0f;

					for (int sy = (int)top; sy < Mathf.Min(sourceHeight, Mathf.CeilToInt(bottom)); ++sy)
					{
						float weightY = Mathf.Min(bottom, sy + 1) - Mathf.Max(top, sy);
						for (int sx = (int)left; sx < Mathf.Min(sourceWidth, Mathf.CeilToInt(right)); ++sx)
						{
							float weight = weightY * (Mathf.Min(right, sx + 1) - Mathf.Max(left, sx));
							Color32 pixel = source[(sy * sourceWidth) + sx];
							float pixelAlpha = pixel.a / 255.0f;
							red += pixel.r * pixelAlpha * weight;
							green += pixel.g * pixelAlpha * weight;
							blue += pixel.b * pixelAlpha * weight;
							alpha += pixelAlpha * weight;
							weightSum += weight;
						}
					}

					if (weightSum <= 0.0f || alpha <= 0.0f)
						continue;

					result[(y * width) + x] = new Color32(
						(byte)Mathf.Clamp(Mathf.RoundToInt(red / alpha), 0, 255),
						(byte)Mathf.Clamp(Mathf.RoundToInt(green / alpha), 0, 255),
						(byte)Mathf.Clamp(Mathf.RoundToInt(blue / alpha), 0, 255),
						(byte)Mathf.Clamp(Mathf.RoundToInt(alpha / weightSum * 255.0f), 0, 255));
				}
			}

			return result;
		}

		private static byte[] ToPremultipliedBgraTopDown(Color32[] pixels, int width, int height)
		{
			byte[] bgra = new byte[width * height * 4];
			for (int y = 0; y < height; ++y)
			{
				int sourceRow = (height - 1 - y) * width;
				for (int x = 0; x < width; ++x)
				{
					Color32 pixel = pixels[sourceRow + x];
					int offset = ((y * width) + x) * 4;
					bgra[offset] = (byte)((pixel.b * pixel.a) / 255);
					bgra[offset + 1] = (byte)((pixel.g * pixel.a) / 255);
					bgra[offset + 2] = (byte)((pixel.r * pixel.a) / 255);
					bgra[offset + 3] = pixel.a;
				}
			}

			return bgra;
		}
	}
}
