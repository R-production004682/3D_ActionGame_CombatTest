using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ActionStudy
{
    /// <summary>
    /// 金属を斬った時のスパーク(火花)エフェクト。流血表現の代わりに、金属を斬るスパークで「斬った」手応えを出す。
    ///
    /// 複数のレイヤーを重ねてフォトリアルに見せる:
    ///  1. Sparks  : 高速で飛び散る火花の筋。重力で落ち、床で跳ねる。白熱 → 橙 → 赤 と冷えていく
    ///  2. Embers  : ゆっくり漂う火の粉。余韻
    ///  3. Flash   : 着弾点の一瞬の閃光
    ///  4. Flare   : 横長のアナモルフィックフレア(カメラのレンズっぽさ)
    ///  5. Slash   : 剣筋方向の光の線
    ///  6. Ring    : 衝撃波リング
    ///  7. Debris  : 金属片(メッシュ)。床で跳ねる
    ///  8. Smoke   : 金属が焼けた薄い煙
    ///  9. Light   : 着弾点を照らすポイントライト
    ///
    /// 攻撃の格(弱/強/最強)ごとに SparkTierProfile で量・速度・種類を出し分ける。
    /// パーティクルは ParticleSystem.Emit で 1 粒ずつ速度を与えて出すので、「斬撃方向に飛ばす」といった制御ができる。
    /// </summary>
    public class HitSparkSystem : MonoBehaviour
    {
        [Serializable]
        public class SparkTierProfile
        {
            public string label = "弱";
            [Header("火花")]
            public int sparkCount = 16;
            public Vector2 sparkSpeed = new Vector2(4f, 11f);
            public Vector2 sparkLifetime = new Vector2(0.12f, 0.35f);
            public Vector2 sparkSize = new Vector2(0.018f, 0.035f);
            [Tooltip("斬撃方向を中心に火花が広がる角度")]
            public float sparkSpread = 40f;
            [Tooltip("一拍遅れて吹き出す二次スパークの数 (0 で無し)")]
            public int secondaryBurstCount;
            public float secondaryBurstDelay = 0.05f;
            [Header("閃光・線")]
            public float flashSize = 0.6f;
            public float flareWidth = 1.2f;
            public float slashWidth = 0.06f;
            [Header("強攻撃以上の追加要素")]
            public int ringCount;
            public float ringSize = 2.2f;
            public int emberCount = 4;
            public int debrisCount;
            public int smokeCount;
            [Header("ライト")]
            [Tooltip("着弾ライトの明るさ (lm)。昼光下なので大きな値が必要")]
            public float lightLumen = 4e5f;
            public float lightDuration = 0.1f;
            public Color lightColor = new Color(1f, 0.75f, 0.45f);
        }

        [Header("マテリアル")]
        public Material sparkMaterial;
        public Material emberMaterial;
        public Material flashMaterial;
        public Material flareMaterial;
        public Material slashMaterial;
        public Material ringMaterial;
        public Material smokeMaterial;
        public Material debrisMaterial;
        public Mesh debrisMesh;

        [Tooltip("火花や破片が跳ねる床。未設定なら y=0 の平面を使う")]
        public Transform groundPlane;

        [Header("攻撃の格ごとの設定 (弱 / 強 / 最強 / 超強)")]
        public SparkTierProfile lightTier = CreateLight();
        public SparkTierProfile heavyTier = CreateHeavy();
        public SparkTierProfile finisherTier = CreateFinisher();
        public SparkTierProfile ultraTier = CreateUltra();

        [Header("溜め (超強)")]
        [Tooltip("溜め終わりの時の、1 秒あたりに集まる光の粒の数")]
        public float chargeParticleRate = 140f;
        public Color chargeColor = new Color(0.6f, 0.8f, 1f, 1f);

        ParticleSystem m_Sparks;
        ParticleSystem m_Embers;
        ParticleSystem m_Flash;
        ParticleSystem m_Flare;
        ParticleSystem m_Slash;
        ParticleSystem m_Ring;
        ParticleSystem m_Debris;
        ParticleSystem m_Smoke;
        ParticleSystem m_GroundRing;
        ParticleSystem m_GroundDust;
        ParticleSystem m_Charge;
        float m_ChargeAccumulator;

        const int k_LightPoolSize = 4;
        Light[] m_Lights;
        float[] m_LightStartTimes;
        float[] m_LightDurations;
        float[] m_LightPeaks;
        int m_NextLight;

        void Awake()
        {
            if (groundPlane == null)
            {
                var ground = new GameObject("SparkGroundPlane");
                ground.transform.SetParent(transform, false);
                ground.transform.position = Vector3.zero;
                groundPlane = ground.transform;
            }

            if (debrisMesh == null)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                debrisMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                Destroy(cube);
            }

            m_Sparks = CreateSparks();
            m_Embers = CreateEmbers();
            m_Flash = CreateFlash();
            m_Flare = CreateFlare();
            m_Slash = CreateSlash();
            m_Ring = CreateRing();
            m_Debris = CreateDebris();
            m_Smoke = CreateSmoke();
            m_GroundRing = CreateGroundRing();
            m_GroundDust = CreateGroundDust();
            m_Charge = CreateCharge();
            CreateLightPool();
        }

        SparkTierProfile GetProfile(AttackTier tier)
        {
            switch (tier)
            {
                case AttackTier.Heavy: return heavyTier;
                case AttackTier.Finisher: return finisherTier;
                case AttackTier.Ultra: return ultraTier;
                default: return lightTier;
            }
        }

        static bool IsBlueTier(AttackTier tier)
        {
            return tier == AttackTier.Finisher || tier == AttackTier.Ultra;
        }

        // ------------------------------------------------------------------
        // 溜め・キラッ・叩きつけ (超強)
        // ------------------------------------------------------------------

        /// <summary>溜め: 周囲から剣へ光の粒が吸い込まれ、剣先の光が育っていく。毎フレーム呼ぶ。</summary>
        public void EmitCharge(Vector3 bladeBase, Vector3 bladeTip, float progress)
        {
            // 集まる光の粒(溜まるほど多く、内側から)
            m_ChargeAccumulator += Mathf.Lerp(0.15f, 1f, progress) * chargeParticleRate * Time.deltaTime;
            while (m_ChargeAccumulator >= 1f)
            {
                m_ChargeAccumulator -= 1f;
                Vector3 target = Vector3.Lerp(bladeBase, bladeTip, UnityEngine.Random.Range(0.3f, 1f));
                float radius = Mathf.Lerp(1.6f, 1f, progress) * UnityEngine.Random.Range(0.7f, 1f);
                const float life = 0.22f;
                Vector3 from = target + UnityEngine.Random.onUnitSphere * radius;
                Color c = chargeColor;
                c.a = Mathf.Lerp(0.5f, 1f, progress);
                m_Charge.Emit(new ParticleSystem.EmitParams
                {
                    position = from,
                    velocity = (target - from) / life,
                    startLifetime = life,
                    startSize = UnityEngine.Random.Range(0.025f, 0.045f),
                    startColor = c,
                }, 1);
            }

            // 剣先の光(毎フレーム短い寿命で出し直して、育っていく光に見せる)
            Color glow = chargeColor;
            glow.a = Mathf.Lerp(0.3f, 0.9f, progress);
            m_Flash.Emit(new ParticleSystem.EmitParams
            {
                position = bladeTip,
                startSize = Mathf.Lerp(0.2f, 0.75f, progress * progress),
                startLifetime = 0.05f,
                startColor = glow,
                rotation = UnityEngine.Random.Range(0f, 360f),
            }, 1);
        }

        /// <summary>振り下ろす直前の「キラッ」: 剣先に十字の光。</summary>
        public void EmitGlint(Vector3 position)
        {
            m_Flash.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                startSize = 0.5f,
                startLifetime = 0.12f,
                startColor = Color.white,
            }, 1);
            m_Flare.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                startSize3D = new Vector3(2f, 0.06f, 1f),
                startLifetime = 0.14f,
                startColor = Color.white,
            }, 1);
            m_Flare.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                startSize3D = new Vector3(1.1f, 0.05f, 1f),
                rotation = 90f,
                startLifetime = 0.14f,
                startColor = Color.white,
            }, 1);
        }

        /// <summary>
        /// 地面への叩きつけ。地面を這う衝撃波リング + 外へ流れる砂煙 + 火花の噴き上げ +
        /// 地割れの光 + 破片 + 閃光 + ライト。敵に当たらなくても出す。
        /// </summary>
        public void PlayGroundSlam(Vector3 point, float radius)
        {
            SparkTierProfile p = ultraTier;
            Vector3 ground = new Vector3(point.x, 0.05f, point.z);

            // 地面を這う衝撃波リング(地面と平行) × 2
            m_GroundRing.Emit(new ParticleSystem.EmitParams
            {
                position = ground,
                startSize = radius * 2.3f,
                startLifetime = 0.35f,
                startColor = new Color(1f, 0.85f, 0.65f, 1f),
                rotation = UnityEngine.Random.Range(0f, 360f),
            }, 1);
            m_GroundRing.Emit(new ParticleSystem.EmitParams
            {
                position = ground,
                startSize = radius * 3.2f,
                startLifetime = 0.55f,
                startColor = new Color(0.6f, 0.8f, 1f, 0.6f),
                rotation = UnityEngine.Random.Range(0f, 360f),
            }, 1);

            // 外へ流れる砂煙
            const int dustCount = 28;
            for (int i = 0; i < dustCount; i++)
            {
                float angle = (i + UnityEngine.Random.value * 0.6f) / dustCount * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                m_GroundDust.Emit(new ParticleSystem.EmitParams
                {
                    position = ground + outward * 0.6f + Vector3.up * 0.2f,
                    velocity = outward * UnityEngine.Random.Range(6f, 11f) + Vector3.up * UnityEngine.Random.Range(0.3f, 1.2f),
                    startSize = UnityEngine.Random.Range(0.7f, 1.3f),
                    startLifetime = UnityEngine.Random.Range(0.9f, 1.5f),
                    rotation = UnityEngine.Random.Range(0f, 360f),
                    startColor = new Color(0.62f, 0.58f, 0.52f, 0.5f),
                }, 1);
            }

            // 火花の噴き上げ + 地面を這う火花
            EmitSparks(p, p.sparkCount, ground, Vector3.up, Vector3.up, 1f);
            for (int i = 0; i < 40; i++)
            {
                Vector3 outward = UnityEngine.Random.insideUnitCircle.normalized;
                outward = new Vector3(outward.x, 0.12f, outward.y).normalized;
                m_Sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = ground,
                    velocity = outward * UnityEngine.Random.Range(8f, 18f),
                    startLifetime = UnityEngine.Random.Range(0.25f, 0.6f),
                    startSize = UnityEngine.Random.Range(p.sparkSize.x, p.sparkSize.y),
                    startColor = Color.white,
                }, 1);
            }

            // 地割れの光: 放射状に伸びる光の筋
            const int crackCount = 9;
            for (int i = 0; i < crackCount; i++)
            {
                float angle = (i + UnityEngine.Random.value * 0.5f) / crackCount * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                m_Slash.Emit(new ParticleSystem.EmitParams
                {
                    position = ground + outward * UnityEngine.Random.Range(0.8f, 1.6f),
                    velocity = outward * 0.001f,
                    startSize = UnityEngine.Random.Range(0.08f, 0.14f),
                    startLifetime = UnityEngine.Random.Range(0.35f, 0.6f),
                    startColor = new Color(1f, 0.7f, 0.4f, 1f),
                }, 1);
            }

            EmitDebris(p, ground, Vector3.up, Vector3.up);
            EmitEmbers(p, ground, Vector3.up, Vector3.up);

            // 閃光(地面すれすれの大きな光 + 横長フレア)
            m_Flash.Emit(new ParticleSystem.EmitParams
            {
                position = ground + Vector3.up * 0.3f,
                startSize = p.flashSize * 2f,
                startLifetime = 0.14f,
                startColor = new Color(0.75f, 0.85f, 1f, 1f),
            }, 1);
            m_Flare.Emit(new ParticleSystem.EmitParams
            {
                position = ground + Vector3.up * 0.3f,
                startSize3D = new Vector3(p.flareWidth * 1.3f, p.flareWidth * 0.04f, 1f),
                startLifetime = 0.16f,
                startColor = new Color(0.7f, 0.85f, 1f, 1f),
            }, 1);

            FireLight(p, ground + Vector3.up * 0.8f);
        }

        // ------------------------------------------------------------------
        // 移動 (追加演出: 足元の砂煙)
        // ------------------------------------------------------------------

        /// <summary>
        /// 足元の砂煙。走り出し・切り返し・急停止で地面を蹴った方向へ土煙を飛ばす。
        /// strength は 0-1 (足取りの小さな煙は 0.2 程度)。
        /// </summary>
        public void EmitMoveDust(Vector3 footPosition, Vector3 kickDirection, float strength)
        {
            Vector3 ground = new Vector3(footPosition.x, 0.08f, footPosition.z);
            Vector3 kick = Ease.Flat(kickDirection);
            kick = kick.sqrMagnitude > 1e-4f ? kick.normalized : Vector3.zero;
            Vector3 side = Vector3.Cross(Vector3.up, kick);
            int count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(2f, 9f, strength)));
            for (int i = 0; i < count; i++)
            {
                float spread = UnityEngine.Random.Range(-1f, 1f);
                Vector3 velocity = kick * UnityEngine.Random.Range(1.5f, 4f) * strength
                                   + side * (spread * 1.6f * strength)
                                   + Vector3.up * UnityEngine.Random.Range(0.2f, 0.9f);
                m_GroundDust.Emit(new ParticleSystem.EmitParams
                {
                    position = ground + side * (spread * 0.25f) + UnityEngine.Random.insideUnitSphere * 0.08f,
                    velocity = velocity,
                    startSize = UnityEngine.Random.Range(0.25f, 0.5f) * Mathf.Lerp(0.8f, 1.4f, strength),
                    startLifetime = UnityEngine.Random.Range(0.45f, 0.8f),
                    rotation = UnityEngine.Random.Range(0f, 360f),
                    startColor = new Color(0.62f, 0.58f, 0.52f, Mathf.Lerp(0.18f, 0.4f, strength)),
                }, 1);
            }
        }

        // ------------------------------------------------------------------
        // 再生
        // ------------------------------------------------------------------

        /// <summary>【1】ヒットエフェクト。全レイヤーを攻撃の格に応じて出す。</summary>
        public void Play(HitInfo info)
        {
            if (!HitFeelSettings.T.hitEffect)
                return;

            AttackTier tier = info.attack.tier;
            SparkTierProfile p = GetProfile(tier);
            Vector3 point = info.point;
            Vector3 normal = info.normal;
            Vector3 slash = info.slashDirection;

            EmitSparks(p, p.sparkCount, point, normal, slash, 1f);
            if (p.secondaryBurstCount > 0)
                StartCoroutine(SecondaryBurst(p, point, normal));
            EmitFlash(p, point, tier);
            EmitSlash(p, point, slash, tier);
            if (p.ringCount > 0)
                StartCoroutine(EmitRings(p, point));
            EmitEmbers(p, point, normal, slash);
            EmitDebris(p, point, normal, slash);
            EmitSmoke(p, point, normal);
            FireLight(p, point + normal * 0.3f);
        }

        IEnumerator SecondaryBurst(SparkTierProfile p, Vector3 point, Vector3 normal)
        {
            // 一拍遅れて「噴き出す」二次スパーク。上向きの噴水状にして量感を出す
            yield return new WaitForSecondsRealtime(p.secondaryBurstDelay);
            Vector3 fountainDir = (normal * 0.6f + Vector3.up * 0.8f).normalized;
            EmitSparks(p, p.secondaryBurstCount, point, fountainDir, fountainDir, 0.75f);
        }

        IEnumerator EmitRings(SparkTierProfile p, Vector3 point)
        {
            for (int i = 0; i < p.ringCount; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = Vector3.zero,
                    startSize = p.ringSize * (1f + i * 0.45f),
                    startLifetime = 0.22f + i * 0.06f,
                    startColor = i == 0 ? new Color(1f, 0.9f, 0.75f, 1f) : new Color(0.7f, 0.85f, 1f, 0.8f),
                    rotation = UnityEngine.Random.Range(0f, 360f),
                };
                m_Ring.Emit(ep, 1);
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        void EmitSparks(SparkTierProfile p, int count, Vector3 point, Vector3 normal, Vector3 slash, float speedScale)
        {
            // 斬撃方向と面の法線の間を「火花が飛ぶ主方向」にする
            Vector3 mainDir = (slash * 0.75f + normal * 0.55f).normalized;
            for (int i = 0; i < count; i++)
            {
                // 主方向に近いものほど速く(円錐の外側は遅い) → 鋭い指向性
                float speedRand = Mathf.Sqrt(UnityEngine.Random.value);
                float spreadRand = UnityEngine.Random.value;
                Vector3 dir = RandomInCone(mainDir, p.sparkSpread * spreadRand);
                speedRand *= Mathf.Lerp(1f, 0.55f, spreadRand);

                float speed = Mathf.Lerp(p.sparkSpeed.x, p.sparkSpeed.y, speedRand) * speedScale;
                // 白〜淡い黄色の範囲で色をばらつかせる(全部同じ色だと CG っぽくなる)
                Color c = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.55f), UnityEngine.Random.value);
                var ep = new ParticleSystem.EmitParams
                {
                    position = point + UnityEngine.Random.insideUnitSphere * 0.04f,
                    velocity = dir * speed,
                    startLifetime = UnityEngine.Random.Range(p.sparkLifetime.x, p.sparkLifetime.y),
                    startSize = UnityEngine.Random.Range(p.sparkSize.x, p.sparkSize.y),
                    startColor = c,
                };
                m_Sparks.Emit(ep, 1);
            }
        }

        void EmitEmbers(SparkTierProfile p, Vector3 point, Vector3 normal, Vector3 slash)
        {
            Vector3 mainDir = (slash * 0.5f + normal * 0.5f + Vector3.up * 0.4f).normalized;
            for (int i = 0; i < p.emberCount; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = point + UnityEngine.Random.insideUnitSphere * 0.1f,
                    velocity = RandomInCone(mainDir, 70f) * UnityEngine.Random.Range(0.6f, 3.2f),
                    startLifetime = UnityEngine.Random.Range(0.6f, 1.4f),
                    startSize = UnityEngine.Random.Range(0.02f, 0.045f),
                    startColor = Color.Lerp(new Color(1f, 0.8f, 0.45f), new Color(1f, 0.55f, 0.25f), UnityEngine.Random.value),
                };
                m_Embers.Emit(ep, 1);
            }
        }

        void EmitFlash(SparkTierProfile p, Vector3 point, AttackTier tier)
        {
            // コア: 小さく強い白 + 外側: 大きく弱い色付きの光。2 層にすると光源らしく見える
            m_Flash.Emit(new ParticleSystem.EmitParams
            {
                position = point,
                startSize = p.flashSize * 0.55f,
                startLifetime = 0.06f,
                startColor = Color.white,
                rotation = UnityEngine.Random.Range(0f, 360f),
            }, 1);
            Color outer = IsBlueTier(tier) ? new Color(0.6f, 0.75f, 1f, 0.9f) : new Color(1f, 0.75f, 0.45f, 0.8f);
            m_Flash.Emit(new ParticleSystem.EmitParams
            {
                position = point,
                startSize = p.flashSize * 1.6f,
                startLifetime = 0.11f,
                startColor = outer,
                rotation = UnityEngine.Random.Range(0f, 360f),
            }, 1);

            m_Flare.Emit(new ParticleSystem.EmitParams
            {
                position = point,
                startSize3D = new Vector3(p.flareWidth, p.flareWidth * 0.045f, 1f),
                startLifetime = 0.09f,
                startColor = IsBlueTier(tier) ? new Color(0.65f, 0.8f, 1f, 1f) : new Color(1f, 0.92f, 0.8f, 1f),
            }, 1);
        }

        void EmitSlash(SparkTierProfile p, Vector3 point, Vector3 slash, AttackTier tier)
        {
            // 速度をごく小さく与え、Stretched Billboard を剣筋の向きに伸ばす(移動はほぼしない)
            m_Slash.Emit(new ParticleSystem.EmitParams
            {
                position = point,
                velocity = slash * 0.001f,
                startSize = p.slashWidth,
                startLifetime = tier == AttackTier.Light ? 0.12f : 0.18f,
                startColor = new Color(0.85f, 0.95f, 1f, 1f),
            }, 1);
        }

        void EmitDebris(SparkTierProfile p, Vector3 point, Vector3 normal, Vector3 slash)
        {
            Vector3 mainDir = (normal * 0.6f + slash * 0.4f + Vector3.up * 0.5f).normalized;
            for (int i = 0; i < p.debrisCount; i++)
            {
                float size = UnityEngine.Random.Range(0.025f, 0.07f);
                m_Debris.Emit(new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = RandomInCone(mainDir, 50f) * UnityEngine.Random.Range(2.5f, 6.5f),
                    startLifetime = UnityEngine.Random.Range(1.2f, 2f),
                    startSize3D = new Vector3(size, size * UnityEngine.Random.Range(0.15f, 0.4f), size * UnityEngine.Random.Range(0.6f, 1.4f)),
                    rotation3D = UnityEngine.Random.insideUnitSphere * 180f,
                    angularVelocity3D = UnityEngine.Random.insideUnitSphere * 900f,
                    startColor = Color.white,
                }, 1);
            }
        }

        void EmitSmoke(SparkTierProfile p, Vector3 point, Vector3 normal)
        {
            for (int i = 0; i < p.smokeCount; i++)
            {
                m_Smoke.Emit(new ParticleSystem.EmitParams
                {
                    position = point + UnityEngine.Random.insideUnitSphere * 0.15f,
                    velocity = (normal * 0.4f + Vector3.up * 0.5f + UnityEngine.Random.insideUnitSphere * 0.3f),
                    startLifetime = UnityEngine.Random.Range(0.8f, 1.4f),
                    startSize = UnityEngine.Random.Range(0.4f, 0.8f),
                    rotation = UnityEngine.Random.Range(0f, 360f),
                    startColor = new Color(0.55f, 0.52f, 0.5f, 0.35f),
                }, 1);
            }
        }

        static Vector3 RandomInCone(Vector3 axis, float angleDegrees)
        {
            Vector3 random = UnityEngine.Random.onUnitSphere;
            Vector3 perpendicular = Vector3.Cross(axis, random);
            if (perpendicular.sqrMagnitude < 1e-6f)
                perpendicular = Vector3.Cross(axis, Vector3.up);
            float angle = UnityEngine.Random.Range(0f, angleDegrees);
            return Quaternion.AngleAxis(angle, perpendicular.normalized) * axis;
        }

        // ------------------------------------------------------------------
        // ライト
        // ------------------------------------------------------------------

        void CreateLightPool()
        {
            m_Lights = new Light[k_LightPoolSize];
            m_LightStartTimes = new float[k_LightPoolSize];
            m_LightDurations = new float[k_LightPoolSize];
            m_LightPeaks = new float[k_LightPoolSize];
            for (int i = 0; i < k_LightPoolSize; i++)
            {
                var go = new GameObject("HitLight_" + i);
                go.transform.SetParent(transform, false);
                Light l = go.AddComponent<Light>();
                if (!go.TryGetComponent(out HDAdditionalLightData _))
                    go.AddComponent<HDAdditionalLightData>();
                l.type = LightType.Point;
                l.lightUnit = LightUnit.Lumen;
                l.range = 7f;
                l.shadows = LightShadows.None;
                l.intensity = 0f;
                l.enabled = false;
                m_Lights[i] = l;
            }
        }

        void FireLight(SparkTierProfile p, Vector3 position)
        {
            int i = m_NextLight;
            m_NextLight = (m_NextLight + 1) % k_LightPoolSize;
            Light l = m_Lights[i];
            l.transform.position = position;
            l.color = p.lightColor;
            l.enabled = true;
            m_LightStartTimes[i] = Time.unscaledTime;
            m_LightDurations[i] = p.lightDuration;
            m_LightPeaks[i] = p.lightLumen;
        }

        void Update()
        {
            for (int i = 0; i < k_LightPoolSize; i++)
            {
                Light l = m_Lights[i];
                if (!l.enabled)
                    continue;
                float u = (Time.unscaledTime - m_LightStartTimes[i]) / m_LightDurations[i];
                if (u >= 1f)
                {
                    l.enabled = false;
                    continue;
                }
                // 小刻みに揺らぎながら減衰(火花の明滅)
                float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(i * 7.1f, Time.unscaledTime * 60f);
                l.intensity = m_LightPeaks[i] * (1f - u) * (1f - u) * flicker;
            }
        }

        // ------------------------------------------------------------------
        // パーティクルシステムの生成
        // ------------------------------------------------------------------

        ParticleSystem CreateSystem(string systemName, Material material, int maxParticles)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startSpeed = 0f;
            // ヒットストップ中もエフェクトだけは等倍で動かす(止まった世界で火花だけが散る)
            main.useUnscaledTime = true;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            return ps;
        }

        void AddGroundCollision(ParticleSystem ps, float bounce, float dampen, float lifetimeLoss)
        {
            ParticleSystem.CollisionModule collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.AddPlane(groundPlane);
            collision.bounce = bounce;
            collision.dampen = dampen;
            collision.lifetimeLoss = lifetimeLoss;
            collision.radiusScale = 0.5f;
        }

        static Gradient MakeGradient(Color[] colors, float[] colorTimes, float[] alphas, float[] alphaTimes)
        {
            var colorKeys = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                colorKeys[i] = new GradientColorKey(colors[i], colorTimes[i]);
            var alphaKeys = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++)
                alphaKeys[i] = new GradientAlphaKey(alphas[i], alphaTimes[i]);
            var g = new Gradient();
            g.SetKeys(colorKeys, alphaKeys);
            return g;
        }

        static AnimationCurve Curve(params float[] timeValuePairs)
        {
            var keys = new Keyframe[timeValuePairs.Length / 2];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new Keyframe(timeValuePairs[i * 2], timeValuePairs[i * 2 + 1]);
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
                curve.SmoothTangents(i, 0f);
            return curve;
        }

        ParticleSystem CreateSparks()
        {
            ParticleSystem ps = CreateSystem("Sparks", sparkMaterial, 3000);
            ParticleSystem.MainModule main = ps.main;
            main.gravityModifier = 1.1f;

            // 白熱(白) → 黄 → 橙 → 暗い赤 と冷えていく金属の色
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new[] { Color.white, new Color(1f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.12f), new Color(0.6f, 0.08f, 0.02f) },
                new[] { 0f, 0.2f, 0.55f, 1f },
                new[] { 1f, 1f, 0f },
                new[] { 0f, 0.7f, 1f });

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 1f, 1f, 0.35f));

            // 空気抵抗: 速いほど強く減速する → 最初だけ鋭く飛ぶ
            ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 100f;
            drag.drag = 1.6f;
            drag.multiplyDragByParticleVelocity = true;
            drag.multiplyDragByParticleSize = false;

            AddGroundCollision(ps, 0.35f, 0.45f, 0.25f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 2f;
            renderer.cameraVelocityScale = 0f;
            return ps;
        }

        ParticleSystem CreateEmbers()
        {
            ParticleSystem ps = CreateSystem("Embers", emberMaterial, 1000);
            ParticleSystem.MainModule main = ps.main;
            main.gravityModifier = 0.25f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new[] { new Color(1f, 0.85f, 0.5f), new Color(1f, 0.4f, 0.1f), new Color(0.5f, 0.05f, 0f) },
                new[] { 0f, 0.5f, 1f },
                new[] { 0f, 1f, 1f, 0f },
                new[] { 0f, 0.05f, 0.6f, 1f });

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 1.5f;
            noise.scrollSpeed = 0.6f;
            noise.damping = true;

            ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 100f;
            drag.drag = 2.5f;

            AddGroundCollision(ps, 0.2f, 0.6f, 0.3f);
            return ps;
        }

        ParticleSystem CreateFlash()
        {
            ParticleSystem ps = CreateSystem("Flash", flashMaterial, 64);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new[] { Color.white, Color.white },
                new[] { 0f, 1f },
                new[] { 1f, 0.6f, 0f },
                new[] { 0f, 0.4f, 1f });
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.7f, 0.3f, 1f, 1f, 1.15f));
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = -10f;
            return ps;
        }

        ParticleSystem CreateFlare()
        {
            ParticleSystem ps = CreateSystem("Flare", flareMaterial, 32);
            ParticleSystem.MainModule main = ps.main;
            main.startSize3D = true;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new[] { Color.white, Color.white },
                new[] { 0f, 1f },
                new[] { 1f, 0f },
                new[] { 0f, 1f });
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.6f, 1f, 1.3f));
            size.y = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 1f, 1f, 0.3f));
            size.z = new ParticleSystem.MinMaxCurve(1f);
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = -20f;
            return ps;
        }

        ParticleSystem CreateSlash()
        {
            ParticleSystem ps = CreateSystem("SlashMark", slashMaterial, 32);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new[] { Color.white, new Color(0.6f, 0.8f, 1f) },
                new[] { 0f, 1f },
                new[] { 1f, 0f },
                new[] { 0f, 1f });
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 1f, 1f, 0.2f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0f;
            renderer.lengthScale = 16f;
            renderer.cameraVelocityScale = 0f;
            renderer.sortingFudge = -5f;
            return ps;
        }

        ParticleSystem CreateRing()
        {
            ParticleSystem ps = CreateSystem("ShockRing", ringMaterial, 16);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new[] { Color.white, Color.white },
                new[] { 0f, 1f },
                new[] { 1f, 0.5f, 0f },
                new[] { 0f, 0.4f, 1f });
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.15f, 0.35f, 0.8f, 1f, 1f));
            return ps;
        }

        ParticleSystem CreateDebris()
        {
            ParticleSystem ps = CreateSystem("MetalDebris", debrisMaterial, 300);
            ParticleSystem.MainModule main = ps.main;
            main.gravityModifier = 2f;
            main.startSize3D = true;
            main.startRotation3D = true;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 1f, 0.85f, 1f, 1f, 0f));

            AddGroundCollision(ps, 0.4f, 0.35f, 0f);
            ParticleSystem.CollisionModule collision = ps.collision;
            collision.radiusScale = 0.3f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = debrisMesh;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return ps;
        }

        ParticleSystem CreateSmoke()
        {
            ParticleSystem ps = CreateSystem("Smoke", smokeMaterial, 200);
            ParticleSystem.MainModule main = ps.main;
            main.gravityModifier = -0.02f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new[] { Color.white, Color.white },
                new[] { 0f, 1f },
                new[] { 0f, 1f, 0f },
                new[] { 0f, 0.15f, 1f });
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.5f, 1f, 1.8f));
            ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 100f;
            drag.drag = 1.5f;
            return ps;
        }

        ParticleSystem CreateGroundRing()
        {
            // 地面と平行に広がる衝撃波リング
            ParticleSystem ps = CreateSystem("GroundShockRing", ringMaterial, 16);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new[] { Color.white, Color.white },
                new[] { 0f, 1f },
                new[] { 1f, 0.6f, 0f },
                new[] { 0f, 0.5f, 1f });
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.05f, 0.3f, 0.75f, 1f, 1f));
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            return ps;
        }

        ParticleSystem CreateGroundDust()
        {
            // 叩きつけで外へ流れる砂煙。強い空気抵抗で急に失速し、その場で広がって消える
            ParticleSystem ps = CreateSystem("GroundDust", smokeMaterial, 200);
            ParticleSystem.MainModule main = ps.main;
            main.gravityModifier = -0.01f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new[] { Color.white, Color.white },
                new[] { 0f, 1f },
                new[] { 0f, 1f, 0f },
                new[] { 0f, 0.08f, 1f });
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.4f, 0.3f, 1.3f, 1f, 2.4f));
            ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 100f;
            drag.drag = 4f;
            return ps;
        }

        ParticleSystem CreateCharge()
        {
            // 溜めで剣に集まる光の粒。吸い込まれるほど明るくなる
            ParticleSystem ps = CreateSystem("ChargeParticles", emberMaterial, 400);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new[] { Color.white, Color.white },
                new[] { 0f, 1f },
                new[] { 0f, 1f, 1f },
                new[] { 0f, 0.4f, 1f });
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.5f, 1f, 1.2f));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            renderer.cameraVelocityScale = 0f;
            return ps;
        }

        // ------------------------------------------------------------------
        // 既定値
        // ------------------------------------------------------------------

        static SparkTierProfile CreateLight()
        {
            return new SparkTierProfile
            {
                label = "弱",
                sparkCount = 18, sparkSpeed = new Vector2(4f, 11f), sparkLifetime = new Vector2(0.12f, 0.35f),
                sparkSize = new Vector2(0.018f, 0.032f), sparkSpread = 38f,
                flashSize = 0.6f, flareWidth = 1.3f, slashWidth = 0.055f,
                ringCount = 0, emberCount = 4, debrisCount = 0, smokeCount = 0,
                lightLumen = 6e5f, lightDuration = 0.08f, lightColor = new Color(1f, 0.8f, 0.55f),
            };
        }

        static SparkTierProfile CreateHeavy()
        {
            return new SparkTierProfile
            {
                label = "強",
                sparkCount = 42, sparkSpeed = new Vector2(6f, 16f), sparkLifetime = new Vector2(0.15f, 0.5f),
                sparkSize = new Vector2(0.022f, 0.042f), sparkSpread = 50f,
                flashSize = 1f, flareWidth = 2.4f, slashWidth = 0.085f,
                ringCount = 1, ringSize = 2.2f, emberCount = 14, debrisCount = 5, smokeCount = 2,
                lightLumen = 1.6e6f, lightDuration = 0.14f, lightColor = new Color(1f, 0.72f, 0.42f),
            };
        }

        static SparkTierProfile CreateFinisher()
        {
            return new SparkTierProfile
            {
                label = "最強",
                sparkCount = 85, sparkSpeed = new Vector2(8f, 22f), sparkLifetime = new Vector2(0.2f, 0.7f),
                sparkSize = new Vector2(0.025f, 0.05f), sparkSpread = 65f,
                secondaryBurstCount = 45, secondaryBurstDelay = 0.05f,
                flashSize = 1.6f, flareWidth = 4.2f, slashWidth = 0.12f,
                ringCount = 2, ringSize = 3.4f, emberCount = 32, debrisCount = 12, smokeCount = 5,
                lightLumen = 4e6f, lightDuration = 0.22f, lightColor = new Color(0.85f, 0.85f, 1f),
            };
        }

        static SparkTierProfile CreateUltra()
        {
            return new SparkTierProfile
            {
                label = "超強",
                sparkCount = 110, sparkSpeed = new Vector2(9f, 26f), sparkLifetime = new Vector2(0.25f, 0.9f),
                sparkSize = new Vector2(0.028f, 0.058f), sparkSpread = 40f,
                secondaryBurstCount = 60, secondaryBurstDelay = 0.07f,
                flashSize = 2.2f, flareWidth = 6f, slashWidth = 0.15f,
                ringCount = 2, ringSize = 4f, emberCount = 45, debrisCount = 18, smokeCount = 8,
                lightLumen = 8e6f, lightDuration = 0.4f, lightColor = new Color(0.8f, 0.88f, 1f),
            };
        }
    }
}
