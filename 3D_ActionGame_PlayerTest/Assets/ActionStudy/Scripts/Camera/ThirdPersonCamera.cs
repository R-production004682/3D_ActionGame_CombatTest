using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// 三人称のオービットカメラ + カメラシェイク + FOV パンチ。
    /// 追従・シェイクはすべて実時間で動かす(ヒットストップ中も揺れ続けるように)。
    ///
    /// 操作を気持ちよくする工夫(HitFeelSettings の「操作の手触り：カメラ」で個別に OFF にできる):
    ///  ・追従の遅れ … わずかに遅れてついてきて、移動に滑らかさとスピード感を出す
    ///  ・先読み … 進行方向へ少し先を映し、行き先が見やすくなる
    ///  ・速度ズーム … 速く走るほど少し引いて、スピード感と視界を両立する
    ///  ・自動回り込み … 視点操作をしばらくしていない時、横移動に合わせてゆっくり背後へ回り込む
    ///  ・攻撃フレーミング … 攻撃中は狙った敵が画面から外れないよう、自動で向きを寄せる
    ///
    /// シェイクは「トラウマ値」方式: AddShake で値を足し、時間で減衰させ、揺れ幅はトラウマの 2 乗に比例させる。
    /// 小さなヒットは控えめに、重いヒットは急激に強く揺れ、自然に収まる。揺れは衝撃の方向に寄せる。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        public static ThirdPersonCamera Instance { get; private set; }

        [Header("参照")]
        public Transform target;
        public PlayerInputReader input;
        [Tooltip("未設定なら target から探す")]
        public PlayerController controller;
        [Tooltip("未設定なら target から探す")]
        public PlayerCombat combat;

        [Header("オービット")]
        public Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);
        public float distance = 5.2f;
        public float mouseSensitivity = 0.12f;
        public float stickSensitivity = 200f;
        public float minPitch = -25f;
        public float maxPitch = 65f;
        [Tooltip("追従の遅れ(秒)")]
        public float followSmoothTime = 0.1f;

        [Header("先読み・速度ズーム")]
        [Tooltip("進行方向へ何秒先を映すか")]
        public float lookAheadTime = 0.12f;
        public float lookAheadSmoothTime = 0.3f;
        [Tooltip("最高速の時に追加で引く距離")]
        public float speedZoomDistance = 0.8f;
        public float speedZoomSmoothTime = 0.5f;

        [Header("自動回り込み")]
        [Tooltip("視点操作をやめてから回り込みを始めるまでの時間")]
        public float recenterDelay = 1f;
        [Tooltip("回り込みの速さ (度/秒)。横移動の量に比例する")]
        public float recenterSpeed = 70f;

        [Header("攻撃フレーミング")]
        [Tooltip("狙った敵が画面中央からこの角度以上ずれたら寄せる")]
        public float framingAngle = 25f;
        public float framingSpeed = 160f;

        [Header("カメラシェイク")]
        public float maxShakeOffset = 0.28f;
        public float maxShakeRoll = 2.5f;
        public float shakeFrequency = 30f;
        [Tooltip("トラウマ値が 1 秒あたりに減る量")]
        public float traumaDecay = 2.2f;

        [Header("FOV パンチ")]
        public float fovReturnTime = 0.18f;

        [Header("溜め (超強)")]
        [Tooltip("溜め切った時に狭める FOV (度)")]
        public float chargeFov = 7f;
        [Tooltip("溜め切った時に寄る距離 (m)")]
        public float chargeDistance = 0.9f;
        public float chargeSmoothTime = 0.12f;

        [Header("ロックオン")]
        [Tooltip("未設定なら target から探す")]
        public PlayerLockOn lockOn;
        [Tooltip("ロック中の敵の方向へ向き直るまでの時間の目安(秒)")]
        public float lockOnSmoothTime = 0.15f;
        [Tooltip("ロック中、視点操作をしていない時に戻す上下の角度")]
        public float lockOnPitch = 12f;

        Camera m_Camera;
        float m_LockYawVelocity;
        float m_Yaw;
        float m_Pitch = 15f;
        Vector3 m_Pivot;
        Vector3 m_PivotVelocity;
        Vector3 m_LookAhead;
        Vector3 m_LookAheadVelocity;
        float m_ZoomOffset;
        float m_ZoomVelocity;
        float m_LastLookInputTime = -10f;
        float m_Trauma;
        Vector3 m_ShakeDirection;
        float m_ShakeSeed;
        float m_BaseFov;
        float m_FovOffset;
        float m_FovVelocity;
        float m_ChargeTarget;
        float m_Charge;
        float m_ChargeVelocity;

        public float Yaw => m_Yaw;

        void Awake()
        {
            Instance = this;
            m_Camera = GetComponent<Camera>();
            m_BaseFov = m_Camera.fieldOfView;
            m_ShakeSeed = Random.value * 100f;
        }

        void Start()
        {
            if (target != null)
            {
                m_Yaw = target.eulerAngles.y;
                m_Pivot = target.position + pivotOffset;
                if (controller == null)
                    controller = target.GetComponent<PlayerController>();
                if (combat == null)
                    combat = target.GetComponent<PlayerCombat>();
                if (lockOn == null)
                    lockOn = target.GetComponent<PlayerLockOn>();
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>【8】カメラシェイクを加える。direction はワールド空間での衝撃方向。</summary>
        public void AddShake(float amount, Vector3 direction)
        {
            if (!HitFeelSettings.T.cameraShake)
                return;
            m_Trauma = Mathf.Clamp01(m_Trauma + amount);
            m_ShakeDirection = direction;
        }

        /// <summary>一瞬ズームして戻る。正の値でズームイン、負の値でズームアウト(衝撃で弾ける)。</summary>
        public void FovPunch(float degrees)
        {
            if (!HitFeelSettings.T.fovPunch)
                return;
            m_FovOffset = -degrees;
            m_FovVelocity = 0f;
        }

        /// <summary>溜めの進み具合 (0-1)。溜めている間カメラがじわじわ寄り、0 で一気に戻る。</summary>
        public void SetCharge(float amount)
        {
            m_ChargeTarget = Mathf.Clamp01(amount);
            if (m_ChargeTarget <= 0f)
            {
                // 解放は一瞬で戻す(叩きつけの FOV パンチと合わさって『弾ける』)
                m_Charge = 0f;
                m_ChargeVelocity = 0f;
            }
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            float dt = Time.unscaledDeltaTime;
            float gameDt = Time.deltaTime;
            HitFeelToggles t = HitFeelSettings.T;
            Enemy locked = t.lockOn && lockOn != null ? lockOn.LockedTarget : null;

            // --- 視点操作 (ロック中は左右を敵に任せ、上下だけ動かせる) ---
            if (input != null)
            {
                Vector2 look = input.LookStick * (stickSensitivity * dt);
                if (Cursor.lockState == CursorLockMode.Locked)
                    look += input.LookMouseDelta * mouseSensitivity;
                if (look.sqrMagnitude > 1e-4f)
                    m_LastLookInputTime = Time.unscaledTime;
                if (locked == null)
                    m_Yaw += look.x;
                m_Pitch = Mathf.Clamp(m_Pitch - look.y, minPitch, maxPitch);
            }

            // --- ロックオン: 敵の方向へ向き続ける。移動はそのまま敵の周りを回り込む動きになる ---
            if (locked != null)
            {
                Vector3 toEnemy = Ease.Flat(locked.transform.position - target.position);
                // 敵に重なるほど近い時は向きが暴れるので追わない
                if (toEnemy.sqrMagnitude > 0.36f)
                {
                    float lockYaw = Mathf.Atan2(toEnemy.x, toEnemy.z) * Mathf.Rad2Deg;
                    m_Yaw = Mathf.SmoothDampAngle(m_Yaw, lockYaw, ref m_LockYawVelocity, lockOnSmoothTime, Mathf.Infinity, dt);
                }
                if (Time.unscaledTime - m_LastLookInputTime > recenterDelay)
                    m_Pitch = Mathf.MoveTowards(m_Pitch, lockOnPitch, 30f * dt);
            }
            else
            {
                m_LockYawVelocity = 0f;
            }

            Vector3 velocity = controller != null ? controller.PlanarVelocity : Vector3.zero;
            float speed01 = controller != null ? controller.Speed01 : 0f;

            // --- 自動回り込み: 横移動の量に比例して、進行方向の背後へゆっくり回る ---
            if (t.autoRecenter && locked == null && gameDt > 0f && Time.unscaledTime - m_LastLookInputTime > recenterDelay && velocity.sqrMagnitude > 1f)
            {
                float moveYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                float diff = Mathf.DeltaAngle(m_Yaw, moveYaw);
                float lateral = Mathf.Abs(Mathf.Sin(diff * Mathf.Deg2Rad));
                // 真後ろ(カメラへ向かってくる)方向には回り込まない
                if (Mathf.Abs(diff) < 135f)
                    m_Yaw = Mathf.MoveTowardsAngle(m_Yaw, moveYaw, recenterSpeed * lateral * speed01 * gameDt);
            }

            // --- 攻撃フレーミング: 狙った敵が画面から外れないように寄せる ---
            Enemy framed = t.attackFraming && locked == null && combat != null ? combat.CurrentTarget : null;
            if (framed != null && gameDt > 0f)
            {
                Vector3 mid = Vector3.Lerp(target.position, framed.transform.position, 0.5f) - m_Pivot;
                if (mid.sqrMagnitude > 0.01f)
                {
                    float targetYaw = Mathf.Atan2(mid.x, mid.z) * Mathf.Rad2Deg;
                    float diff = Mathf.DeltaAngle(m_Yaw, targetYaw);
                    if (Mathf.Abs(diff) > framingAngle)
                        m_Yaw = Mathf.MoveTowardsAngle(m_Yaw, targetYaw - Mathf.Sign(diff) * framingAngle, framingSpeed * gameDt);
                }
            }

            // --- 追従 + 先読み ---
            if (t.cameraLag)
            {
                m_LookAhead = Vector3.SmoothDamp(m_LookAhead, velocity * lookAheadTime, ref m_LookAheadVelocity, lookAheadSmoothTime, Mathf.Infinity, dt);
                Vector3 desiredPivot = target.position + pivotOffset + m_LookAhead;
                m_Pivot = Vector3.SmoothDamp(m_Pivot, desiredPivot, ref m_PivotVelocity, followSmoothTime, Mathf.Infinity, dt);
            }
            else
            {
                // 遅れも先読みも無し: プレイヤーにぴったり張り付く
                m_LookAhead = Vector3.zero;
                m_LookAheadVelocity = Vector3.zero;
                m_PivotVelocity = Vector3.zero;
                m_Pivot = target.position + pivotOffset;
            }

            // --- 速度ズーム ---
            m_ZoomOffset = Mathf.SmoothDamp(m_ZoomOffset, t.speedZoom ? speedZoomDistance * speed01 : 0f, ref m_ZoomVelocity, speedZoomSmoothTime, Mathf.Infinity, dt);

            Quaternion orbit = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
            m_Charge = Mathf.SmoothDamp(m_Charge, m_ChargeTarget, ref m_ChargeVelocity, chargeSmoothTime, Mathf.Infinity, dt);
            float chargeEase = m_Charge * m_Charge;
            Vector3 position = m_Pivot - orbit * Vector3.forward * (distance + m_ZoomOffset - chargeDistance * chargeEase);

            // --- 【8】カメラシェイク ---
            m_Trauma = Mathf.Max(0f, m_Trauma - traumaDecay * dt);
            float shake = m_Trauma * m_Trauma;
            float roll = 0f;
            if (shake > 0f)
            {
                float time = Time.unscaledTime * shakeFrequency;
                float nx = Mathf.PerlinNoise(m_ShakeSeed, time) * 2f - 1f;
                float ny = Mathf.PerlinNoise(m_ShakeSeed + 10f, time) * 2f - 1f;
                float nr = Mathf.PerlinNoise(m_ShakeSeed + 20f, time) * 2f - 1f;

                // 衝撃方向を画面平面に投影し、その軸に沿って往復させる(+ 少しだけ垂直方向のノイズ)
                Vector3 local = Quaternion.Inverse(orbit) * m_ShakeDirection;
                Vector2 axis = new Vector2(local.x, local.y);
                axis = axis.sqrMagnitude > 1e-4f ? axis.normalized : Vector2.right;
                Vector2 perpendicular = new Vector2(-axis.y, axis.x);
                Vector2 o = axis * nx + perpendicular * (ny * 0.3f);

                position += orbit * new Vector3(o.x, o.y, 0f) * (maxShakeOffset * shake);
                roll = nr * maxShakeRoll * shake;
            }

            transform.SetPositionAndRotation(position, orbit * Quaternion.Euler(0f, 0f, roll));

            // --- FOV パンチ ---
            m_FovOffset = Mathf.SmoothDamp(m_FovOffset, 0f, ref m_FovVelocity, fovReturnTime, Mathf.Infinity, dt);
            m_Camera.fieldOfView = m_BaseFov + m_FovOffset - chargeFov * chargeEase;
        }
    }
}
