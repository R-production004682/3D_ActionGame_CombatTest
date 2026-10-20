using System;
using UnityEngine;

namespace ActionStudy
{
    /// <summary>攻撃の格。ヒット演出の出し分けに使う。</summary>
    public enum AttackTier
    {
        Light,    // 弱
        Heavy,    // 強
        Finisher, // 最強
        Ultra,    // 超強
    }

    /// <summary>
    /// 1 段分の攻撃データ。モーション(剣の軌道)と、その攻撃が当たった時のヒット演出の強さをまとめて持つ。
    /// 「演出の強さを攻撃ごとに段階的に上げていく」ことがコンボの気持ちよさの核になるので、
    /// 値を見比べながら調整できるように 1 か所に集めている。
    /// </summary>
    [Serializable]
    public class AttackData
    {
        public string name = "弱1";
        public AttackTier tier = AttackTier.Light;
        public float damage = 10f;

        [Header("タイミング (秒)")]
        [Tooltip("予備動作(振りかぶり)の長さ")]
        public float windup = 0.1f;
        [Tooltip("攻撃判定が出ている長さ")]
        public float active = 0.1f;
        [Tooltip("後隙の長さ")]
        public float recovery = 0.3f;
        [Tooltip("攻撃開始からこの時間が経てば次の段へ繋げられる(空振り時)。当たった時は攻撃判定の終了時点から繋げられる")]
        public float comboWindowStart = 0.18f;

        [Header("剣の軌道")]
        [Tooltip("剣を振る面の傾き。0 = 水平、90 = 縦振り")]
        public float swingRoll;
        [Tooltip("振り始めの角度 (+ が右側)")]
        public float startAngle = 110f;
        [Tooltip("振り終わりの角度")]
        public float endAngle = -80f;
        [Tooltip("体ごと回転する角度 (回転斬り用)")]
        public float bodySpin;
        [Tooltip("攻撃中に剣を握る手の位置 (Player ローカル)")]
        public Vector3 handPosition = new Vector3(0.3f, 1.2f, 0.3f);

        [Header("踏み込み")]
        public float lungeDistance = 0.6f;

        [Header("ヒット演出の強さ")]
        [Tooltip("ヒットストップの長さ(秒)。60fps 換算で 0.0167 秒 = 1 フレーム")]
        public float hitStopDuration = 0.06f;
        [Tooltip("敵を押し出す距離 (m)")]
        public float knockbackDistance = 0.4f;
        [Tooltip("のけぞりで敵が動けない時間(秒)")]
        public float staggerTime = 0.3f;
        [Tooltip("のけぞり角度")]
        public float staggerAngle = 12f;
        [Tooltip("カメラシェイクの強さ (0-1)")]
        public float cameraShake = 0.25f;
        [Tooltip("FOV パンチ量(度)。0 で無し")]
        public float fovPunch;
        [Tooltip("色収差・レンズ歪み・ラジアルブラーの強さ (0-1)。0 で無し")]
        public float screenFxStrength;
        [Tooltip("パッド振動: 低周波モーター / 高周波モーター / 時間")]
        public Vector3 rumble = new Vector3(0.15f, 0.35f, 0.08f);

        [Header("吹き飛び")]
        [Tooltip("吹き飛ばす時の上向きの初速 (m/s)。0 で吹き飛ばさない")]
        public float launchUpSpeed;
        [Tooltip("吹き飛ばす時の水平方向の初速 (m/s)")]
        public float launchSpeed;
        [Tooltip("true: 倒しきれなくても吹き飛ばす / false: 倒した時だけ吹き飛ばす")]
        public bool launchAlive;

        [Header("溜め・体の動き")]
        [Tooltip("予備動作中に溜めの演出(光の収束・暗転・カメラの寄り)を出す")]
        public bool chargeEffect;
        [Tooltip("予備動作で体を後ろに反らす角度")]
        public float windupLeanBack;
        [Tooltip("振り抜きで体を前に倒す角度")]
        public float strikeLeanForward;

        [Header("地面への叩きつけ")]
        [Tooltip("攻撃判定の終わりに地面を叩き、この半径の敵をまとめて巻き込む。0 で無し")]
        public float groundShockwaveRadius;

        [Header("命中後のスロー")]
        [Tooltip("命中時のスローモーションの速さ。0 で無し (最強は撃破時のみ別途スロー)")]
        public float slowMotionScale;
        public float slowMotionDuration;

        public float TotalDuration => windup + active + recovery;

        /// <summary>表示用の短い名前 ("弱1 (右→左 薙ぎ)" → "弱1")。</summary>
        public string ShortName
        {
            get
            {
                int space = name.IndexOf(' ');
                return space > 0 ? name.Substring(0, space) : name;
            }
        }

        public static AttackData[] CreateDefaultCombo()
        {
            return new[]
            {
                new AttackData
                {
                    name = "弱1 (右→左 薙ぎ)", tier = AttackTier.Light, damage = 10f,
                    windup = 0.09f, active = 0.09f, recovery = 0.32f, comboWindowStart = 0.26f,
                    swingRoll = 18f, startAngle = 115f, endAngle = -85f, handPosition = new Vector3(0.25f, 1.2f, 0.3f),
                    lungeDistance = 0.7f,
                    hitStopDuration = 0.06f, knockbackDistance = 0.35f, staggerTime = 0.28f, staggerAngle = 10f,
                    cameraShake = 0.3f, fovPunch = 0f, screenFxStrength = 0f, rumble = new Vector3(0.1f, 0.3f, 0.07f),
                },
                new AttackData
                {
                    name = "弱2 (左→右 切り返し)", tier = AttackTier.Light, damage = 12f,
                    windup = 0.08f, active = 0.09f, recovery = 0.32f, comboWindowStart = 0.25f,
                    swingRoll = -22f, startAngle = -105f, endAngle = 95f, handPosition = new Vector3(0.15f, 1.15f, 0.35f),
                    lungeDistance = 0.7f,
                    hitStopDuration = 0.07f, knockbackDistance = 0.4f, staggerTime = 0.3f, staggerAngle = 12f,
                    cameraShake = 0.34f, fovPunch = 0f, screenFxStrength = 0f, rumble = new Vector3(0.12f, 0.35f, 0.08f),
                },
                new AttackData
                {
                    name = "強 (振り下ろし)", tier = AttackTier.Heavy, damage = 20f,
                    windup = 0.17f, active = 0.1f, recovery = 0.4f, comboWindowStart = 0.4f,
                    swingRoll = 80f, startAngle = 135f, endAngle = -55f, handPosition = new Vector3(0.1f, 1.35f, 0.35f),
                    lungeDistance = 1.1f,
                    hitStopDuration = 0.11f, knockbackDistance = 0.9f, staggerTime = 0.5f, staggerAngle = 22f,
                    cameraShake = 0.45f, fovPunch = 3f, screenFxStrength = 0.45f, rumble = new Vector3(0.45f, 0.6f, 0.14f),
                    windupLeanBack = 7f, strikeLeanForward = 12f,
                },
                new AttackData
                {
                    name = "最強 (回転斬り)", tier = AttackTier.Finisher, damage = 35f,
                    windup = 0.22f, active = 0.3f, recovery = 0.55f, comboWindowStart = 0.62f,
                    swingRoll = 8f, startAngle = 100f, endAngle = -100f, bodySpin = -360f, handPosition = new Vector3(0.35f, 1.15f, 0.25f),
                    lungeDistance = 1.5f,
                    hitStopDuration = 0.16f, knockbackDistance = 1.8f, staggerTime = 0.8f, staggerAngle = 30f,
                    cameraShake = 0.75f, fovPunch = 6f, screenFxStrength = 1f, rumble = new Vector3(0.9f, 1f, 0.25f),
                    launchUpSpeed = 6.5f, launchSpeed = 9f, launchAlive = false,
                },
                new AttackData
                {
                    name = "超強 (溜め振り下ろし・地割れ)", tier = AttackTier.Ultra, damage = 60f,
                    windup = 0.55f, active = 0.08f, recovery = 0.75f, comboWindowStart = 99f,
                    swingRoll = 88f, startAngle = 165f, endAngle = -72f, handPosition = new Vector3(0.05f, 1.5f, 0.35f),
                    lungeDistance = 1.8f,
                    hitStopDuration = 0.26f, knockbackDistance = 2.5f, staggerTime = 1.2f, staggerAngle = 45f,
                    cameraShake = 1f, fovPunch = -10f, screenFxStrength = 1.3f, rumble = new Vector3(1f, 1f, 0.45f),
                    launchUpSpeed = 9.5f, launchSpeed = 11f, launchAlive = true,
                    chargeEffect = true, windupLeanBack = 16f, strikeLeanForward = 26f,
                    groundShockwaveRadius = 4.5f,
                    slowMotionScale = 0.25f, slowMotionDuration = 0.9f,
                },
            };
        }
    }

    /// <summary>1 回のヒットの情報。演出側はこれを見て出し分ける。</summary>
    public struct HitInfo
    {
        public AttackData attack;
        public Enemy target;
        public Vector3 point;
        public Vector3 normal;
        public Vector3 slashDirection;
        public Vector3 attackerPosition;
        public bool killed;

        /// <summary>水平面上の「攻撃者から見て押し出す」方向。</summary>
        public Vector3 PushDirection
        {
            get
            {
                Vector3 away = point - attackerPosition;
                away.y = 0f;
                Vector3 slash = slashDirection;
                slash.y = 0f;
                Vector3 dir = away.normalized + slash.normalized * 0.4f;
                return dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            }
        }
    }
}
