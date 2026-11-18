using UnityEngine;

namespace ScreenBlanker
{
	/// <summary>
	/// Full-window blackout drawn with IMGUI, for platforms whose host cannot create native windows
	/// (see <see cref="Platform.InAppPlatformHost"/>). Mac and Windows never use it.
	/// </summary>
	public sealed class InAppBlackoutGui
	{
		private const float GraphicSize = 180.0f;
		private const float GroupGap = 12.0f;

		private GUIStyle promptStyle;
		private float promptMoveTime;
		private Vector2 promptPosition;

		/// <summary>
		/// Draws the blackout over the whole window. Call from OnGUI.
		/// </summary>
		/// <param name="promptText">Prompt to show, or null for none.</param>
		/// <param name="graphic">Graphic to show above the prompt, or null for none.</param>
		public void Draw(string promptText, Texture2D graphic)
		{
			if (promptStyle == null)
			{
				promptStyle = new GUIStyle(GUI.skin.label);
				promptStyle.fontSize = 16;
				promptStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
				promptStyle.wordWrap = false;
			}

			GUI.matrix = Matrix4x4.identity;
			GUI.color = Color.black;
			GUI.DrawTexture(new Rect(0.0f, 0.0f, Screen.width, Screen.height), Texture2D.whiteTexture);
			GUI.color = Color.white;

			if (promptText == null && graphic == null)
				return;

			Vector2 textSize = promptText == null ? Vector2.zero : promptStyle.CalcSize(new GUIContent(promptText));
			Vector2 graphicSize = Vector2.zero;
			if (graphic != null)
			{
				float graphicScale = Mathf.Min(1.0f, GraphicSize / Mathf.Max(graphic.width, graphic.height));
				graphicSize = new Vector2(graphic.width * graphicScale, graphic.height * graphicScale);
			}

			float gap = graphic != null && promptText != null ? GroupGap : 0.0f;
			Vector2 groupSize = new Vector2(Mathf.Max(textSize.x, graphicSize.x), graphicSize.y + gap + textSize.y);

			if (Time.unscaledTime - promptMoveTime >= 1.0f)
			{
				promptMoveTime = Time.unscaledTime;
				promptPosition = new Vector2(Random.Range(0.0f, Mathf.Max(0.0f, Screen.width - groupSize.x)), Random.Range(0.0f, Mathf.Max(0.0f, Screen.height - groupSize.y)));
			}

			if (graphic != null)
				GUI.DrawTexture(new Rect(promptPosition.x + ((groupSize.x - graphicSize.x) / 2.0f), promptPosition.y, graphicSize.x, graphicSize.y), graphic, ScaleMode.ScaleToFit);

			if (promptText != null)
				GUI.Label(new Rect(promptPosition.x + ((groupSize.x - textSize.x) / 2.0f), promptPosition.y + graphicSize.y + gap, textSize.x, textSize.y), promptText, promptStyle);
		}
	}
}
