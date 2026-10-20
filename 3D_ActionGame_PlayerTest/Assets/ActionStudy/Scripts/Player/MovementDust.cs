using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// 追加演出「足元の砂煙」。PlayerController の状態を見て、地面を強く蹴った瞬間に土煙を上げる。
    ///  ・走り出し  … 進む方向と逆へ蹴り出す(「走り出しはグッと」を見た目でも補強する)
    ///  ・切り返し  … 慣性で滑っている方向へ大きめに
    ///  ・急停止    … 進んでいた方向へ(ブレーキの踏ん張り)
    ///  ・全力疾走  … 1 歩ごとに小さく(背景が流れなくても速さが伝わる)
    /// </summary>
    public class MovementDust : MonoBehaviour
    {
        public PlayerController controller;
        [Tooltip("未設定ならシーンから探す")]
        public HitSparkSystem sparks;

        [Header("強さ (0-1)")]
        public float startStrength = 0.7f;
        public float quickTurnStrength = 1f;
        public float stopStrength = 0.8f;
        public float footstepStrength = 0.2f;
        [Tooltip("1 歩ごとの砂煙を出し始める速さ (最高速に対する割合)")]
        public float footstepSpeed01 = 0.9f;

        float m_PrevSpeed01;
        float m_PrevInput;
        bool m_PrevQuickTurn;
        float m_PrevStridePhase;
        float m_LastEmitTime = -10f;

        void Start()
        {
            if (controller == null)
                controller = GetComponent<PlayerController>();
            if (sparks == null)
                sparks = FindFirstObjectByType<HitSparkSystem>();
        }

        void LateUpdate()
        {
            if (controller == null || sparks == null || Time.deltaTime <= 0f)
                return;

            float speed01 = controller.Speed01;
            Vector3 velocity = controller.PlanarVelocity;
            Vector3 inputDir = controller.WorldInputDirection;
            float input = controller.MovementLocked ? 0f : inputDir.magnitude;
            bool quickTurn = controller.IsQuickTurning;
            float stride = controller.StridePhase;
            Vector3 feet = transform.position;

            if (HitFeelSettings.T.moveDust)
            {
                if (quickTurn && !m_PrevQuickTurn && speed01 > 0.4f)
                    Emit(feet, velocity, quickTurnStrength * speed01);
                else if (m_PrevSpeed01 < 0.15f && input > 0.6f && speed01 > m_PrevSpeed01)
                    Emit(feet - inputDir.normalized * 0.2f, -inputDir, startStrength);
                else if (m_PrevInput > 0.5f && input < 0.1f && speed01 > 0.6f)
                    Emit(feet + velocity.normalized * 0.3f, velocity, stopStrength * speed01);
                else if (speed01 >= footstepSpeed01 && Mathf.FloorToInt(stride / Mathf.PI) != Mathf.FloorToInt(m_PrevStridePhase / Mathf.PI))
                    Emit(feet - velocity.normalized * 0.15f, -velocity, footstepStrength);
            }

            m_PrevSpeed01 = speed01;
            m_PrevInput = input;
            m_PrevQuickTurn = quickTurn;
            m_PrevStridePhase = stride;
        }

        void Emit(Vector3 position, Vector3 direction, float strength)
        {
            // 同じ瞬間に何度も出さない
            if (Time.time - m_LastEmitTime < 0.05f)
                return;
            m_LastEmitTime = Time.time;
            sparks.EmitMoveDust(position, direction, Mathf.Clamp01(strength));
        }
    }
}
