using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// 検証用「攻撃フレームの可視化」(IMGUI)。直近の攻撃を 1 段 1 行の帯で表示する。
    ///  ・青 = 予備動作 / 赤 = 攻撃判定 / 灰 = 後隙
    ///  ・緑の下線 = 次の段を受け付ける区間(ヒット確定キャンセルで手前に伸びる)
    ///  ・水色の縦線 = 移動キャンセルの受付開始
    ///  ・黄色の縦線 = ボタンを押した瞬間 / 白い菱形 = 初めて当たった瞬間 / 白い縦線 = 現在位置
    /// キャンセルや先行入力の受付時間は「フレーム単位で調整できる仕組み」が無いと詰められないので、見える化して確認する。
    /// </summary>
    public class AttackTimelineView : MonoBehaviour
    {
        public PlayerCombat combat;
        [Tooltip("帯の右端が表す時間(秒)")]
        public float timeScale = 1.4f;
        [Tooltip("攻撃が終わってから表示を消すまでの時間(秒)")]
        public float holdTime = 2.5f;

        GUIStyle m_LabelStyle;
        GUIStyle m_SmallStyle;

        static readonly Color s_Windup = new Color(0.3f, 0.55f, 1f, 0.9f);
        static readonly Color s_Active = new Color(1f, 0.3f, 0.25f, 0.95f);
        static readonly Color s_Recovery = new Color(0.55f, 0.55f, 0.6f, 0.9f);
        static readonly Color s_Chain = new Color(0.35f, 1f, 0.45f, 1f);
        static readonly Color s_MoveCancel = new Color(0.4f, 0.9f, 1f, 1f);
        static readonly Color s_Press = new Color(1f, 0.9f, 0.2f, 1f);

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || combat == null || !HitFeelSettings.T.frameTimeline)
                return;
            var records = combat.Records;
            if (records.Count == 0)
                return;
            float fade = combat.IsAttacking ? 1f : 1f - Mathf.Clamp01((Time.unscaledTime - combat.LastAttackEndUnscaledTime - holdTime) / 0.5f);
            if (fade <= 0f)
                return;

            if (m_LabelStyle == null)
            {
                m_LabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleRight, clipping = TextClipping.Overflow };
                m_SmallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, clipping = TextClipping.Overflow };
            }

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenW = Screen.width / scale;
            float screenH = Screen.height / scale;

            const float labelWidth = 150f;
            const float rowHeight = 22f;
            float barWidth = Mathf.Min(620f, screenW - labelWidth - 80f);
            float x = (screenW - barWidth - labelWidth) * 0.5f + labelWidth;
            float y = screenH - 110f - rowHeight * records.Count;

            Color prevColor = GUI.color;
            Fill(new Rect(x - labelWidth - 10f, y - 26f, barWidth + labelWidth + 20f, rowHeight * records.Count + 46f), new Color(0f, 0f, 0f, 0.55f * fade));
            GUI.color = new Color(1f, 1f, 1f, fade);
            GUI.Label(new Rect(x - labelWidth, y - 24f, barWidth + labelWidth, 18f),
                "攻撃フレーム  青:予備動作  赤:判定  灰:後隙  緑:次段受付  水色:移動キャンセル  黄:入力  ◆:命中", m_SmallStyle);

            for (int i = 0; i < records.Count; i++)
            {
                PlayerCombat.AttackRecord r = records[i];
                AttackData a = r.attack;
                float rowY = y + i * rowHeight;
                float alpha = fade * (r.finished ? 0.75f : 1f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(x - labelWidth - 8f, rowY, labelWidth, rowHeight - 6f), a.ShortName + (r.chained ? " →" : ""), m_LabelStyle);

                float bar = rowHeight - 10f;
                float Px(float time) => x + Mathf.Clamp(time / timeScale, 0f, 1f) * barWidth;

                Fill(Rect.MinMaxRect(Px(0f), rowY + 2f, Px(a.windup), rowY + 2f + bar), s_Windup, alpha);
                Fill(Rect.MinMaxRect(Px(a.windup), rowY + 2f, Px(a.windup + a.active), rowY + 2f + bar), s_Active, alpha);
                Fill(Rect.MinMaxRect(Px(a.windup + a.active), rowY + 2f, Px(a.TotalDuration), rowY + 2f + bar), s_Recovery, alpha);
                if (r.chainTime >= 0f && r.chainTime < a.TotalDuration)
                    Fill(Rect.MinMaxRect(Px(r.chainTime), rowY + 3f + bar, Px(a.TotalDuration), rowY + 6f + bar), s_Chain, alpha);
                if (r.moveCancelTime >= 0f)
                    Fill(new Rect(Px(r.moveCancelTime) - 1f, rowY, 2f, bar + 4f), s_MoveCancel, alpha);
                foreach (float press in r.presses)
                    Fill(new Rect(Px(press) - 1.5f, rowY - 1f, 3f, bar + 6f), s_Press, alpha);
                if (r.hitTime >= 0f)
                    DrawDiamond(new Vector2(Px(r.hitTime), rowY + 2f + bar * 0.5f), 5f, alpha);
                Fill(new Rect(Px(r.elapsed) - 1f, rowY - 2f, 2f, bar + 8f), Color.white, alpha);
            }

            GUI.color = prevColor;
            GUI.matrix = prevMatrix;
        }

        static void Fill(Rect rect, Color color, float alpha = 1f)
        {
            Color prev = GUI.color;
            color.a *= alpha;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void DrawDiamond(Vector2 center, float size, float alpha)
        {
            Matrix4x4 prev = GUI.matrix;
            // GUI.matrix にスケールが掛かっているので、回転の中心はスケール後の座標で渡す
            Vector2 pivot = prev.MultiplyPoint3x4(center);
            GUIUtility.RotateAroundPivot(45f, pivot);
            Fill(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), Color.white, alpha);
            GUI.matrix = prev;
        }
    }
}
