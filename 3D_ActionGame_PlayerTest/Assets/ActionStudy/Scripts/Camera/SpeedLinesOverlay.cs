using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// 追加演出「スピードライン」。プレイヤーの実際の移動速度に応じて、画面の外周に中心へ向かう流線を出す (IMGUI)。
    /// 全力疾走ではうっすら、攻撃の踏み込みのような瞬間的な高速移動ではくっきり出る。
    /// 背景に流れる物が少ない場所でも、相対的な速さが伝わる(カメラ演出とセットでスピード感を作る)。
    /// </summary>
    public class SpeedLinesOverlay : MonoBehaviour
    {
        [Tooltip("速さを測る対象(プレイヤー)")]
        public Transform target;

        [Tooltip("この速さ (m/s) から出始める")]
        public float minSpeed = 6.5f;
        [Tooltip("この速さ (m/s) で最大になる")]
        public float maxSpeed = 14f;
        public int lineCount = 36;
        [Tooltip("線を描き直す間隔(秒)。短いほどチラチラと流れて見える")]
        public float refreshInterval = 0.045f;
        [Tooltip("中心からどこまで空けるか (画面の対角の半分に対する割合)")]
        [Range(0.3f, 0.95f)] public float innerRadius = 0.62f;
        public Color color = new Color(1f, 1f, 1f, 0.55f);

        Vector3 m_PrevPosition;
        float m_Intensity;
        float m_NextRefresh;
        float[] m_Angles;
        float[] m_Lengths;
        float[] m_Widths;

        void Start()
        {
            if (target != null)
                m_PrevPosition = target.position;
            m_Angles = new float[lineCount];
            m_Lengths = new float[lineCount];
            m_Widths = new float[lineCount];
            Randomize();
        }

        void LateUpdate()
        {
            if (target == null)
                return;
            float dt = Time.deltaTime;
            Vector3 position = target.position;
            if (dt > 0f)
            {
                float speed = Ease.Flat(position - m_PrevPosition).magnitude / dt;
                float goal = HitFeelSettings.T.speedLines ? Mathf.InverseLerp(minSpeed, maxSpeed, speed) : 0f;
                // すぐ出て、ゆっくり消える
                float sharpness = goal > m_Intensity ? 30f : 6f;
                m_Intensity = Mathf.Lerp(m_Intensity, goal, 1f - Mathf.Exp(-sharpness * dt));
            }
            else if (!HitFeelSettings.T.speedLines)
            {
                m_Intensity = 0f;
            }
            m_PrevPosition = position;

            // ヒットストップ中は線も止める(ゲーム時間で更新)
            if (Time.time >= m_NextRefresh)
            {
                m_NextRefresh = Time.time + refreshInterval;
                Randomize();
            }
        }

        void Randomize()
        {
            for (int i = 0; i < m_Angles.Length; i++)
            {
                m_Angles[i] = (i + Random.value) / m_Angles.Length * 360f;
                m_Lengths[i] = Random.Range(0.25f, 1f);
                m_Widths[i] = Random.Range(1f, 3f);
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || m_Intensity < 0.02f || m_Angles == null)
                return;

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float halfDiagonal = center.magnitude;
            float scale = Screen.height / 1080f;
            float inner = halfDiagonal * Mathf.Lerp(innerRadius + 0.15f, innerRadius, m_Intensity);

            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = GUI.matrix;
            for (int i = 0; i < m_Angles.Length; i++)
            {
                // 弱い時は一部の線だけ出す
                if (m_Lengths[i] > m_Intensity + 0.25f)
                    continue;
                Color c = color;
                c.a *= m_Intensity * Mathf.Lerp(0.5f, 1f, m_Lengths[i]);
                GUI.color = c;
                GUI.matrix = prevMatrix;
                GUIUtility.RotateAroundPivot(m_Angles[i], center);
                float length = halfDiagonal * 0.6f * m_Lengths[i] * Mathf.Lerp(0.5f, 1f, m_Intensity);
                float width = m_Widths[i] * scale;
                GUI.DrawTexture(new Rect(center.x + inner, center.y - width * 0.5f, length, width), Texture2D.whiteTexture);
            }
            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }
    }
}
