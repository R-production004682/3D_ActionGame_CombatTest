using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// ロックオン。Q / マウスホイールクリック / 右スティック押し込みで、画面の中央に近い敵に狙いを固定する(もう一度押すと解除)。
    ///  ・ロック中はカメラが敵の方向を向き続ける(ThirdPersonCamera が LockedTarget を見る)。移動は敵の周りを回り込む動きになる
    ///  ・攻撃は入力方向に関係なく、ロック中の敵へ向き直って踏み込む(PlayerCombat が LockedTarget を見る)
    ///  ・ロック中にホイールを回す / 右スティックを左右に倒すと、その方向の敵へ切り替える
    ///  ・敵を倒した / 離れすぎた / パネルで OFF にした時は自動で解除する
    /// 敵の体の中心にロックオンマーカーを描く (IMGUI)。
    /// シーンに置いていなくても、PlayerCombat が実行時に自動で追加する(インスペクターで調整したい時は Player に追加しておく)。
    /// </summary>
    public class PlayerLockOn : MonoBehaviour
    {
        public PlayerInputReader input;

        [Header("対象の選び方")]
        [Tooltip("ロックオンできる距離 (m)")]
        public float lockRange = 18f;
        [Tooltip("この距離より離れたら解除する (m)")]
        public float releaseRange = 24f;
        [Tooltip("カメラの正面からこの角度以内の敵を候補にする")]
        public float lockAngle = 75f;
        [Tooltip("右スティックをこの量以上倒したら対象を切り替える")]
        public float switchStickThreshold = 0.8f;

        [Header("マーカー")]
        public Color markerColor = new Color(1f, 0.62f, 0.3f, 1f);
        [Tooltip("マーカーの大きさ (px, 1080p 基準)")]
        public float markerSize = 46f;

        Enemy m_Target;
        float m_LockTime = -10f;
        bool m_StickReady = true;

        /// <summary>ロック中の敵。いなければ null。</summary>
        public Enemy LockedTarget => m_Target != null && !m_Target.IsDead ? m_Target : null;

        void Start()
        {
            if (input == null)
                input = GetComponent<PlayerInputReader>();
        }

        void Update()
        {
            if (!HitFeelSettings.T.lockOn)
            {
                m_Target = null;
                return;
            }

            // 倒した・離れすぎたら解除
            if (m_Target != null && (m_Target.IsDead || Ease.Flat(m_Target.transform.position - transform.position).magnitude > releaseRange))
                m_Target = null;

            if (input == null)
                return;

            if (input.LockOnPressed)
            {
                if (m_Target != null)
                    m_Target = null;
                else
                    Lock(FindBestTarget());
            }

            if (m_Target != null)
                UpdateSwitch();
        }

        void Lock(Enemy enemy)
        {
            m_Target = enemy;
            if (enemy != null)
                m_LockTime = Time.unscaledTime;
        }

        /// <summary>カメラの正面に近く、近い敵を選ぶ。</summary>
        Enemy FindBestTarget()
        {
            Vector3 forward = ThirdPersonCamera.Instance != null
                ? Ease.Flat(ThirdPersonCamera.Instance.transform.forward)
                : transform.forward;
            Enemy best = null;
            float bestScore = float.MaxValue;
            foreach (Enemy enemy in Enemy.All)
            {
                if (enemy.IsDead)
                    continue;
                Vector3 to = Ease.Flat(enemy.transform.position - transform.position);
                float distance = to.magnitude;
                if (distance > lockRange)
                    continue;
                float angle = distance > 0.01f ? Vector3.Angle(forward, to) : 0f;
                if (angle > lockAngle)
                    continue;
                float score = angle + distance * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }
            return best;
        }

        /// <summary>ホイール / 右スティックの左右で、隣の敵へ切り替える。</summary>
        void UpdateSwitch()
        {
            int side = 0;
            float stickX = input.LookStick.x;
            if (m_StickReady && Mathf.Abs(stickX) >= switchStickThreshold)
            {
                side = stickX > 0f ? 1 : -1;
                m_StickReady = false;
            }
            else if (Mathf.Abs(stickX) < 0.3f)
            {
                m_StickReady = true;
            }

            float scroll = input.LockSwitchScroll;
            if (side == 0 && Mathf.Abs(scroll) > 0.01f)
                side = scroll > 0f ? 1 : -1;

            if (side != 0)
            {
                Enemy next = FindNeighbor(side);
                if (next != null)
                    Lock(next);
            }
        }

        /// <summary>今の対象から見て右 (side = 1) / 左 (side = -1) で、角度が一番近い敵。</summary>
        Enemy FindNeighbor(int side)
        {
            Vector3 current = Ease.Flat(m_Target.transform.position - transform.position);
            Enemy best = null;
            float bestAngle = float.MaxValue;
            foreach (Enemy enemy in Enemy.All)
            {
                if (enemy == m_Target || enemy.IsDead)
                    continue;
                Vector3 to = Ease.Flat(enemy.transform.position - transform.position);
                if (to.magnitude > lockRange)
                    continue;
                // 上から見て時計回り(右)が正
                float angle = Vector3.SignedAngle(current, to, Vector3.up);
                if (Mathf.Sign(angle) != side || Mathf.Abs(angle) < 1f)
                    continue;
                if (Mathf.Abs(angle) < bestAngle)
                {
                    bestAngle = Mathf.Abs(angle);
                    best = enemy;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------
        // マーカー
        // ------------------------------------------------------------------

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !HitFeelSettings.T.lockOn)
                return;
            Enemy enemy = LockedTarget;
            Camera cam = Camera.main;
            if (enemy == null || cam == null)
                return;
            Vector3 screen = cam.WorldToScreenPoint(enemy.CenterPosition);
            if (screen.z <= 0f)
                return;

            GUI.depth = 5;
            Matrix4x4 prevMatrix = GUI.matrix;
            Color prevColor = GUI.color;
            GUI.matrix = Matrix4x4.identity;

            float scale = Screen.height / 1080f;
            float age = Time.unscaledTime - m_LockTime;
            // ロックした瞬間は大きく出て、すっと締まる
            float pop = age < 0.18f ? Mathf.Lerp(1.9f, 1f, Ease.OutCubic(age / 0.18f)) : 1f;
            float breathe = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f);
            float half = markerSize * scale * pop * breathe;
            float length = half * 0.55f;
            float thickness = Mathf.Max(2f, 4f * scale);
            float spin = Time.unscaledTime * 35f;
            var center = new Vector2(screen.x, Screen.height - screen.y);
            float alpha = Mathf.Clamp01(age / 0.1f);

            // 暗い縁取り → 本体の順に、4 隅のカギ括弧を描く
            DrawBrackets(center, half, length, thickness + 2f * scale, spin, new Color(0f, 0f, 0f, 0.45f * alpha));
            DrawBrackets(center, half, length, thickness, spin, new Color(markerColor.r, markerColor.g, markerColor.b, alpha));

            // 中心の小さなひし形
            float dot = 8f * scale;
            GUI.matrix = Matrix4x4.identity;
            GUIUtility.RotateAroundPivot(45f, center);
            GUI.color = new Color(markerColor.r, markerColor.g, markerColor.b, alpha);
            GUI.DrawTexture(new Rect(center.x - dot * 0.5f, center.y - dot * 0.5f, dot, dot), Texture2D.whiteTexture);

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        static void DrawBrackets(Vector2 center, float half, float length, float thickness, float spin, Color color)
        {
            GUI.color = color;
            for (int i = 0; i < 4; i++)
            {
                GUI.matrix = Matrix4x4.identity;
                GUIUtility.RotateAroundPivot(spin + 90f * i, center);
                // 左上の隅の L 字(回転して 4 隅に配る)
                GUI.DrawTexture(new Rect(center.x - half, center.y - half, length, thickness), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x - half, center.y - half, thickness, length), Texture2D.whiteTexture);
            }
        }
    }
}
