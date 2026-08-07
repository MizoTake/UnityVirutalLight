using UnityEngine;

namespace MizoTake.VirtualLight.Samples
{
    [DisallowMultipleComponent]
    public sealed class VirtualLightSampleOverlay : MonoBehaviour
    {
        [SerializeField] private string title = "VIRTUAL LIGHT LAB";
        [SerializeField] private string subtitle = "POINT + SPOT SHAPE  /  SHADOW + BEAM  /  RECTANGLE AREA";
        [SerializeField] private string status = "RUNTIME SHAPE  -  POINT + SPOT / CIRCLE";
        [SerializeField] private Color statusColor = new Color(0.52f, 0.58f, 0.68f);
        [SerializeField] private bool showBeamStatus;
        [SerializeField] private string beamTitle = "BEAM VOLUME / FIRST-HIT OCCLUSION";
        [SerializeField] private string beamStatus = "CLEAR  -  FULL BEAM TO TARGET";
        [SerializeField] private Color beamStatusColor = new Color(0.12f, 0.82f, 1f);
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle statusStyle;
        private GUIStyle beamTitleStyle;
        private GUIStyle beamStatusStyle;
        private float cachedScale = -1f;

        public string Title { get => title; set => title = value; }
        public string Subtitle { get => subtitle; set => subtitle = value; }
        public string Status { get => status; set => status = value; }
        public Color StatusColor { get => statusColor; set => statusColor = value; }
        public bool ShowBeamStatus { get => showBeamStatus; set => showBeamStatus = value; }
        public string BeamTitle { get => beamTitle; set => beamTitle = value; }
        public string BeamStatus { get => beamStatus; set => beamStatus = value; }
        public Color BeamStatusColor { get => beamStatusColor; set => beamStatusColor = value; }

        private void OnGUI()
        {
            var scale = Mathf.Max(0.65f, Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));
            EnsureStyles(scale);
            var previousColor = GUI.color;
            GUI.color = new Color(0.015f, 0.02f, 0.04f, 0.88f);
            GUI.Box(new Rect(36f * scale, 36f * scale, 610f * scale, 126f * scale), GUIContent.none);
            GUI.color = previousColor;
            GUI.Label(new Rect(58f * scale, 49f * scale, 560f * scale, 35f * scale), title, titleStyle);
            GUI.Label(new Rect(58f * scale, 84f * scale, 560f * scale, 25f * scale), subtitle, subtitleStyle);
            statusStyle.normal.textColor = statusColor;
            GUI.Label(new Rect(58f * scale, 113f * scale, 560f * scale, 25f * scale), status, statusStyle);
            if (!showBeamStatus) return;
            GUI.color = new Color(0.015f, 0.025f, 0.05f, 0.9f);
            GUI.Box(new Rect(Screen.width - 426f * scale, 36f * scale, 390f * scale, 88f * scale), GUIContent.none);
            GUI.color = previousColor;
            GUI.Label(new Rect(Screen.width - 408f * scale, 48f * scale, 354f * scale, 25f * scale), beamTitle, beamTitleStyle);
            beamStatusStyle.normal.textColor = beamStatusColor;
            GUI.Label(new Rect(Screen.width - 408f * scale, 77f * scale, 354f * scale, 28f * scale), beamStatus, beamStatusStyle);
        }

        private void EnsureStyles(float scale)
        {
            if (Mathf.Approximately(cachedScale, scale) && titleStyle != null) return;
            cachedScale = scale;
            titleStyle = CreateStyle(28, FontStyle.Bold, new Color(0.94f, 0.97f, 1f), scale);
            subtitleStyle = CreateStyle(14, FontStyle.Normal, new Color(0.55f, 0.68f, 0.84f), scale);
            statusStyle = CreateStyle(14, FontStyle.Bold, statusColor, scale);
            beamTitleStyle = CreateStyle(14, FontStyle.Bold, new Color(0.58f, 0.7f, 0.85f), scale);
            beamStatusStyle = CreateStyle(16, FontStyle.Bold, beamStatusColor, scale);
        }

        private static GUIStyle CreateStyle(int fontSize, FontStyle fontStyle, Color color, float scale)
        {
            return new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(fontSize * scale), fontStyle = fontStyle, normal = { textColor = color } };
        }
    }
}
