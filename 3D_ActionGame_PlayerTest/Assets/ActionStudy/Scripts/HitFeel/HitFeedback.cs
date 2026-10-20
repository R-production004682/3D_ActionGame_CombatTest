using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ActionStudy
{
    /// <summary>
    /// ヒット演出の司令塔。攻撃が当たると PlayerCombat から OnHit が呼ばれ、各演出へ振り分ける。
    ///
    ///  ・敵ごとに出すもの: 火花エフェクト (敵側のリアクションは Enemy.TakeHit で処理)
    ///  ・1 フレームに 1 回だけ出すもの: ヒットストップ / カメラ / 画面効果 / SE / 振動
    ///    (複数の敵を同時に斬った時に演出が重複して過剰にならないようにする)
    ///
    /// 超強用に「溜め」「キラッ」「地面への叩きつけ」のイベントも持つ。
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        public static HitFeedback Instance { get; private set; }

        /// <summary>命中のたびに呼ばれる(HP ゲージ・ダメージ数値・コンボカウンターなど UI 側が購読する)。</summary>
        public static event Action<HitInfo> HitLanded;

        [Header("参照")]
        public HitSparkSystem sparks;
        public HitAudio hitAudio;
        public TimeController timeController;
        public ThirdPersonCamera cameraRig;
        public ScreenFxController screenFx;

        [Header("撃破スロー (最強)")]
        [Range(0.05f, 1f)] public float finisherSlowTimeScale = 0.2f;
        public float finisherSlowDuration = 0.7f;

        [Header("叩きつけが空振りした時")]
        public float whiffSlamHitStop = 0.07f;
        public float whiffSlamShake = 0.55f;

        int m_LastGlobalFrame = -1;
        float m_RumbleEndTime;
        bool m_Rumbling;

        /// <summary>デバッグ表示用: 最後のヒットの情報。</summary>
        public string LastHitDescription { get; private set; } = "-";

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnDisable()
        {
            StopRumble();
        }

        public void OnSwing(AttackData attack)
        {
            if (hitAudio != null)
                hitAudio.PlaySwing(attack.tier);
        }

        // ------------------------------------------------------------------
        // 溜め (超強)
        // ------------------------------------------------------------------

        public void OnChargeStart(AttackData attack)
        {
            if (hitAudio != null)
                hitAudio.PlayCharge(attack.windup);
        }

        /// <param name="progress">溜めの進み具合 0-1</param>
        public void OnCharge(float progress, Vector3 bladeBase, Vector3 bladeTip)
        {
            if (sparks != null)
                sparks.EmitCharge(bladeBase, bladeTip, progress);
            if (cameraRig != null)
                cameraRig.SetCharge(progress);
            if (screenFx != null)
                screenFx.SetCharge(progress);
            if (HitFeelSettings.T.forceFeedback)
                StartRumble(0.25f * progress, 0.05f * progress, 0.1f);
        }

        public void OnGlint(Vector3 bladeTip)
        {
            if (sparks != null)
                sparks.EmitGlint(bladeTip);
            if (hitAudio != null)
                hitAudio.PlayGlint();
        }

        public void OnChargeEnd()
        {
            if (cameraRig != null)
                cameraRig.SetCharge(0f);
            if (screenFx != null)
                screenFx.SetCharge(0f);
        }

        // ------------------------------------------------------------------
        // 叩きつけ (超強)
        // ------------------------------------------------------------------

        /// <summary>地面を叩いた瞬間。敵に当たらなくても地面の衝撃は出す。</summary>
        public void OnGroundSlam(Vector3 point, AttackData a, int hitCount)
        {
            HitFeelToggles t = HitFeelSettings.T;
            bool hit = hitCount > 0;

            if (t.hitEffect && sparks != null)
                sparks.PlayGroundSlam(point, a.groundShockwaveRadius);

            if (Time.frameCount == m_LastGlobalFrame)
                return;
            m_LastGlobalFrame = Time.frameCount;

            if (timeController != null)
                timeController.HitStop(hit ? a.hitStopDuration : whiffSlamHitStop);
            if (cameraRig != null)
            {
                // 叩きつけは縦の衝撃なので、カメラも縦に揺らす
                cameraRig.AddShake(hit ? a.cameraShake : whiffSlamShake, Vector3.down);
                cameraRig.FovPunch(a.fovPunch);
            }
            if (screenFx != null)
            {
                screenFx.Pulse(a.screenFxStrength, point, a.tier);
                screenFx.ImpactFrame();
            }
            if (t.hitSE && hitAudio != null)
                hitAudio.PlaySlam(hit);
            if (t.forceFeedback)
                StartRumble(a.rumble.x, a.rumble.y, a.rumble.z);

            LastHitDescription = hit
                ? $"{a.name} ×{hitCount}  / ヒットストップ {a.hitStopDuration * 1000f:0}ms ({a.hitStopDuration * 60f:0.#}F)"
                : $"{a.name} (空振り)";
        }

        // ------------------------------------------------------------------
        // 命中
        // ------------------------------------------------------------------

        public void OnHit(HitInfo info)
        {
            HitFeelToggles t = HitFeelSettings.T;
            AttackData a = info.attack;

            if (sparks != null)
                sparks.Play(info);
            HitLanded?.Invoke(info);

            if (Time.frameCount != m_LastGlobalFrame)
            {
                m_LastGlobalFrame = Time.frameCount;

                if (timeController != null)
                    timeController.HitStop(a.hitStopDuration);

                if (cameraRig != null)
                {
                    cameraRig.AddShake(a.cameraShake, info.slashDirection);
                    if (a.fovPunch != 0f)
                        cameraRig.FovPunch(a.fovPunch);
                }

                if (screenFx != null)
                    screenFx.Pulse(a.screenFxStrength, info.point, a.tier);

                if (t.hitSE && hitAudio != null)
                    hitAudio.PlayHit(a.tier);

                if (t.forceFeedback)
                    StartRumble(a.rumble.x, a.rumble.y, a.rumble.z);

                LastHitDescription = $"{a.name}  / ヒットストップ {a.hitStopDuration * 1000f:0}ms ({a.hitStopDuration * 60f:0.#}F)";
            }

            if (!t.hitStop || timeController == null)
                return;
            // 【3】スローモー: 超強は当たれば常に、最強は倒した時だけ
            if (a.slowMotionScale > 0f)
                timeController.SlowMotion(a.slowMotionScale, a.slowMotionDuration);
            else if (info.killed && a.tier == AttackTier.Finisher)
                timeController.SlowMotion(finisherSlowTimeScale, finisherSlowDuration);
        }

        void StartRumble(float low, float high, float duration)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null)
                return;
            pad.SetMotorSpeeds(low, high);
            m_Rumbling = true;
            m_RumbleEndTime = Time.unscaledTime + duration;
        }

        void StopRumble()
        {
            if (!m_Rumbling)
                return;
            m_Rumbling = false;
            Gamepad pad = Gamepad.current;
            if (pad != null)
                pad.SetMotorSpeeds(0f, 0f);
        }

        void Update()
        {
            if (m_Rumbling && Time.unscaledTime >= m_RumbleEndTime)
                StopRumble();
        }
    }
}
