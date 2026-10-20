using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// プレイ中に HitFeelSettings の各トグルを切り替えるための画面パネル (IMGUI)。
    /// HitFeelToggles の bool フィールドをリフレクションで列挙するので、トグルを増やしてもここは修正不要。
    /// [Header] ごとに見出しと「ON / OFF」ボタンを出す。[ExcludeFromAll] の付いた項目は「すべて ON」の対象外。
    /// Tab (パッドは Select) でパネルの表示 / マウスカーソルの解放を切り替える。
    /// </summary>
    public class HitFeelDebugPanel : MonoBehaviour
    {
        public PlayerInputReader input;
        public PlayerCombat combat;
        public bool showOnStart = true;

        class Entry
        {
            public FieldInfo field;
            public string header;
            public string label;
            public string description;
            public bool excludeFromAll;
        }

        // パネルの表示範囲 (スクリーン座標・左上原点)。パネル上のクリックを攻撃と区別するために使う
        static bool s_Visible;
        static Rect s_ScreenRect;

        /// <summary>マウス位置 (Input System の座標・左下原点) がパネルの上にあるか。</summary>
        public static bool IsPointerOverPanel(Vector2 pointerPosition)
        {
            if (!s_Visible)
                return false;
            var guiPoint = new Vector2(pointerPosition.x, Screen.height - pointerPosition.y);
            return s_ScreenRect.Contains(guiPoint);
        }

        readonly List<Entry> m_Entries = new List<Entry>();
        bool m_Visible;
        Vector2 m_Scroll;
        GUIStyle m_HeaderStyle;
        GUIStyle m_SmallStyle;
        GUIStyle m_BoxStyle;
        GUIStyle m_HintStyle;

        void Awake()
        {
            string currentHeader = "";
            foreach (FieldInfo field in typeof(HitFeelToggles).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(bool))
                    continue;
                var header = field.GetCustomAttribute<HeaderAttribute>();
                if (header != null)
                    currentHeader = header.header;
                var tooltip = field.GetCustomAttribute<TooltipAttribute>();
                string text = tooltip != null ? tooltip.tooltip : field.Name;
                int split = text.IndexOf('：');
                m_Entries.Add(new Entry
                {
                    field = field,
                    header = currentHeader,
                    label = split > 0 ? text.Substring(0, split) : field.Name,
                    description = split > 0 ? text.Substring(split + 1) : text,
                    excludeFromAll = field.GetCustomAttribute<ExcludeFromAllAttribute>() != null,
                });
            }
        }

        void Start()
        {
            SetVisible(showOnStart);
        }

        void Update()
        {
            if (input != null && input.TogglePanelPressed)
                SetVisible(!m_Visible);
        }

        void SetVisible(bool visible)
        {
            m_Visible = visible;
            s_Visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
        }

        void OnDisable()
        {
            s_Visible = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void EnsureStyles()
        {
            if (m_HeaderStyle != null)
                return;
            m_HeaderStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
            m_HeaderStyle.normal.textColor = new Color(1f, 0.85f, 0.5f);
            m_SmallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            m_SmallStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            m_BoxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
            m_HintStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            m_HintStyle.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;

            DrawStatus(screenW, screenH);

            if (!m_Visible)
            {
                GUI.Label(new Rect(12, 10, 600, 24), "Tab: ヒット演出パネルを開く", m_HintStyle);
                return;
            }

            HitFeelToggles toggles = HitFeelSettings.T;
            const float width = 440f;
            var area = new Rect(10, 10, width, Mathf.Min(820f, screenH - 20));
            s_ScreenRect = new Rect(area.x * scale, area.y * scale, area.width * scale, area.height * scale);
            GUILayout.BeginArea(area, m_BoxStyle);
            GUILayout.Label("Hit Feel Lab  ―  ヒット演出 & 手触り ON/OFF", m_HeaderStyle);
            GUILayout.Label("WASD/左スティック: 移動   マウス/右スティック: 視点\n左クリック / J / □(X): 攻撃 (弱→弱→強→最強→超強)   Tab: パネル開閉\nQ / ホイールクリック / R3: ロックオン (ホイール / 右スティック左右で切り替え)", m_SmallStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("すべて ON (完成形)"))
                SetAll(toggles, true, null);
            if (GUILayout.Button("すべて OFF"))
                SetAll(toggles, false, null);
            GUILayout.EndHorizontal();

            m_Scroll = GUILayout.BeginScrollView(m_Scroll);
            string lastHeader = null;
            foreach (Entry e in m_Entries)
            {
                if (e.header != lastHeader)
                {
                    lastHeader = e.header;
                    GUILayout.Space(6);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(e.header, m_HeaderStyle);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("ON", GUILayout.Width(40)))
                        SetAll(toggles, true, e.header);
                    if (GUILayout.Button("OFF", GUILayout.Width(40)))
                        SetAll(toggles, false, e.header);
                    GUILayout.EndHorizontal();
                }
                bool value = (bool)e.field.GetValue(toggles);
                bool newValue = GUILayout.Toggle(value, new GUIContent(" " + e.label, e.description));
                if (newValue != value)
                    e.field.SetValue(toggles, newValue);
            }
            GUILayout.EndScrollView();

            GUILayout.Label(string.IsNullOrEmpty(GUI.tooltip) ? "(項目にマウスを乗せると説明が出ます)" : GUI.tooltip, m_SmallStyle, GUILayout.MinHeight(48));
            GUILayout.EndArea();
        }

        /// <summary>header が null ならすべての見出し、そうでなければその見出しの項目だけ切り替える。</summary>
        void SetAll(HitFeelToggles toggles, bool value, string header)
        {
            foreach (Entry e in m_Entries)
            {
                if (header != null && e.header != header)
                    continue;
                // 検証用・悪い例は「すべて ON (完成形)」では OFF にする(見出しごとの ON ボタンでは入る)
                bool v = value && !(header == null && e.excludeFromAll);
                e.field.SetValue(toggles, v);
            }
        }

        void DrawStatus(float screenW, float screenH)
        {
            var rect = new Rect(screenW - 470, screenH - 64, 460, 54);
            string combo = "-";
            if (combat != null)
            {
                int index = combat.ComboIndex;
                AttackData[] attacks = combat.combo;
                var parts = new string[attacks.Length];
                for (int i = 0; i < attacks.Length; i++)
                {
                    string label = attacks[i].ShortName;
                    parts[i] = i == index ? $"<b><color=#ffcc66>[{label}]</color></b>" : label;
                }
                combo = string.Join(" → ", parts);
            }
            string lastHit = HitFeedback.Instance != null ? HitFeedback.Instance.LastHitDescription : "-";
            var style = new GUIStyle(m_HintStyle) { richText = true, alignment = TextAnchor.LowerRight };
            GUI.Label(rect, $"コンボ: {combo}\n最後のヒット: {lastHit}", style);
        }
    }
}
