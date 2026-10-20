using System.Collections.Generic;
using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// 弱・弱・強・最強 の 4 段コンボ。アニメーションは使わず、剣の握り(swordPivot)を数式で回して振りを作る。
    /// 1 段は「予備動作 → 攻撃判定 → 後隙」の 3 フェーズで、各フェーズのイージングが手触りを決める。
    /// 攻撃判定は剣の根元〜剣先のカプセルを 1 フレーム内で細かく補間して取り、速い振りでもすり抜けない。
    ///
    /// 操作を気持ちよくする工夫(HitFeelSettings の「操作の手触り」で個別に OFF にできる):
    ///  ・先行入力 … 攻撃中に押したボタンを覚えておき、繋げられる瞬間に次段を出す
    ///  ・ヒット確定キャンセル … 当たった時だけ次段への受付が早まる。当て続けるとコンボのリズムが速くなる
    ///  ・吸い付き + 踏み込み … 近くの敵へ向き直り、ちょうど届く距離まで踏み込む
    ///  ・移動キャンセル … 後隙の後半はスティック入力ですぐ走り出せる
    ///  ・スクワッシュ & ストレッチ … 振りかぶりで縮み、振り抜きで伸びる(体の溜めと解放)
    ///  ・剣の揺れ追従 … 待機・移動中の剣が体の動きに遅れてついてくる(二次モーション)
    ///
    /// 追加演出: 攻撃側の震え … ヒットストップ中は自分の剣も小刻みに震え、刃が食い込んだ抵抗感を出す
    ///
    /// 超強(5 段目)は「溜め → キラッ → 地面へ叩きつけ → 衝撃波で周囲をまとめて打ち上げ」。
    /// 長い溜めの間に光の収束・暗転・カメラの寄りで期待感を高め、叩きつけで一気に解放する。
    ///
    /// PlayerController より先に実行する(攻撃開始のフレームに移動が 1 フレーム混ざらないように)。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("参照")]
        public PlayerController controller;
        public PlayerInputReader input;
        [Tooltip("回転斬りで体ごと回すためのルート")]
        public Transform spinRoot;
        [Tooltip("スクワッシュ & ストレッチを掛ける体。未設定なら SpinRoot/Body を探す")]
        public Transform body;
        [Tooltip("剣の握り。ここを回して剣を振る")]
        public Transform swordPivot;
        public Transform bladeBase;
        public Transform bladeTip;
        public SwordTrail trail;
        [Tooltip("未設定なら同じ GameObject から探し、無ければ実行時に追加する")]
        public PlayerLockOn lockOn;

        [Header("コンボ")]
        public AttackData[] combo = AttackData.CreateDefaultCombo();
        [Tooltip("攻撃が終わってからこの時間内に押せば次の段から続く")]
        public float comboKeepTime = 0.3f;
        [Tooltip("先行入力を覚えておく時間(秒, 実時間)")]
        public float inputBufferTime = 0.35f;

        [Header("待機姿勢")]
        public Vector3 idleHandPosition = new Vector3(0.45f, 1.0f, 0.1f);
        public Vector3 idleSwordEuler = new Vector3(40f, 20f, 0f);
        [Tooltip("予備動作で通常より余分に振りかぶる角度")]
        public float anticipationOvershoot = 20f;

        [Header("剣の揺れ追従 (二次モーション)")]
        [Tooltip("旋回角速度に対して剣が遅れる量")]
        public float swordLagPerYawRate = 0.025f;
        [Tooltip("加速度に対して剣が遅れる量")]
        public float swordLagPerAcceleration = 0.6f;
        [Tooltip("走りに合わせて剣が揺れる角度")]
        public float swordRunSwing = 6f;
        public float swordLagSmoothTime = 0.09f;

        [Header("スクワッシュ & ストレッチ")]
        public float squashStiffness = 320f;
        public float squashDamping = 16f;
        [Tooltip("振りかぶりで縮む勢い")]
        public float windupSquashImpulse = -2.2f;
        [Tooltip("振り抜きで伸びる勢い")]
        public float releaseStretchImpulse = 3.2f;

        [Header("攻撃判定")]
        public float bladeRadius = 0.2f;
        public LayerMask hitMask = ~0;
        [Tooltip("攻撃判定期間を何分割して当たりを取るか")]
        public int sweepSubSteps = 32;

        [Header("攻撃の吸い付き")]
        public float assistRange = 5.5f;
        [Tooltip("入力方向からこの角度以内の敵を対象にする")]
        public float assistAngle = 80f;
        [Tooltip("敵の手前で止まる距離")]
        public float assistStopDistance = 1.3f;
        public float assistMaxLunge = 3.5f;

        [Header("自分へのノックバック速度")]
        public float heavyRecoilSpeed = 2.5f;
        public float finisherRecoilSpeed = 4f;

        [Header("攻撃側の震え (ヒットストップ中)")]
        public float attackerShakeAmplitude = 0.018f;
        public float attackerShakeFrequency = 34f;

        [Header("検証用")]
        [Tooltip("「先行入力が長すぎる」悪い例で使う受付時間(秒)")]
        public float longInputBufferTime = 1f;

        /// <summary>攻撃フレームの可視化用: 1 段分の記録。</summary>
        public class AttackRecord
        {
            public AttackData attack;
            /// <summary>攻撃開始からの経過時間(秒, ゲーム時間)。</summary>
            public float elapsed;
            /// <summary>次の段を受け付け始める時間。負の値なら次の段は無い。</summary>
            public float chainTime;
            /// <summary>移動キャンセルを受け付け始める時間。負の値なら無効。</summary>
            public float moveCancelTime;
            /// <summary>初めて当たった時間。当たっていなければ負の値。</summary>
            public float hitTime = -1f;
            /// <summary>この段の最中にボタンを押した時間。</summary>
            public readonly List<float> presses = new List<float>();
            public bool chained;
            public bool finished;
        }

        const int k_MaxRecords = 5;
        readonly List<AttackRecord> m_Records = new List<AttackRecord>();
        AttackRecord m_Record;

        readonly HashSet<Enemy> m_HitThisSwing = new HashSet<Enemy>();
        readonly List<HitInfo> m_PendingHits = new List<HitInfo>();
        readonly Collider[] m_Overlap = new Collider[16];

        AttackData m_Attack;
        int m_Index = -1;
        int m_NextComboIndex;
        bool m_Attacking;
        float m_Time;
        float m_LastAttackEndTime = -10f;
        float m_BufferedPressTime = -10f;
        bool m_SwingSoundPlayed;
        bool m_ReleasePlayed;
        bool m_HitConfirmed;
        bool m_Charging;
        bool m_GlintPlayed;
        bool m_SlamDone;

        Quaternion m_StartSwordRotation;
        Vector3 m_StartHandPosition;
        float m_StartYaw;
        float m_TargetYaw;
        Vector3 m_LungeDirection;
        float m_LungeDistance;
        Enemy m_Target;

        Vector3 m_SwordLag;
        Vector3 m_SwordLagVelocity;
        float m_Squash;
        float m_SquashVelocity;
        Vector3 m_BodyBaseScale = Vector3.one;

        public bool IsAttacking => m_Attacking;
        public int ComboIndex => m_Attacking ? m_Index : -1;
        public AttackData CurrentAttack => m_Attacking ? m_Attack : null;
        /// <summary>攻撃中に狙っている敵 (カメラのフレーミングに使う)。</summary>
        public Enemy CurrentTarget => m_Attacking && m_Target != null && !m_Target.IsDead ? m_Target : null;
        /// <summary>攻撃開始からの経過時間(秒)。</summary>
        public float AttackTime => m_Time;
        /// <summary>直近の攻撃の記録(古い順)。攻撃フレームの可視化に使う。</summary>
        public IReadOnlyList<AttackRecord> Records => m_Records;
        /// <summary>最後に攻撃が終わった時刻 (Time.unscaledTime)。</summary>
        public float LastAttackEndUnscaledTime { get; private set; } = -10f;

        Quaternion IdleRotation => Quaternion.Euler(idleSwordEuler);

        void Awake()
        {
            UpgradeCombo();
            // ロックオンはシーンに置いていなくても使えるよう、無ければ追加する
            if (lockOn == null)
                lockOn = GetComponent<PlayerLockOn>();
            if (lockOn == null)
                lockOn = gameObject.AddComponent<PlayerLockOn>();
        }

        void OnValidate()
        {
            UpgradeCombo();
        }

        /// <summary>
        /// コンボの既定値を変えた時に上げる番号。シーンに保存された番号がこれより古ければ、
        /// コンボを最新の既定値(弱→弱→強→最強→超強)に置き換える。
        /// (コンボはシーンに保存されるので、スクリプトの既定値を変えただけでは古いシーンに反映されないため)
        /// </summary>
        const int k_ComboVersion = 3;

        [SerializeField, HideInInspector]
        int m_ComboVersion;

        void UpgradeCombo()
        {
            if (m_ComboVersion >= k_ComboVersion && combo != null && combo.Length > 0)
                return;
            if (m_ComboVersion > 0 || (combo != null && combo.Length > 0 && combo[0].comboWindowStart < 0.2f))
                Debug.Log("[ActionStudy] 古いコンボ設定を検出したため、最新の既定値 (弱→弱→強→最強→超強) に更新しました。", this);
            combo = AttackData.CreateDefaultCombo();
            m_ComboVersion = k_ComboVersion;
        }

        void Start()
        {
            if (body == null && spinRoot != null)
                body = spinRoot.Find("Body");
            if (body != null)
                m_BodyBaseScale = body.localScale;
            if (swordPivot != null)
            {
                swordPivot.localPosition = idleHandPosition;
                swordPivot.localRotation = IdleRotation;
            }
        }

        void Update()
        {
            HitFeelToggles t = HitFeelSettings.T;
            bool pressedNow = input != null && input.AttackPressed;
            if (pressedNow)
            {
                m_BufferedPressTime = Time.unscaledTime;
                if (m_Attacking && m_Record != null)
                    m_Record.presses.Add(m_Time);
            }

            // 先行入力: 少し前に押した入力も有効。OFF ならそのフレームに押した入力だけ
            float bufferTime = t.longInputBuffer ? longInputBufferTime : t.inputBuffer ? inputBufferTime : 0f;
            bool hasPress = pressedNow || Time.unscaledTime - m_BufferedPressTime <= bufferTime;

            if (!m_Attacking)
            {
                if (hasPress)
                {
                    bool continueCombo = Time.time - m_LastAttackEndTime <= comboKeepTime;
                    StartAttack(continueCombo ? m_NextComboIndex : 0);
                }
                else
                {
                    RelaxToIdle(Time.deltaTime);
                }
            }
            else
            {
                TickAttack(hasPress);
                if (m_Attacking)
                    ApplyAttackerShake(t);
            }

            UpdateSquash(Time.deltaTime);

            if (trail != null)
                trail.Emitting = t.swordTrail && IsTrailWindow();
        }

        /// <summary>攻撃側の震え: ヒットストップ中だけ剣を小刻みに揺らす(実時間で動かす)。</summary>
        void ApplyAttackerShake(HitFeelToggles t)
        {
            if (!t.attackerShake || !m_HitConfirmed || TimeController.Instance == null || !TimeController.Instance.InHitStop)
                return;
            float scale = TierSquashScale(m_Attack.tier);
            float phase = Time.unscaledTime * attackerShakeFrequency * Mathf.PI * 2f;
            Vector3 jitter = new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.37f + 1f) * 0.6f, 0f) * (attackerShakeAmplitude * scale);
            swordPivot.localPosition += jitter;
            swordPivot.localRotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.9f) * 2.5f * scale);
        }

        // ------------------------------------------------------------------
        // 攻撃の進行
        // ------------------------------------------------------------------

        void StartAttack(int index)
        {
            m_Index = Mathf.Clamp(index, 0, combo.Length - 1);
            m_Attack = combo[m_Index];
            m_Attacking = true;
            m_Time = 0f;
            m_SwingSoundPlayed = false;
            m_ReleasePlayed = false;
            m_HitConfirmed = false;
            m_GlintPlayed = false;
            m_SlamDone = false;
            m_BufferedPressTime = -10f;
            m_HitThisSwing.Clear();
            controller.MovementLocked = true;

            if (m_Record != null)
                m_Record.finished = true;
            m_Record = new AttackRecord { attack = m_Attack, chainTime = m_Attack.comboWindowStart, moveCancelTime = -1f };
            m_Records.Add(m_Record);
            if (m_Records.Count > k_MaxRecords)
                m_Records.RemoveAt(0);

            m_StartSwordRotation = swordPivot.localRotation;
            m_StartHandPosition = swordPivot.localPosition;

            // 向きと踏み込み距離を決める
            Vector3 inputDir = controller.WorldInputDirection;
            Vector3 dir = inputDir.sqrMagnitude > 0.04f ? inputDir.normalized : transform.forward;
            float lunge = m_Attack.lungeDistance;

            // ロック中はその敵を最優先。ロックしていなければ吸い付き(ON の時)で近くの敵を選ぶ
            Enemy locked = lockOn != null ? lockOn.LockedTarget : null;
            if (locked != null)
                m_Target = locked;
            else
                m_Target = HitFeelSettings.T.attackAssist ? FindAssistTarget(dir) : null;
            if (m_Target != null)
            {
                Vector3 toTarget = Ease.Flat(m_Target.transform.position - transform.position);
                dir = toTarget.normalized;
                lunge = Mathf.Clamp(toTarget.magnitude - assistStopDistance, 0f, assistMaxLunge);
            }

            m_LungeDirection = dir;
            m_LungeDistance = lunge;
            m_StartYaw = controller.Yaw;
            m_TargetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            // 振りかぶりで体を縮める(溜め)
            m_SquashVelocity += windupSquashImpulse * TierSquashScale(m_Attack.tier);

            if (m_Attack.chargeEffect)
            {
                // 直前の撃破スローが残っていると溜めが間延びするので、溜めは等速で見せる
                if (TimeController.Instance != null)
                    TimeController.Instance.CancelSlowMotion();
                m_Charging = true;
                if (HitFeedback.Instance != null)
                    HitFeedback.Instance.OnChargeStart(m_Attack);
            }
        }

        static float TierSquashScale(AttackTier tier)
        {
            switch (tier)
            {
                case AttackTier.Light: return 0.7f;
                case AttackTier.Ultra: return 1.7f;
                default: return 1f;
            }
        }

        void StopCharge()
        {
            if (!m_Charging)
                return;
            m_Charging = false;
            if (HitFeedback.Instance != null)
                HitFeedback.Instance.OnChargeEnd();
        }

        void TickAttack(bool hasPress)
        {
            float prevTime = m_Time;
            m_Time += Time.deltaTime;
            AttackData a = m_Attack;
            HitFeelToggles t = HitFeelSettings.T;

            // 次の段へのキャンセル。当たっていれば攻撃判定が終わった時点で受付開始(ヒット確定キャンセル)
            float chainTime = m_HitConfirmed && t.hitConfirmCancel ? Mathf.Min(a.comboWindowStart, a.windup + a.active) : a.comboWindowStart;
            bool hasNext = m_Index < combo.Length - 1;
            bool canChain = hasNext && m_Time >= chainTime;
            m_Record.elapsed = m_Time;
            m_Record.chainTime = hasNext ? chainTime : -1f;
            m_Record.moveCancelTime = t.moveCancel ? a.windup + a.active + a.recovery * 0.5f : -1f;
            if (canChain && hasPress)
            {
                m_Record.chained = true;
                StartAttack(m_Index + 1);
                ApplyPose(0f);
                return;
            }

            if (!m_SwingSoundPlayed && m_Time >= a.windup - 0.03f)
            {
                m_SwingSoundPlayed = true;
                if (HitFeedback.Instance != null)
                    HitFeedback.Instance.OnSwing(a);
            }

            if (m_Charging && HitFeedback.Instance != null)
            {
                // 溜め: 光の収束・暗転・カメラの寄りを溜めの進み具合に合わせて強める
                HitFeedback.Instance.OnCharge(Mathf.Clamp01(m_Time / a.windup), bladeBase.position, bladeTip.position);

                // 振り下ろす直前の「キラッ」。ここから来る、という合図
                if (!m_GlintPlayed && m_Time >= a.windup - 0.08f)
                {
                    m_GlintPlayed = true;
                    HitFeedback.Instance.OnGlint(bladeTip.position);
                }
            }

            if (!m_ReleasePlayed && m_Time >= a.windup)
            {
                // 振り抜きで体を伸ばす(解放)
                m_ReleasePlayed = true;
                m_SquashVelocity += releaseStretchImpulse * TierSquashScale(a.tier);
                StopCharge();
            }

            UpdateFacing();
            UpdateLunge(prevTime, m_Time);
            // 地面を叩く攻撃は剣筋では当てず、叩きつけの瞬間の衝撃波でまとめて当てる
            if (a.groundShockwaveRadius > 0f)
                TryGroundSlam();
            else
                SweepForHits(prevTime, m_Time);
            ApplyPose(m_Time);
            ResolveHits();

            // 後隙の後半は移動入力でキャンセルできる(移動の滑らかさを損なわないため)
            bool lateRecovery = m_Time >= a.windup + a.active + a.recovery * 0.5f;
            bool moveCancel = t.moveCancel && lateRecovery && controller.WorldInputDirection.sqrMagnitude > 0.09f;
            if (m_Time >= a.TotalDuration || moveCancel)
                EndAttack();
        }

        void EndAttack()
        {
            StopCharge();
            m_Attacking = false;
            LastAttackEndUnscaledTime = Time.unscaledTime;
            if (m_Record != null)
                m_Record.finished = true;
            controller.MovementLocked = false;
            m_LastAttackEndTime = Time.time;
            m_NextComboIndex = m_Index + 1 < combo.Length ? m_Index + 1 : 0;
            m_SwordLag = Vector3.zero;
            m_SwordLagVelocity = Vector3.zero;
            if (spinRoot != null)
                spinRoot.localRotation = Quaternion.identity;
        }

        bool IsTrailWindow()
        {
            if (!m_Attacking)
                return false;
            AttackData a = m_Attack;
            // 溜め攻撃は溜めている間に軌跡が出ないよう、振り下ろしの直前から出す
            float start = a.chargeEffect ? a.windup - 0.03f : a.windup * 0.75f;
            return m_Time >= start && m_Time <= a.windup + a.active + a.recovery * 0.2f;
        }

        void UpdateFacing()
        {
            // 予備動作の間に目標方向へ素早く向き直る
            float duration = Mathf.Max(0.05f, m_Attack.windup * 0.8f);
            float u = Ease.OutCubic(m_Time / duration);
            controller.SetYaw(Mathf.LerpAngle(m_StartYaw, m_TargetYaw, u));
        }

        void UpdateLunge(float prevTime, float time)
        {
            if (m_LungeDistance <= 0f)
                return;
            AttackData a = m_Attack;
            // 溜め攻撃は溜めている間に滑らないよう、振り下ろしの直前から踏み込む
            float start = a.chargeEffect ? a.windup * 0.85f : a.windup * 0.4f;
            float end = a.windup + a.active * 0.7f;
            float p0 = Ease.OutQuad(Mathf.InverseLerp(start, end, prevTime));
            float p1 = Ease.OutQuad(Mathf.InverseLerp(start, end, time));
            controller.AddDisplacement(m_LungeDirection * (m_LungeDistance * (p1 - p0)));
        }

        // ------------------------------------------------------------------
        // 剣と体の姿勢
        // ------------------------------------------------------------------

        /// <summary>振る面の傾き(roll)と、その面内での角度(angle)から剣の向きを作る。</summary>
        static Quaternion SwingRotation(float roll, float angle)
        {
            return Quaternion.AngleAxis(roll, Vector3.forward) * Quaternion.AngleAxis(angle, Vector3.up);
        }

        void ApplyPose(float time)
        {
            AttackData a = m_Attack;
            // 予備動作と緩急: OFF なら振りかぶりの上乗せを無くし、剣を等速(線形)で動かす
            bool easing = HitFeelSettings.T.anticipation;
            float overshoot = easing ? anticipationOvershoot * Mathf.Sign(a.startAngle - a.endAngle) : 0f;
            float windupAngle = a.startAngle + overshoot;

            Quaternion rotation;
            Vector3 hand;
            float spin;
            float pitch; // + で前傾 / - で後ろに反る

            if (time < a.windup)
            {
                // 予備動作: 振りかぶって一瞬「溜める」(ease-out で減速しながら止まる)
                float u = easing ? Ease.OutCubic(time / a.windup) : Mathf.Clamp01(time / a.windup);
                rotation = Quaternion.Slerp(m_StartSwordRotation, SwingRotation(a.swingRoll, windupAngle), u);
                hand = Vector3.Lerp(m_StartHandPosition, a.handPosition, u);
                spin = 0f;
                pitch = -a.windupLeanBack * u;
                if (a.chargeEffect)
                {
                    // 溜めの間、剣先をわずかに震わせて「力が溜まっている」感じを出す
                    float tremble = Mathf.Sin(time * 90f) * 1.5f * u;
                    rotation *= Quaternion.Euler(tremble, 0f, 0f);
                }
            }
            else if (time < a.windup + a.active)
            {
                // 攻撃判定: 溜めから一気に振り抜く(ease-out で初速最大 → 振り切って減速)
                float u = easing ? Ease.OutCubic((time - a.windup) / a.active) : Mathf.Clamp01((time - a.windup) / a.active);
                rotation = SwingRotation(a.swingRoll, Mathf.Lerp(windupAngle, a.endAngle, u));
                hand = a.handPosition;
                spin = a.bodySpin * u;
                pitch = Mathf.Lerp(-a.windupLeanBack, a.strikeLeanForward, u);
            }
            else
            {
                // 後隙: 構えに戻る
                float recoveryU = Mathf.Clamp01((time - a.windup - a.active) / a.recovery);
                float u = easing ? Ease.InOutCubic(recoveryU) : recoveryU;
                rotation = Quaternion.Slerp(SwingRotation(a.swingRoll, a.endAngle), IdleRotation, u);
                hand = Vector3.Lerp(a.handPosition, idleHandPosition, u);
                spin = a.bodySpin;
                pitch = Mathf.Lerp(a.strikeLeanForward, 0f, u);
            }

            swordPivot.localRotation = rotation;
            swordPivot.localPosition = hand;
            if (spinRoot != null)
                spinRoot.localRotation = Quaternion.Euler(pitch, spin, 0f);
        }

        void RelaxToIdle(float dt)
        {
            if (dt <= 0f)
                return;

            // 剣の揺れ追従: 体の旋回・加速に対して剣が遅れ、走りに合わせて揺れる
            float speed01 = controller.Speed01;
            Vector3 targetLag = Vector3.zero;
            if (HitFeelSettings.T.swordSecondary)
            {
                targetLag = new Vector3(
                    Mathf.Clamp(-controller.ForwardAcceleration * swordLagPerAcceleration, -18f, 18f) + Mathf.Sin(controller.StridePhase) * swordRunSwing * speed01,
                    Mathf.Clamp(-controller.YawRate * swordLagPerYawRate, -25f, 25f),
                    0f);
            }
            m_SwordLag = Vector3.SmoothDamp(m_SwordLag, targetLag, ref m_SwordLagVelocity, swordLagSmoothTime, Mathf.Infinity, dt);

            float k = 1f - Mathf.Exp(-12f * dt);
            Quaternion target = IdleRotation * Quaternion.Euler(m_SwordLag);
            swordPivot.localRotation = Quaternion.Slerp(swordPivot.localRotation, target, k);
            swordPivot.localPosition = Vector3.Lerp(swordPivot.localPosition, idleHandPosition, k);
        }

        void UpdateSquash(float dt)
        {
            if (body == null || dt <= 0f)
                return;
            if (!HitFeelSettings.T.squashStretch)
            {
                m_Squash = 0f;
                m_SquashVelocity = 0f;
                body.localScale = m_BodyBaseScale;
                return;
            }
            // 減衰バネ。体積を保つように縦に伸びたら横は細くする
            m_SquashVelocity += (-squashStiffness * m_Squash - squashDamping * m_SquashVelocity) * dt;
            m_Squash = Mathf.Clamp(m_Squash + m_SquashVelocity * dt, -0.2f, 0.2f);
            float y = 1f + m_Squash;
            float xz = 1f / Mathf.Sqrt(y);
            body.localScale = Vector3.Scale(m_BodyBaseScale, new Vector3(xz, y, xz));
        }

        // ------------------------------------------------------------------
        // 攻撃判定
        // ------------------------------------------------------------------

        void SweepForHits(float prevTime, float time)
        {
            AttackData a = m_Attack;
            float activeStart = a.windup;
            float activeEnd = a.windup + a.active;
            float t0 = Mathf.Max(prevTime, activeStart);
            float t1 = Mathf.Min(time, activeEnd);
            if (t1 <= t0)
                return;

            int steps = Mathf.Clamp(Mathf.CeilToInt((t1 - t0) / a.active * sweepSubSteps), 1, sweepSubSteps);
            ApplyPose(t0);
            Vector3 prevTip = bladeTip.position;

            for (int s = 1; s <= steps; s++)
            {
                ApplyPose(Mathf.Lerp(t0, t1, (float)s / steps));
                Vector3 basePos = bladeBase.position;
                Vector3 tipPos = bladeTip.position;
                Vector3 slashDir = tipPos - prevTip;
                prevTip = tipPos;

                int count = Physics.OverlapCapsuleNonAlloc(basePos, tipPos, bladeRadius, m_Overlap, hitMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Enemy enemy = m_Overlap[i].GetComponentInParent<Enemy>();
                    if (enemy == null || enemy.IsDead || m_HitThisSwing.Contains(enemy))
                        continue;
                    m_HitThisSwing.Add(enemy);
                    m_PendingHits.Add(BuildHitInfo(enemy, basePos, tipPos, slashDir));
                }
            }
        }

        /// <summary>攻撃判定の終わりで地面を叩き、周囲の敵を衝撃波でまとめて巻き込む。</summary>
        void TryGroundSlam()
        {
            AttackData a = m_Attack;
            float slamTime = a.windup + a.active;
            if (m_SlamDone || m_Time < slamTime)
                return;
            m_SlamDone = true;

            ApplyPose(slamTime);
            Vector3 slamPoint = bladeTip.position;
            slamPoint.y = 0f;

            int count = Physics.OverlapSphereNonAlloc(slamPoint + Vector3.up, a.groundShockwaveRadius, m_Overlap, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Enemy enemy = m_Overlap[i].GetComponentInParent<Enemy>();
                if (enemy == null || enemy.IsDead || m_HitThisSwing.Contains(enemy))
                    continue;
                m_HitThisSwing.Add(enemy);
                m_PendingHits.Add(BuildShockwaveHit(enemy, slamPoint));
            }

            // 叩きつけの衝撃そのもの(敵に当たらなくても出す)
            m_SquashVelocity -= 4f;
            if (HitFeedback.Instance != null)
                HitFeedback.Instance.OnGroundSlam(slamPoint, a, m_PendingHits.Count);
        }

        HitInfo BuildShockwaveHit(Enemy enemy, Vector3 slamPoint)
        {
            Vector3 enemyPos = enemy.transform.position;
            Vector3 outward = Ease.Flat(enemyPos - slamPoint);
            outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : transform.forward;
            Vector3 point = enemyPos - outward * enemy.BodyRadius + Vector3.up * 0.6f;
            return new HitInfo
            {
                attack = m_Attack,
                target = enemy,
                point = point,
                normal = -outward,
                slashDirection = (outward + Vector3.up * 0.8f).normalized,
                attackerPosition = slamPoint,
            };
        }

        HitInfo BuildHitInfo(Enemy enemy, Vector3 basePos, Vector3 tipPos, Vector3 slashDir)
        {
            Vector3 enemyPos = enemy.transform.position;
            Vector3 onBlade = ClosestPointOnSegment(basePos, tipPos, enemyPos + Vector3.up * enemy.BodyCenterHeight);
            Vector3 axis = new Vector3(enemyPos.x, onBlade.y, enemyPos.z);

            // 刃が胴体にめり込んだ点ではなく、こちら側の表面を着弾点にする(エフェクトが体内に埋もれないように)
            Vector3 toAttacker = Ease.Flat(transform.position - axis).normalized;
            Vector3 normal = Ease.Flat(onBlade - axis).normalized + toAttacker * 0.6f;
            normal = normal.sqrMagnitude > 1e-4f ? normal.normalized : toAttacker;

            Vector3 point = axis + normal * enemy.BodyRadius;
            point.y = Mathf.Clamp(onBlade.y, enemyPos.y + 0.35f, enemyPos.y + 1.85f);

            return new HitInfo
            {
                attack = m_Attack,
                target = enemy,
                point = point,
                normal = normal,
                slashDirection = slashDir.sqrMagnitude > 1e-6f ? slashDir.normalized : transform.right,
                attackerPosition = transform.position,
            };
        }

        void ResolveHits()
        {
            if (m_PendingHits.Count == 0)
                return;

            if (!m_HitConfirmed && m_Record != null)
                m_Record.hitTime = m_Time;
            m_HitConfirmed = true;
            for (int i = 0; i < m_PendingHits.Count; i++)
            {
                HitInfo info = m_PendingHits[i];
                info.killed = info.target.TakeHit(info);
                if (HitFeedback.Instance != null)
                    HitFeedback.Instance.OnHit(info);
            }

            // 【6】ノックバック(自分側): 重い攻撃ほど斬った手応えで押し戻される
            bool pushesBack = m_Attack.tier == AttackTier.Heavy || m_Attack.tier == AttackTier.Finisher;
            if (HitFeelSettings.T.knockback && pushesBack)
            {
                float speed = m_Attack.tier == AttackTier.Finisher ? finisherRecoilSpeed : heavyRecoilSpeed;
                controller.AddImpulse(-transform.forward * speed);
            }

            m_PendingHits.Clear();
        }

        Enemy FindAssistTarget(Vector3 desiredDir)
        {
            Enemy best = null;
            float bestScore = float.MaxValue;
            foreach (Enemy enemy in Enemy.All)
            {
                if (enemy.IsDead)
                    continue;
                Vector3 to = Ease.Flat(enemy.transform.position - transform.position);
                float distance = to.magnitude;
                if (distance > assistRange)
                    continue;
                float angle = Vector3.Angle(desiredDir, to);
                if (angle > assistAngle && distance > assistStopDistance)
                    continue;
                float score = distance + angle * 0.03f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }
            return best;
        }

        static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return a + ab * t;
        }
    }
}
