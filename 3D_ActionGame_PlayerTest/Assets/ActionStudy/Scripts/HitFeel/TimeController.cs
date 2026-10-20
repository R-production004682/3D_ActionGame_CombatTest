using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// ヒットストップとスローモーション。Time.timeScale で画面全体の時間を操作する。
    /// ・ヒットストップ中も「実時間」で動くもの: カメラシェイク、モデルシェイク、ダメージフラッシュ、
    ///   画面効果、サウンド、ヒットエフェクト。
    /// ・止まるもの: キャラクターの移動、剣の振り、剣の軌跡、ノックバック。
    /// この「止まるもの / 止まらないもの」の分け方がヒットストップの気持ちよさの肝。
    /// </summary>
    public class TimeController : MonoBehaviour
    {
        public static TimeController Instance { get; private set; }

        [Tooltip("ヒットストップ中の時間の速さ。0 で完全停止、0.05 などにすると『ほぼ停止』になる")]
        [Range(0f, 0.3f)] public float hitStopTimeScale = 0f;

        float m_HitStopRemaining;
        float m_SlowDuration;
        float m_SlowRemaining;
        float m_SlowScale = 1f;
        float m_DefaultFixedDeltaTime;

        public bool InHitStop => m_HitStopRemaining > 0f;

        void Awake()
        {
            Instance = this;
            m_DefaultFixedDeltaTime = Time.fixedDeltaTime;
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = m_DefaultFixedDeltaTime;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void HitStop(float duration)
        {
            if (!HitFeelSettings.T.hitStop || duration <= 0f)
                return;
            m_HitStopRemaining = Mathf.Max(m_HitStopRemaining, duration);
            Apply();
        }

        /// <summary>ヒットストップ明けから始まるスローモーション。終わり際に滑らかに等速へ戻る。</summary>
        public void SlowMotion(float timeScale, float duration)
        {
            m_SlowScale = timeScale;
            m_SlowDuration = duration;
            m_SlowRemaining = duration;
            Apply();
        }

        public void CancelSlowMotion()
        {
            m_SlowRemaining = 0f;
            Apply();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (m_HitStopRemaining > 0f)
                m_HitStopRemaining -= dt;
            else if (m_SlowRemaining > 0f)
                m_SlowRemaining -= dt;
            Apply();
        }

        void Apply()
        {
            float scale = 1f;
            if (m_HitStopRemaining > 0f)
            {
                scale = hitStopTimeScale;
            }
            else if (m_SlowRemaining > 0f)
            {
                // 後半 40% で 1.0 に戻す
                float u = 1f - m_SlowRemaining / Mathf.Max(m_SlowDuration, 1e-4f);
                float recover = Mathf.InverseLerp(0.6f, 1f, u);
                scale = Mathf.Lerp(m_SlowScale, 1f, Ease.InQuad(recover));
            }

            Time.timeScale = scale;
            Time.fixedDeltaTime = m_DefaultFixedDeltaTime * Mathf.Max(scale, 0.05f);
        }
    }
}
