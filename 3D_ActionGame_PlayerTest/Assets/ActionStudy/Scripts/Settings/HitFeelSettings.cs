using System;
using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// 「すべて ON」ボタンの対象から外すトグルに付ける(検証用の可視化や、わざと手触りを悪くする「悪い例」)。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ExcludeFromAllAttribute : Attribute
    {
    }

    /// <summary>
    /// 手触りに関わる演出の ON/OFF。
    ///  ・参考資料(トイロジック プランナーブログ「ヒット演出」)の 11 項目
    ///  ・それ以外の「操作の手触り」(入力・キャンセル / 体の動き / カメラ) … 以前は常時 ON だったものを切り替えられるようにした
    ///  ・追加演出 / UI フィードバック … 各社の技術ブログで挙げられている手法を足したもの
    /// 「すべて ON」の状態が完成形。1 つずつ OFF にして、何が失われるかを体感するための学習用スイッチ。
    /// Tooltip は「表示名：説明」の形式で書いてあり、ゲーム中のデバッグパネルにもそのまま表示される。
    /// </summary>
    [Serializable]
    public class HitFeelToggles
    {
        [Header("■ 参考資料のヒット演出")]
        [Tooltip("【1】ヒットエフェクト：金属を斬ったような火花・閃光・斬撃線など。攻撃の格(弱/強/最強)で量と種類が変わり、火花は斬撃方向に飛ぶ")]
        public bool hitEffect = true;
        [Tooltip("【2】ヒットSE：金属音。攻撃の格で音色を変え、毎回ピッチを少しずらして単調さを消す")]
        public bool hitSE = true;
        [Tooltip("【3】ヒットストップ(スローモー)：命中の瞬間に時間を止める(重い攻撃ほど長い)。止まっている間もエフェクトだけは等倍で再生。最強攻撃での撃破時はスローモーション")]
        public bool hitStop = true;
        [Tooltip("【4】ダメージモーション：被弾で仰け反る。かかしは吹き飛ばず、のけぞり量に比例して起き上がりこぼしのように傾いて揺れ戻る(寄ってくる敵では移動を止め、最強の撃破で吹き飛ぶ)")]
        public bool damageMotion = true;
        [Tooltip("【5】加算モーション：本来の動きを続けたまま、バネで体が揺れる")]
        public bool additiveMotion = true;
        [Tooltip("【6】ノックバック：攻撃方向へ押し出す。かかしは位置を動かさず、その分だけ大きく傾く。強攻撃以上では自分も少し押し戻され、斬った物の重さが出る")]
        public bool knockback = true;
        [Tooltip("【7】モデルシェイク：ヒットストップ中に敵の見た目を画面と平行に小刻みに揺らす。止まっていても『効いている』ことが伝わる")]
        public bool modelShake = true;
        [Tooltip("【8】カメラシェイク：命中時に衝撃の方向へカメラを揺らす。攻撃が重いほど強い")]
        public bool cameraShake = true;
        [Tooltip("【9】ダメージフラッシュ：被弾した敵を白く発光させる。乱戦でも『どれに当たったか』が一目で分かる")]
        public bool damageFlash = true;
        [Tooltip("【10】フォースフィードバック：パッド振動(ゲームパッド接続時のみ)。攻撃が重いほど強く長い")]
        public bool forceFeedback = true;
        [Tooltip("【11】その他画面効果：ラジアルブラー・色収差・レンズ歪み・命中レティクル。強攻撃以上で一瞬だけ出す")]
        public bool screenEffects = true;

        [Header("■ 操作の手触り：入力とキャンセル")]
        [Tooltip("先行入力：攻撃中に押したボタンを約 0.35 秒覚えておき、繋げられる瞬間に次段を出す。OFF だと受付が始まった後に押さないと繋がらず、連打がすっぽ抜ける")]
        public bool inputBuffer = true;
        [Tooltip("ヒット確定キャンセル：当たった時だけ攻撃判定の終わりから次段を受け付ける。当て続けるとコンボのリズムが速くなり、空振りは重くなる")]
        public bool hitConfirmCancel = true;
        [Tooltip("移動キャンセル：後隙の後半はスティック入力ですぐ走り出せる。OFF だと攻撃を最後まで見届けるまで動けない")]
        public bool moveCancel = true;
        [Tooltip("吸い付き・踏み込み：近くの敵へ向き直り、ちょうど届く距離まで踏み込む。OFF だと向いている方向へ一定距離だけ踏み込む(空振りしやすい)")]
        public bool attackAssist = true;

        [Header("■ 操作の手触り：体の動き")]
        [Tooltip("初速ブースト：止まった状態からの加速だけ強くして、走り出しのもたつきを消す(『走り出しはグッと』)")]
        public bool startBoost = true;
        [Tooltip("クイックターン：逆方向へ入れた時だけ素早く振り向き、慣性で滑りながら切り返す")]
        public bool quickTurn = true;
        [Tooltip("体の傾き：旋回で内側へ(バンク)、加速で前へ、急停止・切り返しで後ろへ傾く")]
        public bool bodyLean = true;
        [Tooltip("ランボブ：歩幅に合わせて体が上下・左右に揺れる")]
        public bool runBob = true;
        [Tooltip("予備動作と緩急：振りかぶって一瞬溜め、一気に振り抜く(イージング + 振りかぶりの上乗せ)。OFF だと剣が等速で動き、軽く・遅く見える")]
        public bool anticipation = true;
        [Tooltip("スクワッシュ & ストレッチ：振りかぶりで体が縮み、振り抜きで伸びる")]
        public bool squashStretch = true;
        [Tooltip("剣の揺れ追従(二次モーション)：待機・移動中の剣が、体の旋回・加速・足取りに遅れてついてくる")]
        public bool swordSecondary = true;
        [Tooltip("剣の軌跡：振った軌道に光の帯を残す。速い振りでも剣筋が読める")]
        public bool swordTrail = true;

        [Header("■ 操作の手触り：カメラ")]
        [Tooltip("追従の遅れ・先読み：わずかに遅れてついてきて、進行方向の少し先を映す。OFF だとプレイヤーに張り付き、速さが伝わりにくい")]
        public bool cameraLag = true;
        [Tooltip("速度ズーム：速く走るほど少し引く。スピード感と視界を両立する")]
        public bool speedZoom = true;
        [Tooltip("自動回り込み：視点操作をしばらくしていない時、横移動に合わせてゆっくり背後へ回る")]
        public bool autoRecenter = true;
        [Tooltip("攻撃フレーミング：攻撃中、狙った敵が画面から外れないよう自動で向きを寄せる")]
        public bool attackFraming = true;
        [Tooltip("FOV パンチ：強攻撃以上で一瞬ズームイン(超強はズームアウト)して戻る")]
        public bool fovPunch = true;
        [Tooltip("ロックオン：Q / マウスホイールクリック / 右スティック押し込みで近くの敵に狙いを固定する(もう一度押すと解除)。カメラが敵を向き続け、攻撃もその敵へ自動で向かう。ロック中はホイール / 右スティックの左右で対象を切り替え")]
        public bool lockOn = true;

        [Header("■ 追加演出")]
        [Tooltip("足元の砂煙：走り出し・切り返し・急停止・全力疾走の足取りで土煙を上げる。地面を蹴った力が見える")]
        public bool moveDust = true;
        [Tooltip("スピードライン：全力疾走や踏み込みで、画面の外周に流線を出す。相対的な速さを伝える(トイロジック『移動アクション TIPS』)")]
        public bool speedLines = true;
        [Tooltip("攻撃側の震え：ヒットストップ中、敵だけでなく自分の剣も小刻みに震える。刃が食い込んだ抵抗感が出る")]
        public bool attackerShake = true;
        [Tooltip("撃破の予兆：倒した瞬間に目の光を消し、一瞬硬直して明滅してから倒れる。『もう倒した』が即座に分かり、死体への無駄打ちが減る(ASTRAL CHAIN 開発ブログ)")]
        public bool deathTell = true;

        [Header("■ UI フィードバック")]
        [Tooltip("HP ゲージ(遅延ゲージ)：減った分を白いゲージで少し残してから減らす。一撃で削った量が見える")]
        public bool hpGauge = true;
        [Tooltip("ダメージ数値：着弾点から数値が弾けて浮かぶ。攻撃の格で大きさと色が変わる")]
        public bool damageNumbers = true;
        [Tooltip("コンボカウンター：連続ヒット数を表示し、増えるたびに弾ませる。途切れるまでの残り時間をバーで見せる")]
        public bool comboCounter = true;

        [Header("■ 検証用 (「すべて ON」の対象外)")]
        [ExcludeFromAll]
        [Tooltip("攻撃フレームの可視化：攻撃の予備動作・判定・後隙・次段の受付・移動キャンセル・押した瞬間を帯で表示する。キャンセルと先行入力の調整用")]
        public bool frameTimeline;
        [ExcludeFromAll]
        [Tooltip("【悪い例】入力遅延 +100ms：移動と攻撃の入力を 6 フレーム遅らせる。演出が同じでも、反応が遅いだけで手触りが大きく落ちることを体感する")]
        public bool inputDelay;
        [ExcludeFromAll]
        [Tooltip("【悪い例】先行入力が長すぎる (1 秒)：だいぶ前に押したボタンまで実行されてしまい、止めたい時に止まらない")]
        public bool longInputBuffer;
    }

    /// <summary>
    /// シーンに 1 つ置く設定オブジェクト。各演出コンポーネントは HitFeelSettings.T を参照して ON/OFF を判断する。
    /// プレイ中にインスペクターで切り替えても即座に反映される。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class HitFeelSettings : MonoBehaviour
    {
        public static HitFeelSettings Instance { get; private set; }

        static readonly HitFeelToggles s_Defaults = new HitFeelToggles();

        /// <summary>現在のトグル。シーンに設定が無い場合は既定値を返す。</summary>
        public static HitFeelToggles T => Instance != null ? Instance.toggles : s_Defaults;

        public HitFeelToggles toggles = new HitFeelToggles();

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
