using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ActionStudy
{
    /// <summary>
    /// 【11】その他画面効果。HDRP の Volume (ポストプロセス) と Custom Pass、IMGUI を使う。
    ///  ・ラジアルブラー(Custom Pass) / 色収差 / レンズ歪み / インパクト色調(最強のみ) … 強攻撃以上で一瞬だけ
    ///  ・命中レティクル … すべての命中で着弾点にマークを出す
    ///  ・溜めの暗転 / インパクトフレーム (超強) … 溜めで周囲を暗く沈め、叩きつけの瞬間に白黒の閃光を 1 枚挟む
    /// パルスはすべて実時間で減衰させる。「一瞬だけ強く、すぐ戻す」のがコツで、出しっぱなしは画面が見づらくなるだけ。
    /// ブルームとビネットは画作りの土台として Volume Profile 側で常に ON にしている。
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class ScreenFxController : MonoBehaviour
    {
        public static ScreenFxController Instance { get; private set; }

        [Tooltip("Hidden/ActionStudy/RadialBlur シェーダーのマテリアル")]
        public Material radialBlurMaterial;

        [Header("パルスの強さ (screenFxStrength = 1 の時)")]
        public float chromaticAberrationPeak = 0.9f;
        public float lensDistortionPeak = -0.28f;
        public float radialBlurPeak = 0.12f;
        public float impactExposurePeak = 1.3f;
        public float impactSaturationPeak = -60f;

        [Header("パルスの長さ(秒)")]
        public float chromaticAberrationDuration = 0.22f;
        public float lensDistortionDuration = 0.35f;
        public float radialBlurDuration = 0.2f;
        public float impactColorDuration = 0.4f;

        [Header("溜め・インパクトフレーム (超強)")]
        [Tooltip("溜め切った時に追加するビネット")]
        public float chargeVignette = 0.25f;
        public float chargeExposure = -0.6f;
        public float chargeSaturation = -35f;
        [Tooltip("インパクトフレーム(白黒の閃光)の長さ(秒, 実時間)")]
        public float impactFrameDuration = 0.07f;

        [Header("命中レティクル")]
        public float hitMarkerDuration = 0.2f;
        [Tooltip("マークの線の長さ (px, 1080p 基準)")]
        public float hitMarkerLength = 14f;
        public float hitMarkerThickness = 3f;

        Volume m_Volume;
        Vignette m_Vignette;
        float m_BaseVignette;
        float m_Charge;
        float m_ImpactFrameTime = 99f;
        ChromaticAberration m_ChromaticAberration;
        LensDistortion m_LensDistortion;
        ColorAdjustments m_ColorAdjustments;
        float m_BaseChromaticAberration;

        CustomPassVolume m_PassVolume;
        FullScreenCustomPass m_BlurPass;
        Material m_BlurMaterialInstance;

        float m_Strength;
        float m_CaTime = 99f;
        float m_LdTime = 99f;
        float m_BlurTime = 99f;
        float m_ColorTime = 99f;
        Vector2 m_BlurCenter = new Vector2(0.5f, 0.5f);

        float m_MarkerTime = 99f;
        Vector3 m_MarkerWorldPoint;
        AttackTier m_MarkerTier;

        static readonly int s_StrengthId = Shader.PropertyToID("_Strength");
        static readonly int s_CenterId = Shader.PropertyToID("_Center");

        void Awake()
        {
            Instance = this;
            m_Volume = GetComponent<Volume>();

            // .profile はランタイム用のコピーを返すので、元のアセットは書き換わらない
            VolumeProfile profile = m_Volume.profile;
            m_ChromaticAberration = GetOrAdd<ChromaticAberration>(profile);
            m_LensDistortion = GetOrAdd<LensDistortion>(profile);
            m_ColorAdjustments = GetOrAdd<ColorAdjustments>(profile);
            m_Vignette = GetOrAdd<Vignette>(profile);
            m_BaseVignette = m_Vignette.intensity.value;
            m_Vignette.intensity.overrideState = true;
            m_ColorAdjustments.contrast.overrideState = true;

            m_BaseChromaticAberration = m_ChromaticAberration.intensity.value;
            m_ChromaticAberration.intensity.overrideState = true;
            m_LensDistortion.intensity.overrideState = true;
            m_ColorAdjustments.postExposure.overrideState = true;
            m_ColorAdjustments.saturation.overrideState = true;

            if (radialBlurMaterial != null)
            {
                m_BlurMaterialInstance = new Material(radialBlurMaterial);
                var passObject = new GameObject("RadialBlur Custom Pass");
                passObject.transform.SetParent(transform, false);
                m_PassVolume = passObject.AddComponent<CustomPassVolume>();
                m_PassVolume.isGlobal = true;
                m_PassVolume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
                m_BlurPass = new FullScreenCustomPass
                {
                    name = "Radial Blur",
                    fullscreenPassMaterial = m_BlurMaterialInstance,
                    materialPassName = "Custom Pass 0",
                    fetchColorBuffer = true,
                    targetColorBuffer = CustomPass.TargetBuffer.Camera,
                    targetDepthBuffer = CustomPass.TargetBuffer.None,
                    clearFlags = ClearFlag.None,
                    enabled = false,
                };
                m_PassVolume.customPasses.Add(m_BlurPass);
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (m_BlurMaterialInstance != null)
                Destroy(m_BlurMaterialInstance);
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
                component = profile.Add<T>(true);
            component.active = true;
            return component;
        }

        /// <summary>ヒット時の画面効果。strength は AttackData.screenFxStrength。</summary>
        public void Pulse(float strength, Vector3 worldPoint, AttackTier tier)
        {
            if (!HitFeelSettings.T.screenEffects)
                return;

            m_MarkerTime = 0f;
            m_MarkerWorldPoint = worldPoint;
            m_MarkerTier = tier;

            if (strength <= 0f)
                return;
            m_Strength = strength;
            m_CaTime = 0f;
            m_LdTime = 0f;
            m_BlurTime = 0f;
            if (tier == AttackTier.Finisher || tier == AttackTier.Ultra)
                m_ColorTime = 0f;

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 viewport = cam.WorldToViewportPoint(worldPoint);
                m_BlurCenter = viewport.z > 0f ? new Vector2(viewport.x, viewport.y) : new Vector2(0.5f, 0.5f);
            }
        }

        /// <summary>溜めの進み具合 (0-1)。周囲を暗く沈めて、剣の光を際立たせる。</summary>
        public void SetCharge(float amount)
        {
            m_Charge = Mathf.Clamp01(amount);
        }

        /// <summary>叩きつけの瞬間に 1 枚だけ挟む、白黒の強烈な閃光(アニメでいう「インパクトフレーム」)。</summary>
        public void ImpactFrame()
        {
            if (HitFeelSettings.T.screenEffects)
                m_ImpactFrameTime = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool enabledFx = HitFeelSettings.T.screenEffects;
            m_CaTime += dt;
            m_LdTime += dt;
            m_BlurTime += dt;
            m_ColorTime += dt;
            m_MarkerTime += dt;
            m_ImpactFrameTime += dt;

            // 色収差: 立ち上がり一瞬 → 二次曲線で減衰
            float ca = enabledFx ? Decay(m_CaTime, chromaticAberrationDuration) * chromaticAberrationPeak * m_Strength : 0f;
            m_ChromaticAberration.intensity.value = Mathf.Clamp01(m_BaseChromaticAberration + ca);

            // レンズ歪み: 減衰しながら 1 回揺り戻す(画面がたわんで戻る)
            float ld = 0f;
            if (enabledFx && m_LdTime < lensDistortionDuration)
            {
                float u = m_LdTime / lensDistortionDuration;
                ld = lensDistortionPeak * m_Strength * (1f - u) * (1f - u) * Mathf.Cos(u * Mathf.PI * 2.2f);
            }
            m_LensDistortion.intensity.value = Mathf.Clamp(ld, -1f, 1f);

            // ラジアルブラー
            float blur = enabledFx ? Decay(m_BlurTime, radialBlurDuration) * radialBlurPeak * m_Strength : 0f;
            if (m_BlurPass != null)
            {
                m_BlurPass.enabled = blur > 0.001f;
                m_BlurMaterialInstance.SetFloat(s_StrengthId, blur);
                m_BlurMaterialInstance.SetVector(s_CenterId, m_BlurCenter);
            }

            // インパクト色調(最強のみ)
            float color = enabledFx ? Decay(m_ColorTime, impactColorDuration) : 0f;
            float charge = enabledFx ? m_Charge * m_Charge : 0f;
            bool impactFrame = enabledFx && m_ImpactFrameTime < impactFrameDuration;
            m_ColorAdjustments.postExposure.value = impactExposurePeak * color + chargeExposure * charge + (impactFrame ? 2.2f : 0f);
            m_ColorAdjustments.saturation.value = Mathf.Clamp(impactSaturationPeak * color + chargeSaturation * charge + (impactFrame ? -100f : 0f), -100f, 100f);
            m_ColorAdjustments.contrast.value = impactFrame ? 80f : 0f;
            m_Vignette.intensity.value = Mathf.Clamp01(m_BaseVignette + chargeVignette * charge);
        }

        void OnGUI()
        {
            // 命中レティクル: 着弾点の周りに 4 本の斜線を出し、広がりながら消す
            if (m_MarkerTime >= hitMarkerDuration || !HitFeelSettings.T.screenEffects)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Vector3 screen = cam.WorldToScreenPoint(m_MarkerWorldPoint);
            if (screen.z <= 0f)
                return;

            float u = m_MarkerTime / hitMarkerDuration;
            float scale = Screen.height / 1080f;
            float tierScale = m_MarkerTier == AttackTier.Light ? 1f : m_MarkerTier == AttackTier.Heavy ? 1.35f : m_MarkerTier == AttackTier.Finisher ? 1.8f : 2.4f;
            float length = hitMarkerLength * tierScale * scale;
            float gap = Mathf.Lerp(8f, 22f, Ease.OutCubic(u)) * tierScale * scale;
            float thickness = hitMarkerThickness * scale;
            Vector2 center = new Vector2(screen.x, Screen.height - screen.y);

            Color tint = m_MarkerTier >= AttackTier.Finisher ? new Color(1f, 0.75f, 0.35f) : Color.white;
            tint.a = 1f - u * u;
            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.color = tint;
            for (int i = 0; i < 4; i++)
            {
                GUI.matrix = prevMatrix;
                GUIUtility.RotateAroundPivot(45f + 90f * i, center);
                GUI.DrawTexture(new Rect(center.x + gap, center.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
            }
            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        static float Decay(float time, float duration)
        {
            if (time >= duration)
                return 0f;
            float u = 1f - time / duration;
            return u * u;
        }
    }
}
