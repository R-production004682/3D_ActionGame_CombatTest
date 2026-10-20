# 演出パラメータ一覧

このページでは、Inspector から調整できる戦闘・操作・見た目の主な数値を説明します。Inspector の項目名も載せているので、Unity 上で同じ名前を探せます。

値はスクリプトに書かれた初期値です。シーンや Prefab に別の値が保存されている場合は、そちらが使われます。位置や素材などの参照欄は、演出そのものの調整値ではないため、ここでは省いています。

## 用語

| 言葉 | 意味 |
|---|---|
| 秒・m・度 | 時間、Unity の距離、角度です。 |
| Hz | 1 秒間に繰り返す回数です。 |
| 減衰 | 揺れや速度がどれだけ早く収まるかです。値が大きいほど早く収まります。 |
| バネの硬さ | 揺れを元の姿勢へ戻す強さです。値が大きいほど戻りが速くなります。 |
| イージング | 動き始めや終わりの速さを変え、急な切り替わりをやわらげる方法です。 |
| 攻撃の格 | 弱・強・最強・超強の区分です。火花や音などの出し分けに使います。 |

## 攻撃ごとの設定

場所: プレイヤーの `PlayerCombat` コンポーネントにある `combo` 配列。各段の `AttackData` を開いて調整します。攻撃ごとの値が全体の演出強度を決めます。

| 項目 | 初期値の目安 | 内容 |
|---|---:|---|
| `name` / `tier` | 弱1 / Light | 表示名と攻撃の格です。格は火花・音・数値表示などの種類分けにも使います。 |
| `damage` | 10 | 敵の HP を減らす量です。 |
| `windup` | 0.1 秒 | 振りかぶりから攻撃判定までの時間です。長いほど予備動作が見えます。 |
| `active` | 0.1 秒 | 剣の攻撃判定が有効な時間です。 |
| `recovery` | 0.3 秒 | 攻撃判定の後に動けない時間です。 |
| `comboWindowStart` | 0.18 秒 | 空振り時に次の段を受け付け始める時刻です。命中時はヒット確定キャンセルの設定が優先されます。 |
| `swingRoll` | 0° | 剣を振る面の傾きです。0° は水平、90° は縦振りです。 |
| `startAngle` / `endAngle` | 110° / -80° | 剣を振り始める角度と振り終わる角度です。正の角度は右側から振り始めます。 |
| `bodySpin` | 0° | 回転斬りなどで、攻撃中に体を回す角度です。 |
| `handPosition` | (0.3, 1.2, 0.3) | 攻撃中の剣を握る手の位置です。プレイヤー基準の座標です。 |
| `lungeDistance` | 0.6 m | 攻撃中に前へ踏み込む距離です。 |
| `hitStopDuration` | 0.06 秒 | 命中時に時間を止める長さです。60 fps なら約 0.017 秒で 1 フレームです。 |
| `knockbackDistance` | 0.4 m | 敵を攻撃方向へ押し出す距離です。 |
| `staggerTime` | 0.3 秒 | 敵がのけぞって動けない時間です。 |
| `staggerAngle` | 12° | 敵の体がのけぞる角度です。 |
| `cameraShake` | 0.25 | 命中時のカメラ揺れの強さです。0〜1 が目安です。 |
| `fovPunch` | 0° | 命中時に変える FOV の角度です。0 なら変化しません。マイナス値は画面を引きます。 |
| `screenFxStrength` | 0 | 色ずれ・ゆがみ・放射状ぼかしの強さです。0 なら出ません。 |
| `rumble` | (0.15, 0.35, 0.08) | ゲームパッド振動の「低周波の強さ、高周波の強さ、秒数」です。 |
| `launchUpSpeed` | 0 m/s | 敵を打ち上げる上向きの初速です。0 なら打ち上げません。 |
| `launchSpeed` | 0 m/s | 敵を押し飛ばす水平の初速です。 |
| `launchAlive` | false | ON なら敵を倒していなくても吹き飛ばします。OFF なら撃破時だけです。 |
| `chargeEffect` | false | ON なら予備動作中に光の収束・画面暗転・カメラの寄りを出します。 |
| `windupLeanBack` | 0° | 予備動作中に体を後ろへ反らす角度です。 |
| `strikeLeanForward` | 0° | 振り抜く時に体を前へ倒す角度です。 |
| `groundShockwaveRadius` | 0 m | 叩きつけ時に周囲へ当たる範囲です。0 なら衝撃波はありません。 |
| `slowMotionScale` | 0 | 命中後の時間の速さです。1 は通常、0.25 は通常の 4 分の 1 の速さです。0 なら使いません。 |
| `slowMotionDuration` | 0 秒 | 命中後のスローを続ける時間です。 |

初期コンボでは、弱攻撃のヒットストップが 0.06〜0.07 秒、強攻撃が 0.11 秒、最強が 0.16 秒、超強が 0.26 秒です。攻撃が進むほど、押し出し・揺れ・振動も強くなります。超強攻撃は 0.25 倍速のスローと半径 4.5 m の衝撃波を使います。

## 移動と体の動き

場所: `PlayerController`。演出パネルで該当項目が OFF の時は、その動きが止まります。

| 項目 | 初期値 | 内容 |
|---|---:|---|
| `maxSpeed` | 7.5 m/s | プレイヤーの最高速度です。 |
| `acceleration` | 38 m/s² | 走り出しや速度を上げる時の加速です。 |
| `startBoost` | 2 倍 | 止まった状態からの加速倍率です。 |
| `deceleration` | 34 m/s² | 移動入力を離した時に止まる速さです。 |
| `turnSmoothTime` | 0.07 秒 | 向きを変える時の追いつきやすさです。小さいほど素早く向きます。 |
| `maxTurnSpeed` | 1080 度/秒 | 向きを変える最大の速さです。 |
| `gravity` | 25 m/s² | 地面へ押し戻す力です。 |
| `quickTurnAngle` | 110° | 逆向き入力を切り返しとみなす角度です。 |
| `quickTurnSpeedMultiplier` | 3 倍 | 切り返し時に向きを変える速さの倍率です。 |
| `maxLeanAngle` | 16° | 旋回時に体が傾く最大角度です。 |
| `leanPerTurnSpeed` | 0.03 | 旋回速度に対する傾きの量です。 |
| `runForwardLean` | 7° | 走っている時の前傾角度です。 |
| `leanPerAcceleration` | 0.3 | 加速に対する前傾の量です。 |
| `leanSharpness` | 12 | 体の傾きが追いつく速さです。 |
| `strideLength` | 1.15 m | ランボブの歩幅です。 |
| `bobHeight` | 0.05 m | 走った時に上下する高さです。 |
| `bobSwayAngle` | 2° | 走った時に左右へ揺れる角度です。 |

## 攻撃・剣の動き

場所: `PlayerCombat`。コンボ配列の項目は「攻撃ごとの設定」を見てください。

| 項目 | 初期値 | 内容 |
|---|---:|---|
| `comboKeepTime` | 0.3 秒 | 攻撃が終わった後、次段への入力を待つ時間です。 |
| `inputBufferTime` | 0.35 秒 | 先行入力を覚えておく時間です。 |
| `idleHandPosition` / `idleSwordEuler` | (0.45, 1, 0.1) / (40, 20, 0) | 待機中の手の位置と剣の角度です。 |
| `anticipationOvershoot` | 20° | 振りかぶりで、通常より余分に剣を引く角度です。 |
| `swordLagPerYawRate` | 0.025 | 体の旋回速度に対し、剣が遅れる量です。 |
| `swordLagPerAcceleration` | 0.6 | 加速に対し、剣が遅れる量です。 |
| `swordRunSwing` | 6° | 走りに合わせて剣が揺れる角度です。 |
| `swordLagSmoothTime` | 0.09 秒 | 遅れた剣が元の位置へ戻る速さです。 |
| `squashStiffness` | 320 | 体を縮めたり伸ばしたりするバネの強さです。 |
| `squashDamping` | 16 | 体の伸び縮みが収まる速さです。 |
| `windupSquashImpulse` | -2.2 | 振りかぶり時に体を縮める勢いです。 |
| `releaseStretchImpulse` | 3.2 | 振り抜き時に体を伸ばす勢いです。 |
| `bladeRadius` | 0.2 m | 剣の当たり判定の太さです。 |
| `sweepSubSteps` | 32 | 速く振った剣の軌道を何分割して調べるかです。値を上げるとすり抜けにくくなります。 |
| `assistRange` | 5.5 m | 攻撃補助が対象を探す距離です。 |
| `assistAngle` | 80° | 入力方向から対象を選ぶ範囲です。 |
| `assistStopDistance` | 1.3 m | 敵の手前で止まる距離です。 |
| `assistMaxLunge` | 3.5 m | 攻撃補助で踏み込める最大距離です。 |
| `heavyRecoilSpeed` | 2.5 m/s | 強攻撃でプレイヤーが押し戻される速さです。 |
| `finisherRecoilSpeed` | 4 m/s | 最強攻撃でプレイヤーが押し戻される速さです。 |
| `attackerShakeAmplitude` | 0.018 m | ヒットストップ中に剣を震わせる幅です。 |
| `attackerShakeFrequency` | 34 Hz | 剣の震えの細かさです。 |
| `longInputBufferTime` | 1 秒 | 「先行入力 1 秒」の検証例に使う受付時間です。 |

## カメラ

場所: `ThirdPersonCamera`。カメラの位置・揺れ・ズームを調整します。

| 項目 | 初期値 | 内容 |
|---|---:|---|
| `pivotOffset` | (0, 1.5, 0) m | カメラが見る基準点の位置です。 |
| `distance` | 5.2 m | 通常時のカメラ距離です。 |
| `mouseSensitivity` / `stickSensitivity` | 0.12 / 200 | マウスとスティックの視点操作の速さです。 |
| `minPitch` / `maxPitch` | -25° / 65° | 視点を上下に動かせる範囲です。 |
| `followSmoothTime` | 0.1 秒 | プレイヤーを追いかける時の遅れです。 |
| `lookAheadTime` | 0.12 秒 | 進行方向の何秒先を映すかです。 |
| `lookAheadSmoothTime` | 0.3 秒 | 先読み位置へ移るなめらかさです。 |
| `speedZoomDistance` | 0.8 m | 最高速時に追加でカメラを引く距離です。 |
| `speedZoomSmoothTime` | 0.5 秒 | 速度ズームが変わる速さです。 |
| `recenterDelay` | 1 秒 | 視点操作をやめてから自動で背後へ回るまでの待ち時間です。 |
| `recenterSpeed` | 70 度/秒 | 自動で背後へ回る速さです。 |
| `framingAngle` | 25° | 狙った敵が画面中央からこの角度以上ずれた時、カメラを寄せます。 |
| `framingSpeed` | 160 度/秒 | 攻撃中に敵へ画面を向ける速さです。 |
| `maxShakeOffset` | 0.28 m | カメラシェイクで移動する最大距離です。 |
| `maxShakeRoll` | 2.5° | カメラシェイクで傾く最大角度です。 |
| `shakeFrequency` | 30 Hz | カメラの揺れの細かさです。 |
| `traumaDecay` | 2.2 /秒 | 揺れの元になる値が減る速さです。 |
| `fovReturnTime` | 0.18 秒 | FOV を元へ戻す時間です。 |
| `chargeFov` | 7° | 超強攻撃の溜め切り時に FOV を狭める量です。 |
| `chargeDistance` | 0.9 m | 超強攻撃の溜め切り時にカメラが寄る距離です。 |
| `chargeSmoothTime` | 0.12 秒 | 溜め演出でカメラが寄る速さです。 |
| `lockOnSmoothTime` | 0.15 秒 | ロックオン対象へカメラが向き直る速さです。 |
| `lockOnPitch` | 12° | ロックオン中に視点操作していない時の上下角度です。 |

## ロックオン

場所: `PlayerLockOn`。

| 項目 | 初期値 | 内容 |
|---|---:|---|
| `lockRange` | 18 m | ロックオンできる距離です。 |
| `releaseRange` | 24 m | この距離より離れるとロックを解除します。 |
| `lockAngle` | 75° | カメラ正面から対象を探す範囲です。 |
| `switchStickThreshold` | 0.8 | 右スティックをこの量以上倒すと対象を切り替えます。 |
| `markerColor` | 橙 | ロックオンマーカーの色です。 |
| `markerSize` | 46 px | 1080p を基準にしたマーカーの大きさです。 |

## 火花・ヒットエフェクト

場所: `HitSparkSystem`。`lightTier`・`heavyTier`・`finisherTier`・`ultraTier` の各プロファイルを調整します。どの格にも同じ項目があり、攻撃の格ごとに値を変えられます。

| 項目 | 初期値の例 | 内容 |
|---|---:|---|
| `label` | 弱 | Inspector で見分けるための表示名です。 |
| `sparkCount` | 16 | 飛び散る火花の数です。 |
| `sparkSpeed` | 4〜11 m/s | 火花が飛ぶ速さの範囲です。 |
| `sparkLifetime` | 0.12〜0.35 秒 | 火花が消えるまでの時間の範囲です。 |
| `sparkSize` | 0.018〜0.035 m | 火花の大きさの範囲です。 |
| `sparkSpread` | 40° | 斬撃方向を中心に火花が広がる角度です。 |
| `secondaryBurstCount` | 0 | 少し遅れて追加で出す火花の数です。0 なら出ません。 |
| `secondaryBurstDelay` | 0.05 秒 | 追加の火花が出るまでの待ち時間です。 |
| `flashSize` | 0.6 m | 命中時の閃光の大きさです。 |
| `flareWidth` | 1.2 m | 横長のレンズ風の光の幅です。 |
| `slashWidth` | 0.06 m | 剣筋の光の線の太さです。 |
| `ringCount` | 0 | 強攻撃以上で出す衝撃波リングの数です。 |
| `ringSize` | 2.2 m | 衝撃波リングの大きさです。 |
| `emberCount` | 4 | ゆっくり漂う火の粉の数です。 |
| `debrisCount` / `smokeCount` | 0 | 金属片と煙の数です。 |
| `lightLumen` | 400,000 lm | 命中位置のライトの明るさです。 |
| `lightDuration` | 0.1 秒 | 命中ライトが光る時間です。 |
| `lightColor` | 橙系 | 命中ライトの色です。 |
| `chargeParticleRate` | 140 個/秒 | 超強攻撃の溜め切り時に集まる光の粒の数です。 |
| `chargeColor` | 青白 | 溜めの光の色です。 |

火花・火の粉・閃光・フレア・剣筋・リング・破片・煙には、それぞれ素材を割り当てます。`groundPlane` は火花や破片が跳ねる床です。未設定なら地面の高さを 0 として扱います。

## 画面効果

場所: `ScreenFxController`。攻撃データの `screenFxStrength` が、下記の強さに掛け合わされます。

| 項目 | 初期値 | 内容 |
|---|---:|---|
| `chromaticAberrationPeak` | 0.9 | 画面端の色ずれの最大値です。 |
| `lensDistortionPeak` | -0.28 | 画面を押し込む／膨らませるゆがみの最大値です。 |
| `radialBlurPeak` | 0.12 | 画面中心から外へ流れるぼかしの最大値です。 |
| `impactExposurePeak` | 1.3 | 命中時に画面を明るくする量です。 |
| `impactSaturationPeak` | -60 | 命中時に色を薄くする量です。 |
| `chromaticAberrationDuration` | 0.22 秒 | 色ずれが戻るまでの時間です。 |
| `lensDistortionDuration` | 0.35 秒 | ゆがみが戻るまでの時間です。 |
| `radialBlurDuration` | 0.2 秒 | 放射状ぼかしが戻るまでの時間です。 |
| `impactColorDuration` | 0.4 秒 | 明るさ・色の変化が戻るまでの時間です。 |
| `chargeVignette` | 0.25 | 超強攻撃の溜め切り時に画面の隅を暗くする量です。 |
| `chargeExposure` | -0.6 | 溜め切り時に画面を暗くする量です。 |
| `chargeSaturation` | -35 | 溜め切り時に色を薄くする量です。 |
| `impactFrameDuration` | 0.07 秒 | 白黒の閃光を出す時間です。 |
| `hitMarkerDuration` | 0.2 秒 | 命中マークを表示する時間です。 |
| `hitMarkerLength` / `hitMarkerThickness` | 14 / 3 px | 1080p 基準の命中マークの長さと太さです。 |

## 敵の反応・撃破

場所: `Enemy`。

| 項目 | 初期値 | 内容 |
|---|---:|---|
| `chasePlayer` | OFF | ON なら敵がプレイヤーへ近づき、OFF ならその場で反応するかかしになります。 |
| `weebleTiltPerStagger` | 1.6 | のけぞり角度を、かかしの傾きへ変える倍率です。 |
| `weebleTiltPerKnockback` | 8 | ノックバック距離を、かかしの追加の傾きへ変える倍率です。 |
| `weebleMaxTilt` | 80° | かかしが傾ける最大角度です。 |
| `weebleFrequency` | 1.2 Hz | かかしの揺れ戻りの速さです。 |
| `weebleDamping` | 2.8 | かかしの揺れが収まる速さです。 |
| `knockbackDamping` | 9 | 押し出された敵が止まる速さです。 |
| `flashDuration` | 0.1 秒 | 被弾フラッシュの長さです。 |
| `modelShakeAmplitude` | 0.06 m | 敵の見た目を震わせる幅です。 |
| `modelShakeFrequency` | 30 Hz | 敵の見た目を震わせる速さです。60 fps では 30 Hz が細かい震えに見えます。55 Hz などフレームレートに近い値は、ゆっくり揺れて見える場合があります。 |
| `recoilStiffness` | 260 | 加算モーションのバネの硬さです。 |
| `recoilDamping` | 14 | 加算モーションの揺れが収まる速さです。 |
| `recoilImpulse` | 220 | 加算モーションを動かし始める勢いです。 |
| `maxHp` | 100 | 敵の最大 HP です。 |
| `moveSpeed` / `turnSpeed` | 2.4 m/s / 300 度/秒 | 追いかける敵の移動速度と向き直る速さです。 |
| `stopDistance` | 1.7 m | プレイヤーに近づいた時に止まる距離です。 |
| `separationRadius` | 1.3 m | 敵同士が重ならないようにする範囲です。 |
| `bodyRadius` / `bodyCenterHeight` | 0.5 m / 1 m | 敵の当たり判定の太さと中心の高さです。 |
| `airGravity` | 25 m/s² | 打ち上げられた敵が落ちる速さです。 |
| `bounceSpeedThreshold` | 5 m/s | 着地時にこれ以上の速度があれば跳ね返ります。 |
| `bounceRestitution` | 0.3 | 着地後に跳ね返る強さです。 |
| `getUpDelay` | 0.6 秒 | 着地してから起き上がり始めるまでの時間です。 |
| `juggleUpSpeed` | 3.5 m/s | 空中で追撃された敵を浮かせ直す速さです。 |
| `spawnRiseDuration` | 0.4 秒 | 敵が地面から現れる時間です。 |
| `deathTellDuration` | 0.16 秒 | 撃破後、倒れ始める前に止まる時間です。 |
| `toppleDuration` | 0.38 秒 | 敵が倒れる時間です。 |
| `lieDuration` | 0.7 秒 | 倒れた敵がその場に残る時間です。 |
| `vanishDuration` | 0.45 秒 | 敵が消えるまでの時間です。 |

## 音・時間・移動エフェクト

| コンポーネント・項目 | 初期値 | 内容 |
|---|---:|---|
| `HitAudio.hitVolume` | 0.8 | 命中音の音量です。 |
| `HitAudio.swingVolume` | 0.45 | 空振りを含む剣の振り音の音量です。 |
| `HitAudio.pitchVariation` | 0.06 | 音を鳴らすたびに音程をずらす幅です。 |
| `TimeController.hitStopTimeScale` | 0 | ヒットストップ中の時間の速さです。0 は完全停止、0.05 はほぼ停止です。Inspector で設定できる範囲は 0〜0.3 です。 |
| `HitFeedback.finisherSlowTimeScale` | 0.2 | 最強攻撃で敵を倒した時の時間の速さです。 |
| `HitFeedback.finisherSlowDuration` | 0.7 秒 | 最強攻撃の撃破スローを続ける時間です。 |
| `HitFeedback.whiffSlamHitStop` | 0.07 秒 | 叩きつけ攻撃が空振りした時に止める時間です。 |
| `HitFeedback.whiffSlamShake` | 0.55 | 叩きつけが空振りした時のカメラ揺れです。 |
| `MovementDust.startStrength` | 0.7 | 走り出しの砂煙の強さです。 |
| `MovementDust.quickTurnStrength` | 1 | 切り返しの砂煙の強さです。 |
| `MovementDust.stopStrength` | 0.8 | 急停止の砂煙の強さです。 |
| `MovementDust.footstepStrength` | 0.2 | 全力疾走時の足跡の砂煙の強さです。 |
| `MovementDust.footstepSpeed01` | 0.9 | 最高速の何割で足跡の砂煙を出し始めるかです。 |
| `SwordTrail.lifetime` | 0.13 秒 | 剣の軌跡が残る時間です。 |
| `SwordTrail.subdivisions` | 6 | フレーム間の剣の動きを補間する分割数です。 |
| `SwordTrail.color` | 水色 | 剣の軌跡の色です。 |
| `SpeedLinesOverlay.minSpeed` | 6.5 m/s | スピードラインが出始める速度です。 |
| `SpeedLinesOverlay.maxSpeed` | 14 m/s | スピードラインが最大になる速度です。 |
| `SpeedLinesOverlay.lineCount` | 36 | 画面外周に出す線の数です。 |
| `SpeedLinesOverlay.refreshInterval` | 0.045 秒 | 線を描き直す間隔です。短いほど線が細かく流れて見えます。 |
| `SpeedLinesOverlay.innerRadius` | 0.62 | 画面中央を空ける大きさです。0〜1 の割合です。 |
| `SpeedLinesOverlay.color` | 白半透明 | スピードラインの色です。 |

## HP・数値・コンボ表示

場所: `CombatHud`。

| 項目 | 初期値 | 内容 |
|---|---:|---|
| `gaugeSize` | 96 × 9 px | 敵 HP ゲージの幅と高さです。 |
| `gaugeHeightOffset` | 0.35 m | 敵の頭からゲージを上に離す距離です。 |
| `gaugeHoldTime` | 0.45 秒 | HP が減った後、白い残りゲージを止めておく時間です。 |
| `gaugeDrainSpeed` | 1.4 /秒 | 白い残りゲージが減る速さです。 |
| `gaugeMaxDistance` | 22 m | この距離より遠い敵の HP ゲージは隠します。 |
| `numberLifetime` | 0.75 秒 | ダメージ数値を表示する時間です。 |
| `numberRise` | 0.7 m | ダメージ数値が上がる高さです。 |
| `numberFontSize` | 26 px | ダメージ数値の文字サイズです。 |
| `comboTimeout` | 2.2 秒 | 次のヒットがないとコンボが途切れるまでの時間です。 |

## 検証用の表示

| 項目 | 初期値 | 内容 |
|---|---:|---|
| `AttackTimelineView.timeScale` | 1.4 秒 | 攻撃フレーム表示の横幅が表す時間です。 |
| `AttackTimelineView.holdTime` | 2.5 秒 | 攻撃が終わってからフレーム表示を残す時間です。 |
| `HitFeelDebugPanel.showOnStart` | ON | 再生開始時に演出パネルを表示します。 |

攻撃フレーム表示・入力遅延・長すぎる先行入力の切り替えは、[ヒット演出と切り替え項目](https://github.com/R-production004682/3D_ActionGame_CombatTest/wiki/Hit-Feel)に説明があります。

[トップページへ戻る](https://github.com/R-production004682/3D_ActionGame_CombatTest/wiki)
