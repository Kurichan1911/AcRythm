# AcRhythm 引き継ぎドキュメント

作成日: 2026-11-24 / 対象: Unity 6000.5.2f1 の 3D アクション × リズムゲーム（プロトタイプ段階）
このドキュメントは、別の AI がこのプロジェクトの作業を引き継ぐためのものです。**先に「2. ユーザーとの作業ルール」を必ず読んでください。**

---

## 1. プロジェクト概要

- ゲーム像: **DarkSouls のようなアクションゲームにリズム要素を足したもの**。
  - 3D アクション。リズムに合わせて攻撃・防御・パリィを行う。
  - 敵の攻撃もリズムに合わせて行われる（「構え＝予備動作」→「攻撃の着弾」）。
  - 楽曲の BPM を自動で読み取り、曲の BPM に合わせてプレイのテンポも変わる（C フェーズで実装）。
- 拍の表現: 敵や HUD のエフェクトが曲にノる。プレイヤーキャラクターも曲にノる。
- 曲: ゲーム側が用意する内蔵曲に加え、**プレイヤーが用意した音楽ファイルもゲーム内で使えるようにする**。対応形式は **wav と ogg**、ストリーミング読み込みも実装予定。
  - オーディオは **Unity 内蔵のオーディオシステム**を使う。CRI ADX（ADX LE の環境はあるが）は**不採用**。
  - BPM 取得はロード画面で行う。BPM 解析ロジックは内蔵曲用ツールとカスタム曲で**共通化**する。内蔵曲はインポート時にオフライン解析。システムは自作。
- 最初のプロトタイプ範囲: メトロノームのみの音声（`Metronome_120bpm.wav`）、敵 1 種（ダミー）、攻撃・防御・パリィ、パリィ後の致命攻撃。

## 2. ユーザーとの作業ルール（最重要）

1. **新規スクリプトは「全文」をチャット上で見せる。既存スクリプトについては、全体の 6 割以上に変更が加えられる場合を除いて変更点のみを提示する**。ヒントだけ、ファイル渡しは不可。ユーザーが自分で打ち込む。
2. ユーザーは**Unity の編集操作を 1 から優しく**教えてほしい（右クリックのメニュー名、どの欄に何をドラッグするか、まで書く）。
3. **ユーザーの命名規則に合わせる**: クラス名・メンバーともに lowerCamelCase（例: `beatClock`, `playerCombat`, `enemyBase`, `secPerBeat`, `currentBeat`）。
4. **ユーザーが発言した際には、毎回 Unity プロジェクト（`AcRythm` フォルダ）のデータをチェックする**。Unity のプロジェクトデータ（`Assets/`・`ProjectSettings/`・`Packages/` 等）は**読み取り専用**とし、特別な許可がない限り変更を加えない（編集してよいのは `AcRythm/AntigravityWorkSpace/handoff_AcRhythm.md` のみ）。最新のプロジェクトファイルを必ず読み直してから診断する（過去に古いバージョンから誤診した前例がある）。ミスはユーザーのコードでも自分の説明でも率直に指摘・訂正する。
5. 判断の時刻は**すべて `AudioSettings.dspTime`** で統一する。人間向けの窓は ms、リズム定義は拍／ティックで持つ。
6. 調整値（パリィ窓など）は「後でまとめて調整する」とユーザーが述べている。今は勝手に決め込まず、案として提示する。
7. 返答は日本語。
8. 条件の変更や進捗状況の変更に応じて、この `AcRythm/AntigravityWorkSpace/handoff_AcRhythm.md` を随時編集・更新する。

## 3. 技術前提

- Unity 6000.5.2f1。**新 Input System をデバイスから直接読む**（`Keyboard.current`, `Mouse.current`, `Gamepad.current`）。`using UnityEngine.InputSystem;`
- `CharacterController`、`ScriptableObject`、`[DefaultExecutionOrder]` を使用。
- Inspector は interface フィールドをシリアライズできないため、共通窓口は **abstract な基底クラス**（`enemyBase`）にしている。
- 拍の時計: `beatClock`（dspTime ベース）。`PlayScheduled` で startDelay 0.5 秒後に再生開始。
  - `/home/claude/proto/BeatClock.cs`・`BeatPulse.cs` は**最初にアシスタントが書いた PascalCase 版**（`SecPerBeat`, `CurrentBeat`, `BeatToDspTime`, `OnBeat`）。ユーザーの実際のスクリプトは lowerCamelCase（`beatClock`, `secPerBeat`, `currentBeat`）に直してある。**正本はユーザーの手元の Unity プロジェクト**で、このサーバ上の proto は古い。

### 3.1 3D モデル・ワールドのスケール基準（モデル制作依頼時の目安）
- **基本単位**: **`1 Unity Unit = 1m`**（実寸スケール、インポート時 `Scale = (1, 1, 1)`）。
- **原点（Pivot）・向き**: キャラクター・敵は**両足裏の地面中央（`Y = 0`）を原点**、`+Z` が正面、`+Y` が上。武器は**グリップ（握る位置）を原点**。
- **キャラクターサイズ目安**:
  - プレイヤー: 身長 `1.7m 〜 1.8m`
  - 通常エネミー（人型）: `1.8m 〜 2.2m`（3 人称の背中越しカメラでも敵の予備動作が見やすいよう一回り大きめ推奨）
  - 中型敵: `2.5m 〜 3.5m` ／ 大型ボス: `4.0m 〜 6.0m`
- **武器全長目安**: 短剣 `0.4〜0.5m`、直剣・盾剣 `0.9〜1.1m`（盾 `0.6〜0.8m`）、太刀 `1.2〜1.4m`、大剣・大槌 `1.6〜2.0m`。
- **ワールドサイズ目安**: 雑魚戦エリア `15〜25m` 四方、通路幅 `4〜6m` 以上（天井高 `4〜5m` 以上）、ボス戦アリーナ直径 `30〜45m`。

## 4. これまでに決まった設計（確定事項）

### 4.1 進行順序
**A → B → C → D**（ユーザー承認済み）
- A = プロトタイプ完成（敵の攻撃パターン → 敵の共通窓口 → プレイヤー HP/被弾 → 拍の見た目と手応え）
- B = 手触りの調整
- C = 曲・BPM 機能
- D = ゲームの広がり（ロックオン、アニメーション、敵の種類、ボス、メニュー）

### 4.2 敵の攻撃
- 攻撃パターンは Inspector で設定する ScriptableObject（メニュー `Rhythm/Attack Pattern`）。
- **構え（windup）と攻撃（attack）は別リスト**で管理（例: 3 連符の 1・2 個目で構え、3 個目で攻撃、という指示ができる）。
- 拍の位置は `beatTime` 構造体: `beat + subdivision + subIndex`、**1 拍 = 480 ティック**（3 連符が割り切れる）。
- 将来 3 連符など複雑なリズムにも対応できる設計。
- 敵の攻撃は着弾時刻 (`impactDsp`) と `damage`（既定 10）を持つ。

### 4.3 パリィ
- 判定は**入力時刻**基準。着弾 `impact` が `[parryStartDsp − parryLate, parryStartDsp + parryWindow]` に入れば成功。
- 敵は着弾の `parryLateTolerance`（= `parryLate`）秒**後**に `ReceiveAttack` を呼ぶ（遅れ入力を待つ「判定の遅延呼び出し」）。
- 成功したらパリィ入力を消費（`parryStartDsp = -∞`）。同拍に複数敵が攻撃しても 1 回のパリィで弾けるのは 1 体のみ（ロックオン実装時に再検討）。
- 失敗（空振り）すると硬直（`parryRecovery`）。
- 成功すると敵は**崩れ（stagger）**状態になり、パターンは中断され、**次のグリッド拍（4 拍刻み、`gridBeats = 4`）から再開**。
- 崩れは時間ベース（`staggerEndDsp`、`isStaggered` は dspTime から算出）。`staggerBeats` は敵ごとに設定可能（2 超の値も使う）。構えが実行されるべき拍に崩れているときは、その拍の構えと攻撃を実行しない。
- **致命攻撃（riposte）**: 崩れ中の敵に 1 回だけ当たる。当てたら崩れは終わる。パリィは「大ダメージを狙うリスクとリターン」の位置づけ。致命攻撃の入力は自由入力（拍に縛らない）。
- パリィは**武器が変わっても同じ判定**（後で変更する可能性あり）。大剣・太刀はパリィ不可。

### 4.4 攻撃（プレイヤー）
- 入力は**自由入力**（拍に縛らない）。ただし**最寄りの拍とのズレ（ms）が小さいほどダメージが大きい**。
- ダメージ = `attackDamage × damageCurve.Evaluate(Clamp01(|ズレms| / falloffMs))`（武器導入後はさらに `weaponData.damageMultiplier` を掛ける）。既定カーブは `AnimationCurve.Linear(0, 2, 1, 0.5)`、`falloffMs = 200`。
- JUST ≤ 40ms / GOOD ≤ 90ms はデバッグ表示用（`Debug.Log`）。
- **当たり判定の方針**: プロトタイプ段階では距離判定（`ApplyAttackHit`）で仮実装し、**将来的（D フェーズ）には武器モデルの当たり判定（コライダー）でヒットを決める**。

### 4.5 被弾
- 被弾 100%、ガード 30%（`guardDamageMultiplier`）、パリィ成功 0%、**カウンター成功はガードよりさらに軽減**（案: 15%）。
- プレイヤー最大 HP 200。HP 0 で `Down` 状態（何もできない）。

### 4.6 ロックオン（未実装）
- ロックオンを実装したい。ロックオン中に**右スティックを左右に倒すと、その方向にいる別の敵へ対象を切り替える**機能も。D フェーズ。

### 4.7 入力割り当て（確定）

| 操作 | キーボード・マウス | ゲームパッド |
|---|---|---|
| 主攻撃 | 左クリック | R1 |
| サブ入力（武器ごと。大剣のチャージ等） | 右クリック | L1 |
| ガード（押している間） | C | R2 |
| パリィ | F | L2 |

## 5. 武器システム設計（今ここ）

武器種ごとに攻撃の入力方法が変わる。実装はこれから（W1 から）。設計の詳細・譜面のピアノロール・BPM 換算は添付の **`weapon_rhythm_design.xlsx`**（7 シート）にまとめてある。

### 5.1 表記
- ●押す ／ ━押し続け ／ ○離す ／ ◆攻撃判定 ／ ▼敵の着弾。1 拍あたりのマス数 = 拍の細かさ（8 分=2、3 連符=3、16 分=4 など）。
- 系統: タップ系 / ホールド系 / 二ボタン系 / 待ち（カウンター）系。

### 5.2 武器一覧（状態: 確定 = ユーザーが決定、案 = アシスタント提案で未確認、候補 = 未採用）

| 武器 | 状態 | 入力 | 判定の基準 | 備考 |
|---|---|---|---|---|
| 短剣 | 確定 | タップ。押した瞬間に攻撃判定 | 押す瞬間（8 分まで連打） | 攻撃距離が短い（1.6m）＋8 分の手数＋致命攻撃倍率高め（1.3）。タイミングの厳しさは全武器共通で `timingWindowMul` から調整可能 |
| 直剣 | 確定 | 1 拍長押し → 離す | 押す＋離す | 扱いやすい標準。攻撃距離はある程度。攻撃力の基準 1.0。攻撃判定は離した瞬間 |
| 大剣 | 確定 | 右（サブ）でチャージしながら、左クリックをリズムに合わせて押す → 右を離して解放 | 押す（最大 3 回蓄積）＋離す（解放ボーナス） | 大ダメージ。**パリィ不可**。チャージ中は**被ダメージ 0.8 倍＋ひるみにくい**（強靭 `subChargePoise` を超えると中断）。右を離す瞬間も拍の頭に合わせるとさらに威力上昇 |
| 盾剣 | 確定 | 半拍ホールド（拍の頭で押し、裏拍で離す） | 押す＋離す | 攻撃力は通常程度。**カウンター: 押す瞬間を敵の着弾に合わせる**（敵の攻撃後すぐ反撃）。パリィ可 |
| 太刀 | 確定 | 1 拍ホールド（拍の頭で押し、次の拍の頭で離す） | 押す＋離す | **パリィ不可**。通常攻撃力は直剣より少し低い。**カウンター: 長押し中に着弾タイミングで離す（居合）**、倍率が高い |
| 曲剣 | 案 | 3 連符で 3 回タップ | 押す瞬間 | 全て合うと最後が強攻撃。外すとコンボ切れ |
| 槌 | 案 | 2 拍長押し → 離す | 押す＋離す | 崩し・ガード破り。離しが拍の頭に合うと崩れる案 |
| 大槌 | 案 | 4 拍（1 小節）長押し → 離す | 押す＋離す | 一発に賭ける。溜め中の移動・パリィ不可、スーパーアーマーかは未決 |
| 双剣 | 候補（未採用） | 左右を 8 分で交互押し | 押す瞬間 | 採否は未決 |

※ 槍は案から削除済み。

### 5.3 判定ルール（確定）
- ホールド系は**押す瞬間と離す瞬間の両方**を判定（重み付き平均で合成。初期案 0.5 / 0.5。武器ごとに変えるかは未決）。
- **最短入力間隔ルール**: 武器のリズムの刻みが最短入力間隔より短くなる（高 BPM）場合、**2 倍の長さ（8 分音符 → 4 分音符など）にして判定**する。最短入力間隔の初期案は 150ms。換算結果: 120BPM では 8 分音符 = 250ms だが、**8 分音符の刻みは BPM220 以上で 2 倍化が起きる**（xlsx「BPM換算」シートが式で連動）。
- **カウンター**:
  - 窓はパリィより少し厳しい。盾剣は太刀より広く（ほぼパリィと同じ合計）、太刀はさらに少し厳しい。
  - 対象は将来は「武器を振ったときの当たり判定」で決める。実質は**攻撃してきた敵本人**に返す。
  - 成功時の被ダメージはガードより軽減。
  - 初期案（調整前提）: 盾剣 前 40ms／後 50ms、倍率 ×2。太刀 前 30ms／後 40ms、倍率 ×4。

### 5.4 パラメータ初期値（xlsx「パラメータ」シートより）

| 項目 | 値 | 状態 |
|---|---|---|
| パリィ 早押し猶予（着弾の前） | 60ms | 現在値 |
| パリィ 遅押し猶予（着弾の後） | 40ms | 現在値 |
| パリィ空振り後の硬直 | 400ms | 現在値 |
| 被弾／ガード／パリィ成功／カウンター成功 | 100%／30%／0%／15% | 現在値／現在値／現在値／**案** |
| 最短入力間隔 | 150ms | 案 |
| 攻撃動作の長さ（硬直） | 1 拍 | 現在値（武器ごとは未定） |
| ホールドの押し／離し重み | 0.5／0.5 | 案 |
| 通常攻撃力 | 直剣 1.0／盾剣 1.0／太刀 0.8 | 現在値／案／案 |

**注意**: パリィ窓の式は修正済み（旧: `parryEndDsp + parryWindow` で窓が二重になっていた → 現: `parryStartDsp + parryWindow`）。**修正により実効の「早押し側」の窓が半分になった**ため、Inspector の値（parryWindow / parryLate）は B フェーズで再調整が必要。ユーザーが直近で使っていた手触りの値は parryWindow = 0.06、parryLate = 0.04（ただし式修正前の挙動に対する値）。調整はユーザーが「あとでやる」と述べている。

## 6. 実装の現状（ユーザーの Unity 側のスクリプト）

> 以下は直近に確認できた状態。ユーザーの最新版は添付で確認すること。

| ファイル | 内容 |
|---|---|
| `beatClock.cs` | dspTime ベースの拍時計。`secPerBeat`, `currentBeat`, `songTime`, `isPlaying`, `Bpm`, `BeatToDspTime(beat)`, `OnBeat` イベント。`PlayScheduled`（startDelay 0.5 秒） |
| `beatPulse.cs` / `beatBob.cs` | 拍に合わせて膨らむ／上下に揺れる見た目（確認用） |
| `playerMove.cs` | 移動。`speedMultiplier` プロパティ（旧 "Multipleir" の綴りは修正済み）、`turnSpeed` |
| `followCamera.cs` | マウス＋右スティックで視点操作 |
| `beatTime.cs` | `ticksPerBeat = 480`、`ToTicks()`、`ToBeats()`、`IsValid` |
| `attackPattern.cs` | `windupCue { beatTime time }`、`attackCue { beatTime impact; float damage = 10 }`、`attackPattern { beatTime length; List<windupCue> windups; List<attackCue> attacks; OnValidate で警告 }` |
| `enemyAttacker.cs` | `[DefaultExecutionOrder(10)]`。パターンを dspTime で実行。`windupRuntime` / `attackRuntime`、`ProcessEvents`、パリィ時 `InterruptPattern`、`NextGrid`、`BeatOf`。着弾時 `player.ReceiveAttack(impactDsp, damage)` |
| `enemyBase.cs` | abstract。static `all` リスト、HP、`staggerBeats`、`isStaggered`/`isDown`/`staggerEndDsp`、abstract `ShowIdle/ShowWindup/ShowAttack`、protected `ShowStagger/ShowHit/ShowDown`、`StartStagger`、`TakeDamage`、`TakeRiposte`、`CheckDown`（倒れたら `enabled = false`） |
| `dummyCombat.cs` | `class dummyCombat : enemyBase`。Renderer の色で状態表示 |
| `playerCombat.cs` | 状態 enum `playerAction { Free, Attack, Guard, Parry, Recovery, Riposte, Down }`、`defenceResult { Hit, Guarded, Parried }`。`FindTarget(range, requireStaggered)`、`StartAttack` / `StartParry` / `StartRiposte`、`ReceiveAttack(double impactDspTime, float damage)`、`TakeDamage`、OnGUI の HP 表示 |
| `playerInput.cs` | **W1 で新規作成済み**。`inputButton` + `[DefaultExecutionOrder(-10)]` の `playerInput`（`attack`: 左クリック/R1、`sub`: 右クリック/L1、`guard`: C/R2、`parry`: F/L2） |
| `weaponData.cs` | **W2 で新規作成済み**。`weaponInputType` / `counterTrigger` / `weaponData : ScriptableObject`（`GetEffectiveBeats`、`OnValidate` 付き）。`Assets/Weapons/` に 5 種（直剣・短剣・盾剣・刀・大剣）作成済み |
| `weaponRangeView.cs` | **W2.5 で新規作成済み**。プレイ中に現在武器の `attackRange` を足元のリング（`LineRenderer`）で可視化（範囲内に敵がいると色変化） |

### 6.1 直近の状態（W6 完了 → W7 開始）
- **W6 完了（2026-11-26）**: 太刀（1 拍ホールド＋離す瞬間を着弾に合わせる居合カウンター `OnRelease`）の実装・動作確認が完了。（あわせて `prototype.unity` にてテスト用に `maxHp = 1000`、`damageCurve` 最大倍率 `1.5` への調整を確認）
- **W7 進行中**: 大剣の仕様が確定（サブ入力長押し中に主攻撃で最大 3 回蓄積、チャージ中は被ダメージ 0.8 倍＋ひるみ耐性、サブ入力を離して解放＋離しの拍合わせで威力ボーナス）。`weaponData.cs` と `playerCombat.cs` の変更点を提示済み。動作確認待ち。

## 7. 既知の問題・未対応メモ

- 修正済みを確認したもの（2026-11-24）: `enemyAttacker.cs` の不要な `using UnityEditorInternal;` / `UnityEngine.UIElements;`、`playerCombat.cs` の不要な `using`、`followCamera.cs` の右スティック `Time.deltaTime`、`playerMove.cs` の `turnSpeed`・`speedMultiplier`。
- `beatClock.cs`: 未使用の `using System.ComponentModel.Design;` 等 3 行が残っている（ビルドエラーにはならないが整理候補）。
- 過去の綴りミス: フォルダ名・一部アセットに "Rythm" が残っている（現状維持）。
- 同じ拍に複数の敵が攻撃したときの扱いは、ロックオン実装時に再検討（現状は 1 回のパリィで 1 体のみ）。
- ダミー敵の見た目はキャラクター本体ではなく Renderer の色変更のみ。

## 8. ロードマップ（これから行うこと）

| 区分 | ステップ | 内容 | 状態 |
|---|---|---|---|
| プロトタイプ | A1 | 敵の攻撃パターン（構えと攻撃の別リスト、3 連符対応） | 完了 |
| | A2 | 敵の共通窓口（`enemyBase`）、範囲内の最も近い敵を攻撃対象に | 完了 |
| | A3 | プレイヤー HP と被弾（ガードは軽減） | 完了 |
| | A4 | 拍の見た目と手応え（HUD の拍表示・HP バー、SE、敵のノリ） | 未着手 |
| 武器 | W1 | 入力の型の整理（`playerInput`）。挙動は不変 | 完了 |
| | W2 | 武器データ `weaponData`（ScriptableObject）: 譜面、倍率、カウンターの窓を Inspector で設定（＋5 種の武器アセット作成） | 完了 |
| | W2.5 | 現在武器の Range をプレイ中に表示（足元リング＋HUD） | 完了 |
| | W3 | 直剣: 押す＋離すの 1 拍ホールドで攻撃（＋SubCharge 中の可視化） | 完了 |
| | W4 | 短剣（タップ・8 分連打） | 完了 |
| | W5 | 盾剣（半拍ホールド＋カウンター）＋武器別タイミング厳しさ倍率 | 完了 |
| | W6 | 太刀（1 拍ホールド＋居合カウンター） | 完了 |
| | **W7** | **大剣（サブ入力チャージ：最大3回蓄積・被ダメ0.8倍＆ひるみ耐性・拍合わせ解放）** | **進行中** |
| | W8 | 槌・大槌・曲剣・（双剣） | 次 |
| 調整 | B | パリィ・カウンター・ダメージ倍率・崩れの長さ・入力の遅れ補正（パリィ窓の再調整を含む） | 未着手 |
| 曲 | C1 | `SongData`（BPM・最初の拍のオフセット）で内蔵曲を差し替え | 未着手 |
| | C2 | BPM 変化への対応（アニメ速度、最短入力間隔の倍化） | 未着手 |
| | C3 | カスタム曲の読み込み（wav／ogg）とストリーミング再生 | 未着手 |
| | C4 | BPM 解析（共通の `IBpmAnalyzer`）、ロード画面、キャッシュ、手動補正 UI | 未着手 |
| | C5 | 内蔵曲用のエディタ側解析ツール | 未着手 |
| 広がり | D | ロックオン（右スティック左右で対象切替）、アニメーション、敵の種類、ボス、メニュー | 未着手 |

### 次にやる W2 の想定（アシスタントの案、未提示）
- `weaponData : ScriptableObject`（メニュー例: `Rhythm/Weapon`）。武器名、系統、攻撃力倍率、攻撃距離、硬直（拍）、パリィ可否、最短入力間隔の扱い、ホールド長（拍）、押す／離す重み、カウンター設定（基準=押す／離す、窓 前ms／後ms、倍率、被ダメージ割合）を保持。
- `playerCombat` が現在武器の `weaponData` を参照して攻撃を処理するように段階的に移行（W3 で直剣から）。

## 9. 未決事項（ユーザーと詰める）

- 槌・大槌・曲剣の数値、パリィ可否、通常攻撃力、硬直、崩し／ガード破りの扱い
- ホールド系の押し・離しの重み（初期 0.5 / 0.5。武器ごとに変えるか）
- 最短入力間隔の値（150ms は案。遊んで決める）
- 大槌の溜め中の扱い（無防備か、スーパーアーマーか）
- 同拍に複数の敵が攻撃した場合の優先（ロックオン中の敵を優先する案）
- 双剣を採用するか

## 10. 成果物・参考ファイル

- `/home/claude/weapon_rhythm_design.xlsx` — 武器設計まとめ（シート: 武器一覧／譜面／パラメータ／BPM換算／共通ルール／決定・未決／ロードマップ）。ユーザーに送信済み。生成スクリプトは `/tmp/claude-0/-home-claude/b7d5d617-6a23-5669-882b-76d1c4dd011f/scratchpad/build_weapon_xlsx.py`（openpyxl。LibreOffice で再計算・PDF 描画して確認する）。
- `/home/claude/proto/` — 初期の `BeatClock.cs`・`BeatPulse.cs`（PascalCase の古い版）、`Metronome_120bpm.wav`。
- 会話の全記録: `/root/.claude/projects/-home-claude/b7d5d617-6a23-5669-882b-76d1c4dd011f.jsonl`
- 決定事項のメモ: メモリ `/areas/game-idea.md`（このセッションのメモリ機能で読める）。

## 11. 引き継ぎ時の最初の一手

1. ユーザーに W1 の動作確認結果を聞く（または、最新の `playerCombat.cs` / `playerInput.cs` の添付を依頼する）。
2. 問題なければ W2（`weaponData`）の全スクリプトをチャットで提示し、Unity での作成・設定手順を 1 から説明する。
3. 毎回、新しい添付があれば、**最新版を読み直してから**レビューする。
