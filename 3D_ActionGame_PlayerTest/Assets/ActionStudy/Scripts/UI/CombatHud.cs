using System.Collections.Generic;
using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// 戦闘の UI フィードバック (IMGUI)。「行動の結果をプレイヤーへ返す」ための表示をまとめたもの。
    ///  ・HP ゲージ(遅延ゲージ) … 減った分を白いゲージで少し残してから減らす。被弾の瞬間はゲージも揺らす
    ///  ・ダメージ数値          … 着弾点から弾けて浮かぶ。攻撃の格で大きさと色が変わる
    ///  ・コンボカウンター      … 連続ヒット数。増えるたびに弾ませ、途切れるまでの残り時間をバーで見せる
    /// 時間はすべて実時間で進める(ヒットストップ中も数値の「弾み」は動く)。
    /// </summary>
    public class CombatHud : MonoBehaviour
    {
        [Header("HP ゲージ")]
        public Vector2 gaugeSize = new Vector2(96f, 9f);
        [Tooltip("頭のてっぺんからどれだけ上に出すか (m)")]
        public float gaugeHeightOffset = 0.35f;
        [Tooltip("白いゲージが減り始めるまでの時間(秒)")]
        public float gaugeHoldTime = 0.45f;
        [Tooltip("白いゲージが減る速さ (1 秒あたりの割合)")]
        public float gaugeDrainSpeed = 1.4f;
        [Tooltip("この距離より遠い敵のゲージは出さない")]
        public float gaugeMaxDistance = 22f;

        [Header("ダメージ数値")]
        public float numberLifetime = 0.75f;
        [Tooltip("浮かび上がる高さ (m)")]
        public float numberRise = 0.7f;
        public int numberFontSize = 26;

        [Header("コンボカウンター")]
        [Tooltip("この時間ヒットが無いとコンボが途切れる(秒)")]
        public float comboTimeout = 2.2f;

        class GaugeState
        {
            public float delayed = 1f;
            public float lastHitTime = -10f;
            public Vector3 lastHead;
        }

        struct DamageNumber
        {
            public Vector3 position;
            public float value;
            public AttackTier tier;
            public bool killed;
            public float bornTime;
            public float drift;
        }

        readonly Dictionary<Enemy, GaugeState> m_Gauges = new Dictionary<Enemy, GaugeState>();
        readonly List<Enemy> m_Removed = new List<Enemy>();
        readonly List<DamageNumber> m_Numbers = new List<DamageNumber>();
        int m_Combo;
        int m_BestCombo;
        float m_LastComboHitTime = -10f;
        int m_LastComboFrame = -1;

        GUIStyle m_NumberStyle;
        GUIStyle m_ComboStyle;
        GUIStyle m_ComboLabelStyle;

        void OnEnable()
        {
            HitFeedback.HitLanded += OnHit;
        }

        void OnDisable()
        {
            HitFeedback.HitLanded -= OnHit;
        }

        void OnHit(HitInfo info)
        {
            float now = Time.unscaledTime;
            Enemy enemy = info.target;
            if (enemy != null)
            {
                if (!m_Gauges.TryGetValue(enemy, out GaugeState gauge))
                {
                    // 初めて当たった敵は、減る前の HP から白ゲージを始める
                    gauge = new GaugeState { delayed = Mathf.Clamp01(enemy.HpRatio + info.attack.damage / Mathf.Max(enemy.maxHp, 1f)) };
                    m_Gauges[enemy] = gauge;
                }
                gauge.lastHitTime = now;
            }

            m_Numbers.Add(new DamageNumber
            {
                position = info.point,
                value = info.attack.damage,
                tier = info.attack.tier,
                killed = info.killed,
                bornTime = now,
                drift = Random.Range(-0.25f, 0.25f),
            });

            // 同じフレームに複数の敵を斬ったら、その分だけ数える
            if (now - m_LastComboHitTime > comboTimeout && m_LastComboFrame != Time.frameCount)
                m_Combo = 0;
            m_Combo++;
            m_BestCombo = Mathf.Max(m_BestCombo, m_Combo);
            m_LastComboHitTime = now;
            m_LastComboFrame = Time.frameCount;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;

            foreach (Enemy enemy in Enemy.All)
            {
                if (!m_Gauges.ContainsKey(enemy))
                    m_Gauges[enemy] = new GaugeState { delayed = enemy.HpRatio };
            }

            m_Removed.Clear();
            foreach (KeyValuePair<Enemy, GaugeState> pair in m_Gauges)
            {
                Enemy enemy = pair.Key;
                GaugeState gauge = pair.Value;
                float hp = enemy != null ? enemy.HpRatio : 0f;
                if (now - gauge.lastHitTime > gaugeHoldTime)
                    gauge.delayed = Mathf.MoveTowards(gauge.delayed, hp, gaugeDrainSpeed * dt);
                gauge.delayed = Mathf.Max(gauge.delayed, hp);
                if (enemy != null)
                    gauge.lastHead = enemy.HeadPosition;
                // 倒した敵は、白ゲージが減りきるまで表示してから消す
                if (enemy == null || (enemy.IsDead && gauge.delayed <= 0.001f))
                    m_Removed.Add(enemy);
            }
            foreach (Enemy enemy in m_Removed)
                m_Gauges.Remove(enemy);

            m_Numbers.RemoveAll(n => now - n.bornTime > numberLifetime);
            if (now - m_LastComboHitTime > comboTimeout)
                m_Combo = 0;
        }

        void EnsureStyles()
        {
            if (m_NumberStyle != null)
                return;
            m_NumberStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            m_ComboStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight, clipping = TextClipping.Overflow };
            m_ComboLabelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight, clipping = TextClipping.Overflow };
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            EnsureStyles();
            GUI.depth = 10; // デバッグパネルより奥に描く
            GUI.matrix = Matrix4x4.identity; // 画面のピクセル座標で描く
            float scale = Screen.height / 1080f;
            HitFeelToggles t = HitFeelSettings.T;

            if (t.hpGauge)
                DrawGauges(cam, scale);
            if (t.damageNumbers)
                DrawNumbers(cam, scale);
            if (t.comboCounter)
                DrawCombo(scale);
        }

        void DrawGauges(Camera cam, float scale)
        {
            float now = Time.unscaledTime;
            foreach (KeyValuePair<Enemy, GaugeState> pair in m_Gauges)
            {
                Enemy enemy = pair.Key;
                GaugeState gauge = pair.Value;
                // 敵が今のフレームの姿勢を決めた後(OnGUI は LateUpdate の後)に読むので、揺れに遅れない
                Vector3 head = (enemy != null ? enemy.HeadPosition : gauge.lastHead) + Vector3.up * gaugeHeightOffset;
                if (Vector3.Distance(cam.transform.position, head) > gaugeMaxDistance)
                    continue;
                Vector3 screen = cam.WorldToScreenPoint(head);
                if (screen.z <= 0f)
                    continue;

                float hp = enemy != null ? enemy.HpRatio : 0f;
                float sinceHit = now - gauge.lastHitTime;
                // 被弾の瞬間だけゲージを横に揺らす
                float shake = sinceHit < 0.18f ? Mathf.Sin(sinceHit * 70f) * 4f * (1f - sinceHit / 0.18f) * scale : 0f;
                float w = gaugeSize.x * scale;
                float h = gaugeSize.y * scale;
                var rect = new Rect(screen.x - w * 0.5f + shake, Screen.height - screen.y - h * 0.5f, w, h);

                Fill(new Rect(rect.x - scale, rect.y - scale, rect.width + 2f * scale, rect.height + 2f * scale), new Color(0f, 0f, 0f, 0.65f));
                Fill(new Rect(rect.x, rect.y, rect.width * gauge.delayed, rect.height), new Color(1f, 0.95f, 0.85f, 0.95f));
                // 被弾直後は HP 部分も白く光らせる
                Color hpColor = Color.Lerp(new Color(0.95f, 0.25f, 0.18f), Color.white, sinceHit < 0.08f ? 1f - sinceHit / 0.08f : 0f);
                Fill(new Rect(rect.x, rect.y, rect.width * hp, rect.height), hpColor);
            }
        }

        void DrawNumbers(Camera cam, float scale)
        {
            float now = Time.unscaledTime;
            foreach (DamageNumber n in m_Numbers)
            {
                float age = now - n.bornTime;
                float u = Mathf.Clamp01(age / numberLifetime);
                Vector3 world = n.position + Vector3.up * (numberRise * Ease.OutCubic(u)) + cam.transform.right * (n.drift * u);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                    continue;

                // 出た瞬間に大きく弾けて、すぐ落ち着く
                float pop = age < 0.09f ? Mathf.Lerp(1.9f, 1f, age / 0.09f) : 1f;
                float tierScale = n.tier == AttackTier.Light ? 1f : n.tier == AttackTier.Heavy ? 1.25f : n.tier == AttackTier.Finisher ? 1.5f : 1.8f;
                if (n.killed)
                    tierScale *= 1.15f;
                int size = Mathf.RoundToInt(numberFontSize * tierScale * pop * scale);
                m_NumberStyle.fontSize = Mathf.Max(8, size);

                Color color = n.tier == AttackTier.Light ? Color.white
                    : n.tier == AttackTier.Heavy ? new Color(1f, 0.9f, 0.45f)
                    : n.tier == AttackTier.Finisher ? new Color(1f, 0.6f, 0.25f)
                    : new Color(0.6f, 0.85f, 1f);
                color.a = u < 0.65f ? 1f : 1f - (u - 0.65f) / 0.35f;

                var rect = new Rect(screen.x - 100f, Screen.height - screen.y - 40f, 200f, 80f);
                string text = n.value.ToString("0");
                DrawOutlined(rect, text, m_NumberStyle, color, Mathf.Max(1f, 2f * scale));
            }
        }

        void DrawCombo(float scale)
        {
            if (m_Combo < 2)
                return;
            float sinceHit = Time.unscaledTime - m_LastComboHitTime;
            float remain = 1f - Mathf.Clamp01(sinceHit / comboTimeout);
            float pop = sinceHit < 0.12f ? Mathf.Lerp(1.45f, 1f, Ease.OutQuad(sinceHit / 0.12f)) : 1f;

            float right = Screen.width - 40f * scale;
            float baseline = Screen.height * 0.38f;
            m_ComboStyle.fontSize = Mathf.RoundToInt(64f * pop * scale);
            Color color = m_Combo >= 20 ? new Color(1f, 0.6f, 0.25f) : m_Combo >= 10 ? new Color(1f, 0.88f, 0.45f) : Color.white;
            color.a = Mathf.Clamp01(remain * 4f);
            DrawOutlined(new Rect(right - 400f * scale, baseline - 120f * scale, 400f * scale, 120f * scale), m_Combo.ToString(), m_ComboStyle, color, 2.5f * scale);

            m_ComboLabelStyle.fontSize = Mathf.RoundToInt(20f * scale);
            DrawOutlined(new Rect(right - 400f * scale, baseline + 2f * scale, 400f * scale, 30f * scale), $"HITS   (最高 {m_BestCombo})", m_ComboLabelStyle, new Color(1f, 1f, 1f, color.a), 1.5f * scale);

            // 途切れるまでの残り時間
            float barWidth = 180f * scale;
            var bar = new Rect(right - barWidth, baseline + 34f * scale, barWidth, 4f * scale);
            Fill(bar, new Color(0f, 0f, 0f, 0.5f * color.a));
            Fill(new Rect(bar.xMax - barWidth * remain, bar.y, barWidth * remain, bar.height), new Color(color.r, color.g, color.b, 0.9f * color.a));
        }

        static void Fill(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void DrawOutlined(Rect rect, string text, GUIStyle style, Color color, float outline)
        {
            Color prev = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.85f);
            for (int i = 0; i < 4; i++)
            {
                float ox = (i % 2 == 0 ? -1f : 1f) * outline;
                float oy = (i < 2 ? -1f : 1f) * outline;
                GUI.Label(new Rect(rect.x + ox, rect.y + oy, rect.width, rect.height), text, style);
            }
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
            style.normal.textColor = prev;
        }
    }
}
