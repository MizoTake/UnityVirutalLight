using UnityEngine;

namespace MizoTake.VirtualLight.Samples
{
    [DisallowMultipleComponent]
    public sealed class VirtualLightAreaDirectionOverlay : MonoBehaviour
    {
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle stationTitleStyle;
        private GUIStyle stationResultStyle;
        private float cachedScale = -1f;

        private void OnGUI()
        {
            var scale = Mathf.Max(0.65f, Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));
            EnsureStyles(scale);
            var previousColor = GUI.color;
            GUI.color = new Color(0.015f, 0.02f, 0.04f, 0.9f);
            GUI.Box(new Rect(36f * scale, 36f * scale, 780f * scale, 118f * scale), GUIContent.none);
            GUI.color = previousColor;
            GUI.Label(new Rect(58f * scale, 49f * scale, 730f * scale, 35f * scale), "RECTANGLE AREA DIRECTION", titleStyle);
            GUI.Label(new Rect(58f * scale, 84f * scale, 730f * scale, 25f * scale), "TRANSFORM FORWARD = EMISSION NORMAL  /  LOCAL +Z", subtitleStyle);
            GUI.Label(new Rect(58f * scale, 113f * scale, 730f * scale, 25f * scale), "SAME LIGHT SETTINGS  -  ONLY ROTATION AND TWO SIDED CHANGE", subtitleStyle);
            var margin = 36f * scale;
            var gap = 18f * scale;
            var width = (Screen.width - margin * 2f - gap * 2f) / 3f;
            var y = Screen.height - 132f * scale;
            DrawStation(new Rect(margin, y, width, 96f * scale), "FORWARD-FACING / ONE SIDED", "DOWNWARD RECEIVER: LIT", new Color(1f, 0.48f, 0.14f), previousColor);
            DrawStation(new Rect(margin + width + gap, y, width, 96f * scale), "BACK-FACING / ONE SIDED", "DOWNWARD RECEIVER: DARK", new Color(0.72f, 0.32f, 0.46f), previousColor);
            DrawStation(new Rect(margin + (width + gap) * 2f, y, width, 96f * scale), "BACK-FACING / TWO SIDED", "DOWNWARD RECEIVER: LIT", new Color(0.12f, 0.82f, 1f), previousColor);
        }

        private void DrawStation(Rect rect, string title, string result, Color color, Color previousColor)
        {
            GUI.color = new Color(0.015f, 0.02f, 0.04f, 0.88f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = previousColor;
            stationTitleStyle.normal.textColor = new Color(0.72f, 0.78f, 0.88f);
            stationResultStyle.normal.textColor = color;
            GUI.Label(new Rect(rect.x + 18f, rect.y + 13f, rect.width - 36f, rect.height * 0.35f), title, stationTitleStyle);
            GUI.Label(new Rect(rect.x + 18f, rect.y + rect.height * 0.48f, rect.width - 36f, rect.height * 0.35f), result, stationResultStyle);
        }

        private void EnsureStyles(float scale)
        {
            if (Mathf.Approximately(cachedScale, scale) && titleStyle != null) return;
            cachedScale = scale;
            titleStyle = CreateStyle(28, FontStyle.Bold, new Color(0.94f, 0.97f, 1f), scale);
            subtitleStyle = CreateStyle(14, FontStyle.Bold, new Color(0.55f, 0.68f, 0.84f), scale);
            stationTitleStyle = CreateStyle(13, FontStyle.Bold, new Color(0.72f, 0.78f, 0.88f), scale);
            stationResultStyle = CreateStyle(17, FontStyle.Bold, Color.white, scale);
        }

        private static GUIStyle CreateStyle(int fontSize, FontStyle fontStyle, Color color, float scale)
        {
            return new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(fontSize * scale), fontStyle = fontStyle, alignment = TextAnchor.MiddleCenter, normal = { textColor = color } };
        }
    }
}
