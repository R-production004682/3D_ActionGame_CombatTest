using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// ヒット SE と風切り音。音素材を用意しなくて済むよう、起動時に波形を合成して AudioClip を作る。
    ///
    /// 金属音は「整数倍でない倍音(非調和倍音)」が特徴なので、金属棒の固有振動の比率で正弦波を重ね、
    /// 高い倍音ほど早く減衰させる。そこにアタックのクリックノイズと、重い攻撃では低音の「ドン」を足す。
    /// AAA の工夫として、同じ格でも複数のバリエーションを作り、再生ごとにピッチを少しずらして単調さを消している。
    /// </summary>
    public class HitAudio : MonoBehaviour
    {
        const int k_SampleRate = 48000;
        const int k_VoiceCount = 8;

        [Range(0f, 1f)] public float hitVolume = 0.8f;
        [Range(0f, 1f)] public float swingVolume = 0.45f;
        [Tooltip("再生ごとにピッチをランダムにずらす幅")]
        public float pitchVariation = 0.06f;

        AudioClip[] m_LightHits;
        AudioClip[] m_HeavyHits;
        AudioClip[] m_FinisherHits;
        AudioClip[] m_Swings;
        AudioClip m_UltraHit;
        AudioClip m_SlamBoom;
        AudioClip m_Charge;
        AudioClip m_Glint;
        AudioSource[] m_Voices;

        const float k_ChargeClipLength = 0.6f;
        int m_NextVoice;
        System.Random m_Random;

        // 金属棒の固有振動の比率 (自由端の梁)
        static readonly float[] s_MetalRatios = { 1f, 2.756f, 5.404f, 8.933f, 13.344f };
        static readonly float[] s_MetalAmps = { 1f, 0.65f, 0.42f, 0.25f, 0.14f };

        void Awake()
        {
            m_Random = new System.Random(1234);
            m_Voices = new AudioSource[k_VoiceCount];
            for (int i = 0; i < k_VoiceCount; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                m_Voices[i] = source;
            }

            m_LightHits = new[]
            {
                CreateMetalHit("HitLight_A", 2400f, 0.10f, 0.9f, 0.15f, 0.45f),
                CreateMetalHit("HitLight_B", 2750f, 0.09f, 0.9f, 0.15f, 0.45f),
                CreateMetalHit("HitLight_C", 2150f, 0.11f, 0.9f, 0.18f, 0.45f),
            };
            m_HeavyHits = new[]
            {
                CreateMetalHit("HitHeavy_A", 1500f, 0.22f, 1f, 0.6f, 0.8f),
                CreateMetalHit("HitHeavy_B", 1700f, 0.2f, 1f, 0.6f, 0.8f),
            };
            m_FinisherHits = new[]
            {
                CreateMetalHit("HitFinisher_A", 950f, 0.55f, 1f, 1f, 1.6f),
                CreateMetalHit("HitFinisher_B", 1100f, 0.5f, 1f, 1f, 1.6f),
            };
            m_Swings = new[]
            {
                CreateSwing("Swing_Short", 0.16f, 900f, 2600f),
                CreateSwing("Swing_Mid", 0.22f, 700f, 2200f),
                CreateSwing("Swing_Long", 0.34f, 500f, 1800f),
            };
            m_UltraHit = CreateMetalHit("HitUltra", 700f, 0.9f, 1f, 1.6f, 2.2f);
            m_SlamBoom = CreateBoom("SlamBoom", 1.8f);
            m_Charge = CreateCharge("Charge", k_ChargeClipLength);
            m_Glint = CreateGlint("Glint", 0.6f);
        }

        /// <summary>溜めの唸り。溜め時間にぴったり収まるようピッチで長さを合わせる。</summary>
        public void PlayCharge(float duration)
        {
            Play(m_Charge, swingVolume * 1.3f, k_ChargeClipLength / Mathf.Max(duration, 0.1f));
        }

        /// <summary>振り下ろす直前の「キラーン」。</summary>
        public void PlayGlint()
        {
            Play(m_Glint, hitVolume * 0.6f);
        }

        /// <summary>地面への叩きつけ。当たった時は金属音を重ねる。</summary>
        public void PlaySlam(bool hit)
        {
            Play(m_SlamBoom, hitVolume);
            if (hit)
                PlayHit(AttackTier.Ultra);
        }

        public void PlayHit(AttackTier tier)
        {
            switch (tier)
            {
                case AttackTier.Ultra:
                    // 超強は低い金属音 + 最強の金属音を重ねて、さらに厚く長く
                    Play(m_UltraHit, hitVolume);
                    Play(m_FinisherHits[0], hitVolume * 0.6f, 0.8f);
                    break;
                case AttackTier.Heavy:
                    Play(Pick(m_HeavyHits), hitVolume);
                    break;
                case AttackTier.Finisher:
                    // 最強は金属音を 2 層重ねて厚みを出す
                    Play(m_FinisherHits[0], hitVolume);
                    Play(m_FinisherHits[1], hitVolume * 0.7f, 1.35f);
                    break;
                default:
                    Play(Pick(m_LightHits), hitVolume * 0.85f);
                    break;
            }
        }

        public void PlaySwing(AttackTier tier)
        {
            if (tier == AttackTier.Ultra)
            {
                // 超強は長い風切りを低く鳴らして重さを出す
                Play(m_Swings[2], swingVolume * 1.2f, 0.75f);
                return;
            }
            AudioClip clip = tier == AttackTier.Finisher ? m_Swings[2] : tier == AttackTier.Heavy ? m_Swings[1] : m_Swings[0];
            Play(clip, swingVolume);
        }

        AudioClip Pick(AudioClip[] clips)
        {
            return clips[m_Random.Next(clips.Length)];
        }

        void Play(AudioClip clip, float volume, float basePitch = 1f)
        {
            AudioSource source = m_Voices[m_NextVoice];
            m_NextVoice = (m_NextVoice + 1) % m_Voices.Length;
            float variation = ((float)m_Random.NextDouble() * 2f - 1f) * pitchVariation;
            source.pitch = basePitch * (1f + variation);
            source.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------------
        // 合成
        // ------------------------------------------------------------------

        /// <param name="baseFreq">基音の周波数</param>
        /// <param name="decay">基音の減衰時間(秒)</param>
        /// <param name="click">アタックのクリック量</param>
        /// <param name="thump">低音の「ドン」の量</param>
        /// <param name="length">長さ(秒)</param>
        AudioClip CreateMetalHit(string clipName, float baseFreq, float decay, float click, float thump, float length)
        {
            int count = Mathf.CeilToInt(length * k_SampleRate);
            var data = new float[count];
            var rng = new System.Random(clipName.GetHashCode());
            int partials = s_MetalRatios.Length;
            var freqs = new float[partials * 2];
            var phases = new float[partials * 2];
            for (int i = 0; i < partials; i++)
            {
                // 少しだけずらした 2 本を重ねてうなり(揺らぎ)を作る
                float detune = 1f + ((float)rng.NextDouble() - 0.5f) * 0.012f;
                freqs[i * 2] = baseFreq * s_MetalRatios[i] * detune;
                freqs[i * 2 + 1] = baseFreq * s_MetalRatios[i] * detune * 1.004f;
                phases[i * 2] = (float)rng.NextDouble() * Mathf.PI * 2f;
                phases[i * 2 + 1] = (float)rng.NextDouble() * Mathf.PI * 2f;
            }

            float noiseLp = 0f;
            float peak = 0f;
            for (int n = 0; n < count; n++)
            {
                float t = (float)n / k_SampleRate;
                float s = 0f;
                for (int i = 0; i < partials; i++)
                {
                    float tau = decay / (1f + i * 0.9f);
                    float env = Mathf.Exp(-t / tau);
                    float a = s_MetalAmps[i] * env * 0.5f;
                    s += a * Mathf.Sin(2f * Mathf.PI * freqs[i * 2] * t + phases[i * 2]);
                    s += a * Mathf.Sin(2f * Mathf.PI * freqs[i * 2 + 1] * t + phases[i * 2 + 1]);
                }

                // アタック: ごく短い白色ノイズ + 少し長めのこもったノイズ(擦れ)
                float white = (float)rng.NextDouble() * 2f - 1f;
                noiseLp += (white - noiseLp) * 0.25f;
                s += white * click * Mathf.Exp(-t / 0.003f);
                s += noiseLp * click * 0.6f * Mathf.Exp(-t / 0.03f);

                // 低音の衝撃(ピッチが下がりながら減衰する正弦波)
                if (thump > 0f)
                {
                    float f = 55f + 90f * Mathf.Exp(-t / 0.025f);
                    s += thump * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t / 0.12f) * 1.2f;
                }

                // 簡易ソフトクリップで音圧を出す
                s = (float)System.Math.Tanh(s * 1.4f);
                data[n] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }

            Normalize(data, peak);
            FadeOutTail(data, 0.02f);
            AudioClip clip = AudioClip.Create(clipName, count, 1, k_SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip CreateSwing(string clipName, float length, float lowFreq, float highFreq)
        {
            int count = Mathf.CeilToInt(length * k_SampleRate);
            var data = new float[count];
            var rng = new System.Random(clipName.GetHashCode());

            // ステート変数フィルタでノイズをバンドパスし、中心周波数を上げて下げる → 「ヒュッ」
            float low = 0f, band = 0f;
            float peak = 0f;
            for (int n = 0; n < count; n++)
            {
                float u = (float)n / count;
                float sweep = Mathf.Sin(u * Mathf.PI);
                float freq = Mathf.Lerp(lowFreq, highFreq, sweep);
                float f = 2f * Mathf.Sin(Mathf.PI * freq / k_SampleRate);
                const float q = 0.35f;
                float input = (float)rng.NextDouble() * 2f - 1f;
                low += f * band;
                float high = input - low - q * band;
                band += f * high;

                float env = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 2f) * Mathf.Lerp(1f, 0.4f, u);
                float s = band * env;
                data[n] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }

            Normalize(data, peak);
            AudioClip clip = AudioClip.Create(clipName, count, 1, k_SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>地面を叩いた轟音: ピッチが落ちていく低音 + 地鳴りのノイズ + 割れる音。</summary>
        AudioClip CreateBoom(string clipName, float length)
        {
            int count = Mathf.CeilToInt(length * k_SampleRate);
            var data = new float[count];
            var rng = new System.Random(clipName.GetHashCode());
            float rumbleLp = 0f;
            float crackLp = 0f;
            float phase = 0f;
            float peak = 0f;
            for (int n = 0; n < count; n++)
            {
                float t = (float)n / k_SampleRate;
                float white = (float)rng.NextDouble() * 2f - 1f;

                float freq = 38f + 110f * Mathf.Exp(-t / 0.06f);
                phase += 2f * Mathf.PI * freq / k_SampleRate;
                float s = Mathf.Sin(phase) * Mathf.Exp(-t / 0.45f) * 1.4f;

                rumbleLp += (white - rumbleLp) * 0.02f;
                s += rumbleLp * 6f * Mathf.Exp(-t / 0.6f);

                crackLp += (white - crackLp) * 0.5f;
                s += crackLp * Mathf.Exp(-t / 0.05f) * 0.9f;

                s = (float)System.Math.Tanh(s * 1.6f);
                data[n] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            Normalize(data, peak);
            FadeOutTail(data, 0.1f);
            AudioClip clip = AudioClip.Create(clipName, count, 1, k_SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>溜めの唸り: 低い音から高い音へ上がっていき、最後に鋭く途切れる。</summary>
        AudioClip CreateCharge(string clipName, float length)
        {
            int count = Mathf.CeilToInt(length * k_SampleRate);
            var data = new float[count];
            var rng = new System.Random(clipName.GetHashCode());
            float phase1 = 0f, phase2 = 0f;
            float low = 0f, band = 0f;
            float peak = 0f;
            for (int n = 0; n < count; n++)
            {
                float u = (float)n / count;
                float freq = 160f * Mathf.Pow(6f, u);
                phase1 += 2f * Mathf.PI * freq / k_SampleRate;
                phase2 += 2f * Mathf.PI * freq * 1.5f / k_SampleRate;
                float tremolo = 0.75f + 0.25f * Mathf.Sin(u * length * Mathf.PI * 2f * Mathf.Lerp(8f, 30f, u));
                float s = (Mathf.Sin(phase1) + 0.4f * Mathf.Sin(phase2)) * tremolo;

                // 上がっていく風のようなノイズ
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Lerp(400f, 3000f, u) / k_SampleRate);
                float input = (float)rng.NextDouble() * 2f - 1f;
                low += f * band;
                band += f * (input - low - 0.4f * band);
                s += band * 0.8f;

                float env = Mathf.SmoothStep(0f, 1f, u) * (u > 0.97f ? (1f - u) / 0.03f : 1f);
                s *= env;
                data[n] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            Normalize(data, peak);
            AudioClip clip = AudioClip.Create(clipName, count, 1, k_SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>「キラーン」: 高い倍音を柔らかい立ち上がりで鳴らし、長めに残す。</summary>
        AudioClip CreateGlint(string clipName, float length)
        {
            int count = Mathf.CeilToInt(length * k_SampleRate);
            var data = new float[count];
            float[] freqs = { 3700f, 5250f, 7400f, 9900f };
            float[] amps = { 1f, 0.6f, 0.35f, 0.2f };
            float peak = 0f;
            for (int n = 0; n < count; n++)
            {
                float t = (float)n / k_SampleRate;
                float attack = Mathf.Clamp01(t / 0.004f);
                float s = 0f;
                for (int i = 0; i < freqs.Length; i++)
                    s += amps[i] * Mathf.Sin(2f * Mathf.PI * freqs[i] * t) * Mathf.Exp(-t / (0.22f / (1f + i * 0.5f)));
                // ゆっくりしたうなりで「きらめき」感を出す
                s *= attack * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 11f * t));
                data[n] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            Normalize(data, peak);
            FadeOutTail(data, 0.05f);
            AudioClip clip = AudioClip.Create(clipName, count, 1, k_SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Normalize(float[] data, float peak)
        {
            if (peak <= 1e-5f)
                return;
            float gain = 0.9f / peak;
            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;
        }

        static void FadeOutTail(float[] data, float seconds)
        {
            int fade = Mathf.Min(data.Length, Mathf.CeilToInt(seconds * k_SampleRate));
            for (int i = 0; i < fade; i++)
                data[data.Length - 1 - i] *= (float)i / fade;
        }
    }
}
