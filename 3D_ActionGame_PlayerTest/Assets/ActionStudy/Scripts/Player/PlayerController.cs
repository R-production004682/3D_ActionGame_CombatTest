using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// 三人称の移動。ニーアオートマタのような「滑らかで速い」移動を作るポイント:
    ///  1. カメラ基準の入力方向
    ///  2. 加速 / 減速に慣性を持たせる。ただし走り出しだけは加速を強くして「もたつき」を消す (初速ブースト)
    ///  3. 向きは角速度上限付きで滑らかに回す。逆方向への切り返しだけは素早く振り向く (クイックターン)
    ///  4. 旋回時に内側へ傾け、加速で前傾、急停止・切り返しで後傾する
    ///  5. 走っている間は歩幅に合わせて体を上下・左右に揺らす (ランボブ)
    /// 攻撃中は PlayerCombat が移動をロックし、踏み込みなどの変位を外から与える。
    /// 2〜5 は HitFeelSettings の「操作の手触り：体の動き」で個別に OFF にできる。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("参照")]
        public PlayerInputReader input;
        public ThirdPersonCamera cameraRig;
        [Tooltip("傾け用の見た目ルート (足元が原点)")]
        public Transform leanRoot;

        [Header("移動")]
        public float maxSpeed = 7.5f;
        [Tooltip("加速度 (m/s^2)")]
        public float acceleration = 38f;
        [Tooltip("止まっている状態からの加速度の倍率。走り出しを鋭くする")]
        public float startBoost = 2f;
        [Tooltip("減速度 (m/s^2)")]
        public float deceleration = 34f;
        [Tooltip("向きが追いつくまでの時間の目安(秒)")]
        public float turnSmoothTime = 0.07f;
        [Tooltip("最大旋回速度 (度/秒)")]
        public float maxTurnSpeed = 1080f;
        public float gravity = 25f;

        [Header("クイックターン (切り返し)")]
        [Tooltip("入力と進行方向がこの角度以上ずれたら切り返しとみなす")]
        public float quickTurnAngle = 110f;
        [Tooltip("切り返し時の振り向きの速さの倍率")]
        public float quickTurnSpeedMultiplier = 3f;

        [Header("体の傾き")]
        [Tooltip("旋回で内側へ傾く最大角度")]
        public float maxLeanAngle = 16f;
        [Tooltip("旋回角速度に対する傾きの係数")]
        public float leanPerTurnSpeed = 0.03f;
        [Tooltip("走行中の前傾角度")]
        public float runForwardLean = 7f;
        [Tooltip("加速度に対する前傾の係数")]
        public float leanPerAcceleration = 0.3f;
        public float leanSharpness = 12f;

        [Header("ランボブ (走りの揺れ)")]
        [Tooltip("1 歩の長さ (m)")]
        public float strideLength = 1.15f;
        public float bobHeight = 0.05f;
        public float bobSwayAngle = 2f;

        CharacterController m_CharacterController;
        Vector3 m_PlanarVelocity;
        Vector3 m_ImpulseVelocity;
        Vector3 m_ExternalDisplacement;
        float m_VerticalVelocity;
        float m_Yaw;
        float m_YawVelocity;
        float m_PrevYaw;
        Vector3 m_PrevPlanarVelocity;
        float m_LeanRoll;
        float m_LeanPitch;
        float m_StridePhase;
        float m_Bob;

        /// <summary>true の間は入力による移動・旋回を受け付けない(攻撃中など)。</summary>
        public bool MovementLocked { get; set; }
        public float Yaw => m_Yaw;
        public Vector3 PlanarVelocity => m_PlanarVelocity;
        public float Speed01 => Mathf.Clamp01(m_PlanarVelocity.magnitude / maxSpeed);
        /// <summary>旋回の角速度 (度/秒)。剣の揺れ追従などに使う。</summary>
        public float YawRate { get; private set; }
        /// <summary>前後方向の加速度 (m/s^2)。</summary>
        public float ForwardAcceleration { get; private set; }
        /// <summary>歩行周期の位相 (ラジアン)。1 歩で π 進む。</summary>
        public float StridePhase => m_StridePhase;
        /// <summary>クイックターン(切り返し)中か。足元の砂煙などに使う。</summary>
        public bool IsQuickTurning { get; private set; }

        /// <summary>カメラ基準のワールド入力方向 (大きさ 0-1)。</summary>
        public Vector3 WorldInputDirection
        {
            get
            {
                Vector2 move = input != null ? input.Move : Vector2.zero;
                Quaternion basis = Quaternion.Euler(0f, cameraRig != null ? cameraRig.Yaw : 0f, 0f);
                return basis * new Vector3(move.x, 0f, move.y);
            }
        }

        void Awake()
        {
            m_CharacterController = GetComponent<CharacterController>();
            m_Yaw = transform.eulerAngles.y;
            m_PrevYaw = m_Yaw;
        }

        /// <summary>向きを外部から設定する(攻撃時の向き補正など)。</summary>
        public void SetYaw(float yaw)
        {
            m_Yaw = yaw;
            m_YawVelocity = 0f;
            transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
        }

        /// <summary>このフレームに追加で移動させる量(踏み込みなど)。</summary>
        public void AddDisplacement(Vector3 displacement)
        {
            m_ExternalDisplacement += displacement;
        }

        /// <summary>減衰する速度を加える(自分へのノックバックなど)。</summary>
        public void AddImpulse(Vector3 velocity)
        {
            m_ImpulseVelocity += velocity;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            HitFeelToggles t = HitFeelSettings.T;
            Vector3 desired = MovementLocked ? Vector3.zero : WorldInputDirection * maxSpeed;

            // --- 加減速 ---
            bool speedingUp = desired.sqrMagnitude > m_PlanarVelocity.sqrMagnitude;
            float rate;
            if (MovementLocked)
                rate = deceleration * 1.5f;
            else if (speedingUp)
                rate = acceleration * (t.startBoost ? Mathf.Lerp(startBoost, 1f, Speed01) : 1f); // 走り出しほど強く加速
            else
                rate = deceleration;
            m_PlanarVelocity = Vector3.MoveTowards(m_PlanarVelocity, desired, rate * dt);

            // --- 旋回 ---
            IsQuickTurning = false;
            if (!MovementLocked && desired.sqrMagnitude > 0.01f)
            {
                float targetYaw = Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg;
                float smoothTime = turnSmoothTime;
                float maxSpeedDeg = maxTurnSpeed;
                // クイックターン: 逆方向に入れたら素早く振り向く(慣性で滑りながら向きだけ先に変わる)
                if (t.quickTurn && Mathf.Abs(Mathf.DeltaAngle(m_Yaw, targetYaw)) > quickTurnAngle)
                {
                    IsQuickTurning = true;
                    smoothTime /= quickTurnSpeedMultiplier;
                    maxSpeedDeg *= quickTurnSpeedMultiplier;
                }
                m_Yaw = Mathf.SmoothDampAngle(m_Yaw, targetYaw, ref m_YawVelocity, smoothTime, maxSpeedDeg, dt);
            }
            transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);

            // --- 重力 ---
            if (m_CharacterController.isGrounded && m_VerticalVelocity < 0f)
                m_VerticalVelocity = -2f;
            else
                m_VerticalVelocity -= gravity * dt;

            Vector3 motion = (m_PlanarVelocity + m_ImpulseVelocity + Vector3.up * m_VerticalVelocity) * dt + m_ExternalDisplacement;
            m_ExternalDisplacement = Vector3.zero;
            m_ImpulseVelocity *= Mathf.Exp(-10f * dt);
            m_CharacterController.Move(motion);

            UpdateLeanAndBob(dt, t);
        }

        void UpdateLeanAndBob(float dt, HitFeelToggles t)
        {
            float speed01 = Speed01;
            YawRate = Mathf.DeltaAngle(m_PrevYaw, m_Yaw) / dt;
            Vector3 accel = (m_PlanarVelocity - m_PrevPlanarVelocity) / dt;
            ForwardAcceleration = Vector3.Dot(accel, transform.forward);

            // 右旋回 (YawRate > 0) なら右(内側)へ傾ける = Z 軸回転はマイナス
            float targetRoll = Mathf.Clamp(-YawRate * leanPerTurnSpeed * speed01, -maxLeanAngle, maxLeanAngle);
            // 加速で前傾、急停止・切り返しで後傾
            float targetPitch = runForwardLean * speed01 + Mathf.Clamp(ForwardAcceleration * leanPerAcceleration, -12f, 8f);
            if (!t.bodyLean)
            {
                targetRoll = 0f;
                targetPitch = 0f;
            }

            float k = 1f - Mathf.Exp(-leanSharpness * dt);
            m_LeanRoll = Mathf.Lerp(m_LeanRoll, targetRoll, k);
            m_LeanPitch = Mathf.Lerp(m_LeanPitch, targetPitch, k);

            // ランボブ: 進んだ距離で位相を進めるので、速度が変わっても足取りと揺れがずれない
            m_StridePhase += m_PlanarVelocity.magnitude * dt / strideLength * Mathf.PI;
            m_Bob = Mathf.Lerp(m_Bob, t.runBob ? speed01 : 0f, 1f - Mathf.Exp(-8f * dt));
            float bobY = (Mathf.Abs(Mathf.Sin(m_StridePhase)) - 0.5f) * bobHeight * m_Bob;
            float sway = Mathf.Sin(m_StridePhase) * bobSwayAngle * m_Bob;

            if (leanRoot != null)
            {
                leanRoot.localRotation = Quaternion.Euler(m_LeanPitch, 0f, m_LeanRoll + sway);
                leanRoot.localPosition = new Vector3(0f, bobY, 0f);
            }

            m_PrevYaw = m_Yaw;
            m_PrevPlanarVelocity = m_PlanarVelocity;
        }
    }
}
