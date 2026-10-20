using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// 練習用のかかし。その場から動かず、吹き飛ばない。
    /// 当たると「起き上がりこぼし」のように、丸い底で転がりながら傾き、何度か揺れ戻って立ち直る。
    /// 傾く角度は攻撃ののけぞり量(staggerAngle)に比例し、ノックバック距離の分も傾きに上乗せする。
    /// chasePlayer を ON にすると、以前のようにプレイヤーへ寄ってきて、押し出され・吹き飛ぶ敵になる。
    ///
    /// 被弾リアクションを要素ごとに分けて実装してあり、HitFeelSettings で個別に ON/OFF できる:
    ///  ・【4】ダメージモーション … かかし: のけぞり量に比例して傾く / 寄ってくる敵: 移動を止めて仰け反る・最強の撃破で吹き飛ぶ
    ///  ・【5】加算モーション     … 本来の動きを続けたまま、硬いバネで体が小刻みに揺れる
    ///  ・【6】ノックバック       … かかし: 押された分だけ大きく傾く / 寄ってくる敵: 初速を与えて指数減衰で押し出す
    ///  ・【7】モデルシェイク     … ヒットストップ中も見た目だけ小刻みに揺らす(実時間)
    ///  ・【9】ダメージフラッシュ … オーバーレイマテリアルを光らせる
    /// HP が 0 になると「コテッ」と倒れ、少し間をおいて沈みながら消える。
    /// 追加演出「撃破の予兆」が ON なら、倒した瞬間に目の光が消え、一瞬硬直して明滅してから倒れる。
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        [Header("参照")]
        [Tooltip("見た目のルート(足元が原点)。傾き・揺れはここに与える")]
        public Transform visual;
        [Tooltip("ダメージフラッシュを重ねる Renderer")]
        public Renderer[] flashRenderers;
        public Material flashMaterial;

        [Header("かかし")]
        [Tooltip("OFF: かかし(その場から動かず、起き上がりこぼしのように揺れる) / ON: プレイヤーへ寄ってくる")]
        public bool chasePlayer;

        [Header("起き上がりこぼし (かかしの時)")]
        [Tooltip("のけぞり角度 (AttackData.staggerAngle) × この値 = 傾く角度")]
        public float weebleTiltPerStagger = 1.6f;
        [Tooltip("ノックバック距離 (m) × この値 = 追加で傾く角度")]
        public float weebleTiltPerKnockback = 8f;
        [Tooltip("これ以上は傾かない(倒れない)角度")]
        public float weebleMaxTilt = 80f;
        [Tooltip("揺れ戻りの速さ (Hz)。小さいほどゆったり揺れる")]
        public float weebleFrequency = 1.2f;
        [Tooltip("揺れ戻りの減衰。小さいほど何度も揺れる")]
        public float weebleDamping = 2.8f;
        [Tooltip("撃破の予兆で消す目の Renderer。未設定なら Visual 以下の EyeL / EyeR を探す")]
        public Renderer[] eyeRenderers;

        [Header("パラメータ")]
        public float maxHp = 100f;
        public float moveSpeed = 2.4f;
        public float turnSpeed = 300f;
        public float stopDistance = 1.7f;
        public float separationRadius = 1.3f;
        public float bodyRadius = 0.5f;
        public float bodyCenterHeight = 1f;

        [Header("リアクション")]
        [Tooltip("ノックバックの減速の強さ。大きいほど急ブレーキ")]
        public float knockbackDamping = 9f;
        public float flashDuration = 0.1f;
        public float modelShakeAmplitude = 0.06f;
        [Tooltip("揺れの周波数 (Hz)。60fps で毎フレーム左右が入れ替わる 30Hz が一番「ガタガタ」して見える。" +
                 "フレームレートに近い値(55Hz など)にするとエイリアシングでゆっくりした揺れに見えてしまうので注意")]
        public float modelShakeFrequency = 30f;
        [Tooltip("加算リアクションのバネの硬さ / 減衰")]
        public float recoilStiffness = 260f;
        public float recoilDamping = 14f;
        public float recoilImpulse = 220f;

        [Header("打ち上げ")]
        public float airGravity = 25f;
        [Tooltip("着地時にこの速さ以上で落ちてきたらバウンドする")]
        public float bounceSpeedThreshold = 5f;
        public float bounceRestitution = 0.3f;
        [Tooltip("着地してから起き上がり始めるまでの時間")]
        public float getUpDelay = 0.6f;
        [Tooltip("空中で追撃された時に浮き直す速さ (お手玉)")]
        public float juggleUpSpeed = 3.5f;

        [Header("出現")]
        [Tooltip("出現時に地面からせり上がる時間")]
        public float spawnRiseDuration = 0.4f;

        [Header("死亡")]
        [Tooltip("撃破の予兆: 倒れ始めるまでの硬直時間(秒)")]
        public float deathTellDuration = 0.16f;
        public float toppleDuration = 0.38f;
        public float lieDuration = 0.7f;
        public float vanishDuration = 0.45f;

        public event Action<Enemy> Died;

        public bool IsDead { get; private set; }
        public float BodyRadius => bodyRadius;
        public float BodyCenterHeight => bodyCenterHeight;
        public float HpRatio => Mathf.Clamp01(m_Hp / maxHp);
        /// <summary>その場から動かないかかしか。</summary>
        bool Planted => !chasePlayer;
        /// <summary>頭のてっぺんの位置 (HP ゲージなどの表示用)。体の傾き・揺れ・出現時のせり上がりにもついていく。</summary>
        public Vector3 HeadPosition => visual != null
            ? visual.TransformPoint(Vector3.up * (bodyCenterHeight * 2f))
            : transform.position + Vector3.up * (bodyCenterHeight * 2f);
        /// <summary>体の中心の位置 (ロックオンマーカーの表示用)。体の傾き・揺れにもついていく。</summary>
        public Vector3 CenterPosition => visual != null
            ? visual.TransformPoint(Vector3.up * bodyCenterHeight)
            : transform.position + Vector3.up * bodyCenterHeight;

        Transform m_Player;
        Collider m_Collider;
        MaterialPropertyBlock m_Block;
        float m_Hp;

        Vector3 m_KnockVelocity;
        float m_StaggerRemaining;
        float m_StaggerTarget;
        float m_StaggerTilt;
        Vector3 m_HitDirection = Vector3.forward;

        Vector3 m_Recoil;          // 傾き(度)を水平ベクトルで表現: 向きが傾く方向、大きさが角度
        Vector3 m_RecoilVelocity;
        Vector3 m_Wobble;          // 起き上がりこぼしの傾き(度)。表現は m_Recoil と同じ
        Vector3 m_WobbleVelocity;

        float m_FlashStart = -10f;
        float m_FlashPeak;
        float m_ShakeStart = -10f;
        float m_ShakeDuration;
        float m_ShakeAmplitude;

        // 生きたまま打ち上げられている間の状態
        bool m_Airborne;
        Vector3 m_AirVelocity;

        Vector3 m_HomePosition;
        Quaternion m_HomeRotation = Quaternion.identity;
        bool m_HasHome;

        enum DeathPhase { None, Airborne, Tell, Topple, Lie, Vanish }
        DeathPhase m_DeathPhase;
        float m_DeathTime;
        float m_FlightDuration;
        float m_DeathSpinTotal = 450f;
        float m_DeathStartAngle;
        Vector3 m_DeathVelocity;
        Vector3 m_DeathDirection;
        Vector3 m_VisualBaseScale;
        float m_Age;

        static readonly int s_FlashIntensityId = Shader.PropertyToID("_FlashIntensity");

        void Awake()
        {
            m_Hp = maxHp;
            m_Collider = GetComponent<Collider>();
            m_Block = new MaterialPropertyBlock();
            m_VisualBaseScale = visual != null ? visual.localScale : Vector3.one;

            // ダメージフラッシュ用のマテリアルを 2 枚目として重ねる
            if (flashMaterial != null && flashRenderers != null)
            {
                foreach (Renderer r in flashRenderers)
                {
                    Material[] mats = r.sharedMaterials;
                    var newMats = new Material[mats.Length + 1];
                    mats.CopyTo(newMats, 0);
                    newMats[mats.Length] = flashMaterial;
                    r.sharedMaterials = newMats;
                }
            }
            SetFlash(0f);

            if ((eyeRenderers == null || eyeRenderers.Length == 0) && visual != null)
            {
                var eyes = new List<Renderer>();
                foreach (string eyeName in new[] { "EyeL", "EyeR" })
                {
                    Transform eye = visual.Find(eyeName);
                    if (eye != null && eye.TryGetComponent(out Renderer r))
                        eyes.Add(r);
                }
                eyeRenderers = eyes.ToArray();
            }
        }

        void Start()
        {
            if (!m_HasHome)
                SetHome(transform.position, transform.rotation);
        }

        /// <summary>かかしが立つ位置と向き。</summary>
        public void SetHome(Vector3 position, Quaternion rotation)
        {
            m_HomePosition = position;
            m_HomeRotation = rotation;
            m_HasHome = true;
        }

        void OnEnable()
        {
            All.Add(this);
        }

        void OnDisable()
        {
            All.Remove(this);
        }

        public void SetPlayer(Transform player)
        {
            m_Player = player;
        }

        /// <summary>被弾処理。倒れたら true を返す。</summary>
        public bool TakeHit(HitInfo info)
        {
            if (IsDead)
                return false;

            HitFeelToggles t = HitFeelSettings.T;
            AttackData a = info.attack;
            m_Hp -= a.damage;
            m_HitDirection = info.PushDirection;
            bool killed = m_Hp <= 0f;

            // 【9】ダメージフラッシュ
            if (t.damageFlash)
            {
                m_FlashStart = Time.unscaledTime;
                m_FlashPeak = TierValue(a.tier, 1.2f, 2.2f, 2.2f, 3.2f);
            }

            // 【7】モデルシェイク
            if (t.modelShake)
            {
                // ヒットストップの間 + 少しだけ揺らす
                m_ShakeStart = Time.unscaledTime;
                m_ShakeDuration = a.hitStopDuration + 0.08f;
                m_ShakeAmplitude = modelShakeAmplitude * TierValue(a.tier, 1f, 1.5f, 2f, 2.5f);
            }

            // 【6】ノックバック (かかしは位置を動かさず、下の起き上がりこぼしで傾きに変える)
            if (t.knockback && !Planted)
            {
                // 指数減衰する速度の総移動量は v0 / damping なので、狙った距離から初速を逆算する
                m_KnockVelocity = m_HitDirection * (a.knockbackDistance * knockbackDamping);
            }

            // かかし: のけぞり量(+ ノックバック量)に比例した角度まで傾くよう、揺れの初速を与える
            if (Planted)
            {
                float tilt = (t.damageMotion ? a.staggerAngle * weebleTiltPerStagger : 0f)
                             + (t.knockback ? a.knockbackDistance * weebleTiltPerKnockback : 0f);
                // 減衰の小さいバネは「初速 / 角周波数」がほぼ最大の傾きになるので、狙った角度から初速を逆算する
                m_WobbleVelocity += m_HitDirection * (tilt * WeebleOmega);
            }
            // 【4】ダメージモーション(のけぞり / 打ち上げ)
            else if (t.damageMotion)
            {
                m_StaggerRemaining = Mathf.Max(m_StaggerRemaining, a.staggerTime);
                m_StaggerTarget = Mathf.Max(a.staggerAngle, m_Airborne ? m_StaggerTarget : 0f);

                if (!killed && a.launchAlive && a.launchUpSpeed > 0f)
                {
                    // 倒しきれなくても大きく打ち上げる。着地までは動けない
                    m_Airborne = true;
                    m_AirVelocity = m_HitDirection * a.launchSpeed + Vector3.up * a.launchUpSpeed;
                    m_KnockVelocity = Vector3.zero;
                    m_StaggerTarget = 75f;
                }
                else if (m_Airborne)
                {
                    // 空中で追撃されたら少し浮き直す(お手玉)
                    m_AirVelocity.y = Mathf.Max(m_AirVelocity.y, juggleUpSpeed);
                }
            }

            // 【5】加算モーション
            if (t.additiveMotion)
            {
                float scale = TierValue(a.tier, 1f, 1.4f, 1.8f, 2.2f);
                Vector3 slashSide = Ease.Flat(info.slashDirection).normalized * 0.5f;
                m_RecoilVelocity += (m_HitDirection + slashSide) * (recoilImpulse * scale);
            }

            if (killed)
                Die(info);
            return killed;
        }

        void Die(HitInfo info)
        {
            IsDead = true;
            if (m_Collider != null)
                m_Collider.enabled = false;
            All.Remove(this);
            m_DeathDirection = m_HitDirection;
            m_DeathTime = 0f;

            AttackData a = info.attack;
            HitFeelToggles t = HitFeelSettings.T;

            // 撃破の予兆: 目の光を消して「もう倒した」ことを即座に伝える
            if (t.deathTell && eyeRenderers != null)
            {
                foreach (Renderer eye in eyeRenderers)
                {
                    if (eye != null)
                        eye.enabled = false;
                }
            }

            if (Planted)
            {
                // かかしは吹き飛ばず、今傾いている向きのまま倒れる
                float wobbleAngle = Mathf.Min(m_Wobble.magnitude, weebleMaxTilt);
                if (wobbleAngle > 3f)
                    m_DeathDirection = m_Wobble / m_Wobble.magnitude;
                m_DeathStartAngle = wobbleAngle;
                m_DeathVelocity = Vector3.zero;
                m_DeathPhase = t.deathTell ? DeathPhase.Tell : DeathPhase.Topple;
                return;
            }

            bool launch = t.damageMotion && a.launchUpSpeed > 0f;
            if (launch || m_Airborne)
            {
                // 【4】ダメージモーション(吹き飛び):
                // 放物線で飛び、着地した瞬間にちょうど横倒しになるよう回転数を決める
                m_DeathVelocity = launch ? m_DeathDirection * a.launchSpeed + Vector3.up * a.launchUpSpeed : m_AirVelocity;
                float up = m_DeathVelocity.y;
                float height = Mathf.Max(transform.position.y, 0f);
                m_FlightDuration = Mathf.Max(0.1f, (up + Mathf.Sqrt(Mathf.Max(up * up + 2f * airGravity * height, 0f))) / airGravity);
                int spins = launch ? Mathf.Max(1, Mathf.RoundToInt(m_FlightDuration / 0.5f)) : 0;
                m_DeathSpinTotal = 90f + 360f * spins;
                // 空中で倒した時は、今の仰け反り角度から続けて倒れる(見た目が飛ばないように)
                m_DeathStartAngle = launch ? 0f : m_StaggerTilt;
                m_DeathPhase = DeathPhase.Airborne;
                m_Airborne = false;
            }
            else
            {
                m_DeathVelocity = m_KnockVelocity;
                m_DeathStartAngle = 0f;
                m_DeathPhase = t.deathTell ? DeathPhase.Tell : DeathPhase.Topple;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (IsDead)
            {
                UpdateDeath(dt);
                // 予兆の明滅は UpdateDeath 側で出すので、ここで上書きしない
                if (m_DeathPhase != DeathPhase.Tell)
                    UpdateFlash();
                return;
            }

            if (dt > 0f)
            {
                UpdateMovement(dt);
                UpdateReactionPose(dt);
            }
            UpdateShakeAndFlash();
        }

        void UpdateMovement(float dt)
        {
            if (Planted)
            {
                // かかし: 位置は動かさない(向きだけ元に戻す)
                if (m_HasHome)
                {
                    transform.position = new Vector3(m_HomePosition.x, 0f, m_HomePosition.z);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, m_HomeRotation, turnSpeed * 0.5f * dt);
                }
                m_KnockVelocity = Vector3.zero;
                m_Airborne = false;
                return;
            }

            Vector3 pos = transform.position;

            // ノックバック
            pos += m_KnockVelocity * dt;
            m_KnockVelocity *= Mathf.Exp(-knockbackDamping * dt);

            if (m_Airborne)
            {
                UpdateAirborne(pos, dt);
                return;
            }

            m_StaggerRemaining -= dt;
            bool staggered = m_StaggerRemaining > 0f;

            if (m_Player != null && !staggered)
            {
                Vector3 toPlayer = Ease.Flat(m_Player.position - pos);
                float distance = toPlayer.magnitude;
                if (distance > 0.01f)
                {
                    Quaternion look = Quaternion.LookRotation(toPlayer / distance);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);
                }
                if (distance > stopDistance)
                    pos += toPlayer / distance * (moveSpeed * dt);
            }

            // 敵同士が重ならないように押し合う
            foreach (Enemy other in All)
            {
                if (other == this)
                    continue;
                Vector3 diff = Ease.Flat(pos - other.transform.position);
                float d = diff.magnitude;
                if (d < separationRadius && d > 1e-4f)
                    pos += diff / d * ((separationRadius - d) * 4f * dt);
            }

            pos.y = 0f;
            transform.position = pos;
        }

        /// <summary>打ち上げ中: 放物線で飛び、強く落ちたらバウンドしてから着地する。</summary>
        void UpdateAirborne(Vector3 pos, float dt)
        {
            m_StaggerRemaining = Mathf.Max(m_StaggerRemaining, getUpDelay);
            pos += m_AirVelocity * dt;
            m_AirVelocity.y -= airGravity * dt;

            if (pos.y <= 0f && m_AirVelocity.y < 0f)
            {
                pos.y = 0f;
                if (-m_AirVelocity.y > bounceSpeedThreshold)
                {
                    // バウンド: 跳ね返りは弱く、水平方向は地面との摩擦で大きく失速
                    m_AirVelocity.y = -m_AirVelocity.y * bounceRestitution;
                    m_AirVelocity.x *= 0.45f;
                    m_AirVelocity.z *= 0.45f;
                }
                else
                {
                    m_Airborne = false;
                    m_KnockVelocity = Ease.Flat(m_AirVelocity) * 0.3f;
                    m_StaggerRemaining = getUpDelay;
                }
            }
            transform.position = pos;
        }

        static float TierValue(AttackTier tier, float light, float heavy, float finisher, float ultra)
        {
            switch (tier)
            {
                case AttackTier.Heavy: return heavy;
                case AttackTier.Finisher: return finisher;
                case AttackTier.Ultra: return ultra;
                default: return light;
            }
        }

        void UpdateReactionPose(float dt)
        {
            // のけぞり: 素早く倒れて、ゆっくり戻る
            float target = m_StaggerRemaining > 0f ? m_StaggerTarget : 0f;
            float sharpness = target > m_StaggerTilt ? 30f : 7f;
            m_StaggerTilt = Mathf.Lerp(m_StaggerTilt, target, 1f - Mathf.Exp(-sharpness * dt));

            // 加算リアクション: 減衰バネ
            Vector3 accel = -recoilStiffness * m_Recoil - recoilDamping * m_RecoilVelocity;
            m_RecoilVelocity += accel * dt;
            m_Recoil += m_RecoilVelocity * dt;
            m_Recoil.y = 0f;
            m_RecoilVelocity.y = 0f;

            // 起き上がりこぼし: 減衰の小さいバネで、何度か揺れ戻ってから立ち直る
            float omega = WeebleOmega;
            m_WobbleVelocity += (-omega * omega * m_Wobble - weebleDamping * m_WobbleVelocity) * dt;
            m_Wobble += m_WobbleVelocity * dt;
            m_Wobble.y = 0f;
            m_WobbleVelocity.y = 0f;
            float wobbleAngle = m_Wobble.magnitude;
            if (wobbleAngle > weebleMaxTilt)
            {
                // 倒れきらないよう止め、外向きの勢いを消す
                Vector3 dir = m_Wobble / wobbleAngle;
                m_Wobble = dir * weebleMaxTilt;
                m_WobbleVelocity -= dir * Mathf.Max(0f, Vector3.Dot(m_WobbleVelocity, dir));
            }
        }

        float WeebleOmega => Mathf.PI * 2f * Mathf.Max(0.05f, weebleFrequency);

        /// <summary>
        /// 起き上がりこぼしの姿勢を見た目に与える。底を半径 bodyRadius の半球とみなし、
        /// 半球の中心まわりに傾けつつ、傾いた分だけ転がって進む(底が地面に埋まらず、浮きもしない)。
        /// </summary>
        void ApplyRollingPose(Vector3 rollDirection, float angle, Quaternion extraTilt, Vector3 offset)
        {
            Quaternion tilt = Quaternion.AngleAxis(angle, Vector3.Cross(Vector3.up, rollDirection)) * extraTilt;
            Vector3 center = transform.position + Vector3.up * bodyRadius + rollDirection * (bodyRadius * angle * Mathf.Deg2Rad);
            visual.rotation = tilt * transform.rotation;
            visual.position = center - tilt * (Vector3.up * bodyRadius) + offset;
        }

        void UpdateShakeAndFlash()
        {
            if (visual == null)
                return;

            // 傾き: のけぞり(押された方向へ) + 加算リアクション
            Quaternion stagger = Quaternion.AngleAxis(m_StaggerTilt, Vector3.Cross(Vector3.up, m_HitDirection));
            Quaternion recoil = Quaternion.identity;
            float recoilAngle = Mathf.Min(m_Recoil.magnitude, 35f);
            if (recoilAngle > 0.01f)
                recoil = Quaternion.AngleAxis(recoilAngle, Vector3.Cross(Vector3.up, m_Recoil.normalized));
            if (!Planted)
                visual.rotation = stagger * recoil * transform.rotation;

            // モデルシェイク: 実時間で動くのでヒットストップ中でも揺れる
            Vector3 offset = Vector3.zero;
            float shakeElapsed = Time.unscaledTime - m_ShakeStart;
            if (shakeElapsed < m_ShakeDuration)
            {
                float fade = 1f - shakeElapsed / m_ShakeDuration;
                // カメラと平行な方向(画面の左右)に揺らすと一番よく見える
                Vector3 axis = ThirdPersonCamera.Instance != null ? ThirdPersonCamera.Instance.transform.right : transform.right;
                float wave = Mathf.Sin(shakeElapsed * modelShakeFrequency * Mathf.PI * 2f);
                offset = axis * (wave * m_ShakeAmplitude * fade);
            }
            // 出現: 地面からせり上がる
            m_Age += Time.deltaTime;
            float rise = 1f - Ease.OutCubic(m_Age / spawnRiseDuration);
            offset += Vector3.down * (2.2f * rise);

            if (Planted)
            {
                // 起き上がりこぼし: 揺れの傾き + 加算リアクションを、転がる姿勢で反映する
                float wobbleAngle = Mathf.Min(m_Wobble.magnitude, weebleMaxTilt);
                Vector3 rollDirection = wobbleAngle > 0.01f ? m_Wobble / m_Wobble.magnitude : m_HitDirection;
                ApplyRollingPose(rollDirection, wobbleAngle, recoil, offset);
            }
            else
            {
                // 大きく仰け反った時は、体が地面に埋まらないよう持ち上げる
                float bigTilt = Mathf.InverseLerp(15f, 45f, m_StaggerTilt);
                offset += Vector3.up * (bodyRadius * Mathf.Sin(Mathf.Clamp(m_StaggerTilt, 0f, 90f) * Mathf.Deg2Rad) * bigTilt);
                visual.position = transform.position + offset;
            }

            UpdateFlash();
        }

        void UpdateFlash()
        {
            float elapsed = Time.unscaledTime - m_FlashStart;
            float intensity = 0f;
            if (elapsed < flashDuration)
            {
                // 1 フレーム目はフルで光らせ、すぐ落とす
                float u = elapsed / flashDuration;
                intensity = m_FlashPeak * (1f - u) * (1f - u);
            }
            SetFlash(intensity);
        }

        void SetFlash(float intensity)
        {
            if (flashRenderers == null)
                return;
            foreach (Renderer r in flashRenderers)
            {
                r.GetPropertyBlock(m_Block);
                m_Block.SetFloat(s_FlashIntensityId, intensity);
                r.SetPropertyBlock(m_Block);
            }
        }

        // ------------------------------------------------------------------
        // 死亡: コテッと倒れて消える
        // ------------------------------------------------------------------

        void UpdateDeath(float dt)
        {
            if (visual == null)
            {
                Destroy(gameObject);
                return;
            }

            m_DeathTime += dt;
            Vector3 axis = Vector3.Cross(Vector3.up, m_DeathDirection);
            float angle = 0f;
            float lift = 0f;
            float sink = 0f;

            switch (m_DeathPhase)
            {
                case DeathPhase.Tell:
                {
                    // 撃破の予兆: その場で硬直し、2 回明滅してから倒れ始める
                    float u = Mathf.Clamp01(m_DeathTime / deathTellDuration);
                    SetFlash(Mathf.Sin(u * Mathf.PI * 4f) > 0f ? 1.6f : 0f);
                    float jitter = Mathf.Sin(Time.unscaledTime * 40f * Mathf.PI * 2f) * 0.025f * (1f - u);
                    Vector3 side = ThirdPersonCamera.Instance != null ? ThirdPersonCamera.Instance.transform.right : transform.right;
                    if (Planted)
                    {
                        // かかしは傾いたまま固まる
                        ApplyRollingPose(m_DeathDirection, m_DeathStartAngle, Quaternion.identity, side * jitter);
                    }
                    else
                    {
                        angle = m_StaggerTilt * (1f - u);
                        lift = bodyRadius * Mathf.Sin(Mathf.Clamp(angle, 0f, 90f) * Mathf.Deg2Rad) * Mathf.InverseLerp(15f, 45f, angle);
                        visual.rotation = Quaternion.AngleAxis(angle, axis) * transform.rotation;
                        visual.position = transform.position + Vector3.up * lift + side * jitter;
                    }
                    if (u >= 1f)
                    {
                        SetFlash(0f);
                        m_DeathPhase = DeathPhase.Topple;
                        m_DeathTime = 0f;
                    }
                    return;
                }
                case DeathPhase.Airborne:
                {
                    Vector3 pos = transform.position + m_DeathVelocity * dt;
                    m_DeathVelocity.y -= airGravity * dt;
                    // n 回転 + 90 度 = 着地した瞬間に横倒し
                    float u = Mathf.Clamp01(m_DeathTime / m_FlightDuration);
                    angle = Mathf.Lerp(m_DeathStartAngle, m_DeathSpinTotal, Ease.OutQuad(u));
                    lift = bodyRadius * Mathf.Sin(Mathf.Min(angle, 90f) * Mathf.Deg2Rad);
                    if (pos.y <= 0f && m_DeathVelocity.y < 0f)
                    {
                        pos.y = 0f;
                        m_DeathVelocity = Ease.Flat(m_DeathVelocity) * 0.25f;
                        m_DeathPhase = DeathPhase.Lie;
                        m_DeathTime = 0f;
                    }
                    transform.position = pos;
                    break;
                }
                case DeathPhase.Topple:
                {
                    // 重力で倒れるように加速(ease-in)
                    float u = Mathf.Clamp01(m_DeathTime / toppleDuration);
                    angle = Mathf.Lerp(m_DeathStartAngle, 90f, Ease.InQuad(u));
                    lift = bodyRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
                    SlideAndDamp(dt);
                    if (u >= 1f)
                    {
                        m_DeathPhase = DeathPhase.Lie;
                        m_DeathTime = 0f;
                    }
                    break;
                }
                case DeathPhase.Lie:
                {
                    // 着地の小さなバウンド = 「コテッ」
                    float bounce = m_DeathTime < 0.2f ? Mathf.Sin(m_DeathTime / 0.2f * Mathf.PI) * 9f : 0f;
                    angle = 90f - bounce;
                    lift = bodyRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
                    SlideAndDamp(dt);
                    if (m_DeathTime >= lieDuration)
                    {
                        m_DeathPhase = DeathPhase.Vanish;
                        m_DeathTime = 0f;
                    }
                    break;
                }
                case DeathPhase.Vanish:
                {
                    // 沈みながら縮んで消える
                    float u = Mathf.Clamp01(m_DeathTime / vanishDuration);
                    angle = 90f;
                    lift = bodyRadius * (1f - u * 1.5f);
                    sink = bodyRadius * 1.5f * u;
                    visual.localScale = m_VisualBaseScale * (1f - Ease.InQuad(u));
                    if (u >= 1f)
                    {
                        Died?.Invoke(this);
                        Destroy(gameObject);
                        return;
                    }
                    break;
                }
            }

            if (Planted)
            {
                ApplyRollingPose(m_DeathDirection, angle, Quaternion.identity, Vector3.down * sink);
                return;
            }
            visual.rotation = Quaternion.AngleAxis(angle, axis) * transform.rotation;
            visual.position = transform.position + Vector3.up * lift;
        }

        void SlideAndDamp(float dt)
        {
            Vector3 pos = transform.position + Ease.Flat(m_DeathVelocity) * dt;
            m_DeathVelocity *= Mathf.Exp(-6f * dt);
            pos.y = 0f;
            transform.position = pos;
        }
    }
}
