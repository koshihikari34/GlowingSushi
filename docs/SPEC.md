# GlowingSushi 仕様書

## 1. 概要・目的

AR + VPS を活用した iPhone 向けアプリ。光る寿司が魚群のように泳ぎ、近づいて触ると逃げる。
屋外では Immersal VPS を用いて、街中や空中の決まった実世界座標に寿司を固定配置する。

アーキテクチャは MVVM を採用し、ViewModel が View に依存しない方向を厳守する
(`View → ViewModel → Service → Domain`)。

## 2. 技術スタック(現状調査結果)

| 項目 | 内容 |
|---|---|
| Unity | 6000.3.13f1 |
| レンダーパイプライン | URP (`PC_RPAsset` / `Mobile_RPAsset`)。`SampleSceneProfile` に Bloom が有効化済み → 発光表現に流用 |
| AR | AR Foundation。ARKit / ARCore / OpenXR / Simulation ローダー設定済み。iOS(ARKit)は即デプロイ可能 |
| DI | VContainer 1.19.0 (`jp.hadashikick.vcontainer`) |
| リアクティブ | R3 1.3.1 (`com.cysharp.r3`) + ObservableCollections.R3 (`org.nuget.observablecollections.r3`) |
| VPS | Immersal SDK 2.3.0 (`com.immersal.core`) |
| 寿司モデル | `Assets/GlowingSushi/Models/J-food04/sushi02.fbx` (color/normal/roughness の PBR テクスチャのみ。Emission テクスチャ無し) |
| 既存コード | `Assets/GlowingSushi/Scripts` は空。ゼロからの設計 |

### Immersal SDK の主要コンポーネント(参考)
- `ImmersalSDK` : SDK 初期化を管理するシングルトン
- `ImmersalSession` : 継続的なローカライズループを駆動
- `Localizer` : `OnFirstSuccessfulLocalization` / `OnSuccessfulLocalizations` / `OnLocalizationResult` / `OnFailedLocalizations` イベントを公開
- `SceneUpdater` + `XRSpace` : ローカライズ成功時に姿勢を反映する親トランスフォーム。この配下に置いたコンテンツは自動的に実世界座標へ追従する
- `XRMap` : `mapId` / `mapName` / `mapFile` を保持するマップ設定
- `MapManager` : マップの登録・管理

## 3. 機能要件

### 3.1 発光する寿司
- 寿司マテリアルに Emission を持たせ、URP の Bloom と組み合わせて発光表現を行う
- 発光強度は一定ではなく、時間経過や状態(通常/接近/逃走)に応じて変化させる(パルスなど)
- 発光色は所属する群れごとに異なる(HDRカラーパレットから割当)
- 泳いだ軌跡に発光粒子が残る(移動距離に応じて放出されるParticleSystem、群れの色にティント)

### 3.2 群泳(魚群のような動き) — AR水族館
- 複数の寿司が Boid アルゴリズム(分離・整列・結合)で群れとして自然に泳ぐ
- **軌道アトラクタ**: 各群れはアンカーを中心に円軌道でゆっくり周回する移動目標を追いかける。整列と組み合わさり、群れ全体が輪を描いて流れるように泳ぐ(水族館らしい遊泳の核)。位相・回転方向・上下の揺れは群れごとに変える
- 群れのメンバーは隊列を保つため**カメラへの接近はしない**(接近は3.3の専用個体の役割)
- ふらつき(wander)成分は弱めに加え、ジッターを抑える
- 魚らしさのため、最低速度(止まらない)・垂直速度減衰(主に水平に泳ぐ)・旋回時のバンク(内側へ傾く)を適用する
- 群れは複数配置する(平面検出位置を基準に円周上へ水平・高さオフセットで散らし、AR水族館のような空間にする)。群れ同士が混ざらないよう「引き戻し半径 < 群れ間隔」を保つ

### 3.3 近接・逃走インタラクション
- **接近専用個体(お客好き寿司)**: 群れとは別の少数の個体(金色発光)が水族館中央に漂い、周期的にプレイヤー(カメラ)へ近づいてくる。時間経過または十分近づいたら中央へ戻る
- タッチ操作で寿司に触れると、その個体(および周辺個体)が逃走状態へ遷移し、離れるように泳ぐ(群れメンバー・接近専用個体とも有効)
- 一定時間後、通常状態へ復帰する
- タッチ命中時は命中位置で発光粒子のバーストエフェクトと効果音(水泡ポップ音)を再生する。エフェクトは命中した群れの色にティントする

### 3.4 VPS 配置(屋外)
- Immersal でのローカライズ成功後、`XRSpace` 配下に配置したアンカーポイントに寿司を出現させる
- **マップごとに独立した `XRSpace` を持つ**(1つのXRSpaceに複数マップを入れると座標系が混ざるため)。ローカライズはマップ単位で成功し、その時点でそのマップのアンカーにのみ配置する
- サーバーローカライズ(`ServerLocalization`)を使用(マップファイルの埋め込み不要、ネットワーク必須)
- Developer Tokenは `Assets/GlowingSushi/Resources/ImmersalToken.txt`(Git管理外)から `ImmersalTokenLoader` が実行時に読み込み、`ImmersalSDK` のAwake前に設定する
- 使用マップ: bench(148713: 昼寝+散歩) / table(148714: ベイブレード) / vendingmachine(148694: 転がり)
- アンカー(`VpsAnchorView`)の位置合わせは、XRMapインスペクタのDownloadで点群(Visualization)を取得し、点群を目印にエディタで手動調整する(Y軸=面の法線)
- 配置タイミングの注意: Immersalの成功イベントは `SceneUpdater` がXRSpaceを動かす**前**に発火するため、イベント時点でアンカー姿勢を読むとずれる。配置はイベントの次フレームで行い、以降も成功のたびにスポット姿勢をアンカーへ追従更新する(ローカライズ精度の向上に追従)
- 追従は即時反映せず**指数補間で滑らかに**行う(サーバーローカライズは毎回数cm〜数十cmの誤差があり、即時反映だと約2秒ごとに瞬間移動して見えるため)
- 追従は**配置後 `vpsSettleDuration`(初期値10秒)で停止し、以降は位置を固定**する(バーストモードの高精度化はこの時間内に完了し、それ以降の微小補正はむしろ揺れの原因になるため)

### 3.5 フォールバック
- VPS マップが無い、またはローカライズ失敗時は AR Foundation の平面検出のみでローカルに寿司群を出現させ、発光・群泳・近接逃走インタラクションは同様に動作させる

### 3.6 サウンド
- タッチ命中時の効果音(水泡ポップ音、3D音源で命中位置から再生)
- ベイブレード衝突時の効果音(金属的な「キン」音、強度に応じた音量、3D音源)
- BGM: 環境音パッド風のループ(Am-F-C-G、約24秒、合成生成)を2Dで常時再生。音源は `Assets/GlowingSushi/Audio/AquariumBgm.wav` を差し替えるだけで変更可能
- BGMは状態を持たない単純再生のためViewModelを介さない(シーン直置きの `BgmPlayer` AudioSource)

### 3.7 場所固有の表面ふるまい(Phase 2)
実在の物体(自販機・ベンチ・テーブル)の表面に紐づいた寿司のふるまい。**寿司本体は発光しない**(Emission消灯・軌跡パーティクル無効)。

| 種別 | ふるまい | 想定場所(Map ID) |
|---|---|---|
| Rolling | 固定の向き(長軸)を保ったまま、その場で左右へ正弦波往復し、移動量に同期して長軸まわりにロールする(子供がおもちゃを転がすようなコロコロ) | 自販機の天面(148694) |
| Napping | 横倒しで寝て、呼吸のようにゆっくり上下する | ベンチ(148713) |
| Strolling | 進行方向を揺らしながらゆっくり歩き回る(よちよち揺れ付き) | ベンチ(148713) |
| Battle | ベイブレードのように高速スピンしながら動き回り、ぶつかると弾性衝突で弾かれ、**火花(HDR発光)**と衝突音が出る | テーブル(148714) |

- 火花・衝突音は寿司本体と違い演出として発光してよい(GlowParticle.matを流用)
- `PlacementMode`(Aquarium / SurfaceSpotsDemo / Both / None)で平面検出時に出すコンテンツを切り替えられる(2aの検証は平面上のデモスポット4種で行う)
- Phase 2bでは、Immersalローカライズ成功後にVPSアンカーの位置・種別からスポットを構築する(`SurfaceSpotsViewModel.AddSpot`)

### 3.8 タイトル画面
- タイトル文字の下に**小さなマグロがふわふわ浮遊**し、その下で「**タップスタート**」が点滅する
- 画面タップで: タイトルとタップスタートが消える → **マグロが上に跳ねながら回転**してスケール縮小で消える → 画面全体(CanvasGroup)がフェードアウトしてARへ遷移
- モード選択は置かない(水族館・VPS表面ふるまいの両方が有効)
- スキャン誘導などの常設UIは置かない(寿司鑑賞の邪魔になるため)
- コンテンツ出現系(平面検出・VPSローカライズ購読)の初期化は**タップスタート後**にゲートされる。タイトルの無いシーンでは即時開始

## 4. アーキテクチャ設計(MVVM + VContainer + R3)

### 4.1 レイヤー構成

```
View  →  ViewModel  →  Service  →  Domain
(MonoBehaviour) (Plain C#)  (DI登録)   (純粋C#)
```

ViewModel は View を一切参照しない。View が ViewModel を注入されて購読する一方向の依存のみ。

### 4.2 フォルダ構成・名前空間規約

すべてのスクリプトは `Assets/GlowingSushi/Scripts` 以下に配置する(プロジェクト内の他の場所には置かない)。フォルダ構成と名前空間は層構造にそのまま対応させる。

```
Assets/GlowingSushi/Scripts/
├── Domain/      → namespace GlowingSushi.Domain
├── ViewModel/    → namespace GlowingSushi.ViewModel
├── Service/    → namespace GlowingSushi.Service
├── View/      → namespace GlowingSushi.View
├── Root/      → namespace GlowingSushi.Root   (コンポジションルート)
└── Editor/     → namespace GlowingSushi.Editor  (エディタ専用ツール)
```

- ルート名前空間は `GlowingSushi`、各層フォルダ名をそのままサブ名前空間として付与する(例: `GlowingSushi.Domain.BoidMath`)
- 層をまたぐ共通の型(enumや設定ScriptableObjectなど)は、それが属する層のフォルダ・名前空間に置く(例: `SushiState` は `GlowingSushi.Domain`)
- `Root/` はDIの構成(コンポジションルート)専用の層で、VContainer型と全レイヤーを参照してよい唯一の場所(詳細は4.7)
- `Editor/` はエディタ専用ツール(アセット/シーン生成メニュー等)。ランタイムの依存関係チェーンの外にあり、全レイヤーを参照してよい

### 4.3 Domain 層(純粋 C#、MonoBehaviour 非依存) — `namespace GlowingSushi.Domain`
- `SushiState` : `Schooling` / `Approaching` / `Fleeing` の状態 enum
- `BoidMath` : 分離・整列・結合・追跡・逃走・ふらつき・レイ球交差判定を計算する静的関数群(タッチのヒット判定もコライダーを使わずここで行う)
- `SushiBehaviorSettings` : 群泳半径・速度・逃走距離などを持つ ScriptableObject
- `SushiSpawnData` : 寿司1匹分の初期配置データ(位置・初速・発光位相・ふらつきシード)。Service層がViewModelを直接生成するとService→ViewModelの逆方向依存になるため、Serviceはこのデータを返しViewModel層が実体化する

### 4.4 ViewModel 層(プレーン C# クラス、View を参照しない) — `namespace GlowingSushi.ViewModel`
- `AquariumViewModel` : 水族館全体を管理。複数の `SushiSchoolViewModel` を生成(円周配置+高さ差+色パレット割当+軌道位相/方向の割当)し `ObservableList` で公開、全群れのTick駆動、タッチの全群れ横断ヒット判定、タッチ成功イベント `Observable<TouchHitInfo>` の発行を行う。接近専用個体グループ(金色・軌道なし・canApproach=true)もここで追加生成する
- `SushiViewModel` : `ReactiveProperty<Vector3> Position` / `ReactiveProperty<Quaternion> Rotation` / `ReactiveProperty<SushiState> State` / `ReactiveProperty<float> GlowIntensity` と群れ色 `Color GlowColor` を公開。`Interact(Vector3 touchWorldPos)` などのコマンドメソッドを持つ。`canApproach` がtrueの個体のみ接近状態へ遷移する
- `SushiSchoolViewModel` : 1つの群れ。`SchoolConfig`(アンカー・色・個体数・軌道有無/位相/方向・接近可否)を受けて生成され、`ObservableCollections.R3` で個体群を管理して毎フレーム Boid 計算+軌道アトラクタ追従+バンク回転を駆動する。DI直登録はせず `AquariumViewModel` が生成する。タッチ用に `FindHit(Ray)` / `FleeFrom(個体, 位置)` を公開
- `SchoolConfig` : 群れ1つ分の生成設定(readonly struct)
- `TouchHitInfo` : タッチ成功イベントデータ(命中位置+群れの発光色)
- `SurfaceSpotsViewModel` : 表面ふるまいスポット群の管理。デモ配置(2a)/`AddSpot`(2b)、全スポットのTick駆動、Battle衝突イベント `Observable<BattleClashInfo>` の集約
- `SurfaceSpotViewModel` : スポット1つ分。表面ローカル2D座標系で転がり/昼寝/散歩/ベイブレードの各運動を駆動し、ワールド座標へ変換して個体のReactivePropertyに反映する
- `BattleClashInfo` : ベイブレード衝突イベントデータ(位置+強度)
- `VpsPlacementViewModel` : `VpsAnchorView` から登録されたアンカーを保持し、対応マップのローカライズ成功の翌フレームにアンカー姿勢へ表面ふるまいスポットを配置。整定時間内は追従更新する
- `ArPlacementViewModel` : AR平面検出状態を公開し、`PlacementMode` に応じて水族館/デモスポットの出現をトリガーする
- `StatusViewModel` : 平面検出・VPSローカライズ統計・スポット配置状況を集約して状態HUDへ公開する
- `TitleViewModel` : タイトル画面の開始状態(`IsStarted`)を公開。エントリポイントがこれを購読してコンテンツ出現をゲートする

### 4.5 Service 層(VContainer で DI 登録、ViewModel から注入) — `namespace GlowingSushi.Service`
- `SushiSpawnService` : 群れの初期配置データ(`SushiSpawnData` 群)の生成(ViewModelの実体化はViewModel層の責務)
- `TouchInputService` : Input System(EnhancedTouch)のタッチ入力を `Observable<Ray>` として公開(エディタではTouchSimulationでマウスをタッチ扱い)
- `ArPlaneDetectionService` : `ARPlaneManager.trackablesChanged` を Observable にラップ(Phase 1では初回検出平面のみ通知)
- `ICameraPoseService` / `CameraPoseService` : ARカメラ(プレイヤー視点)の位置・回転を公開。接近行動でViewModelが参照する
- `VpsLocalizationService` : Immersal `Localizer` の UnityEvent(成功マップID配列・初回成功)を R3 の Observable / ReactiveProperty に変換

### 4.6 View 層(MonoBehaviour、`[Inject]` で ViewModel を受け取り購読のみ行う) — `namespace GlowingSushi.View`
- `SushiView` : `Position` / `Rotation` を購読して Transform を更新、`GlowIntensity` を購読して `MaterialPropertyBlock` 経由で Emission を更新(色はViewModelの `GlowColor` × 強度)。軌跡ParticleSystemを群れ色にティント。`State` に応じたアニメーション再生は未実装
- `AquariumView` : `AquariumViewModel` の群れ一覧と各群れの個体一覧の増減をネスト購読して `SushiView` の生成・破棄を行う。寿司の種類(見た目)はプレハブ配列からランダムに選ぶ(見た目の多様性はView層の関心事とする)
- `TouchEffectView` : `TouchHit` を購読し、命中位置でバーストパーティクル(群れ色ティント)と効果音を再生する
- `SurfaceSpotsView` : `SurfaceSpotsViewModel` のスポット・個体の増減をネスト購読して `SushiView` を生成・破棄する
- `BattleEffectView` : `BattleClash` を購読し、衝突位置で火花パーティクル(線状スパーク、HDR発光)と衝突音を強度連動で再生する
- `SushiView` は `IsGlowing=false` の個体に対してEmission消灯+軌跡パーティクル無効化を行う
- `TitleView` : タイトル画面。マグロの浮遊・タップスタート点滅・タップ後のジャンプ回転演出とフェードアウトを行い、`TitleViewModel.Start()` を呼ぶ
- `VpsAnchorView` : マップごとの `XRSpace` 配下に置く配置ポイント。マップIDとふるまい種別を持ち、起動時に `VpsPlacementViewModel` へ自己登録する。エディタ配置用のギズモ表示付き

### 4.7 Root 層(コンポジションルート) — `namespace GlowingSushi.Root`
- `GlowingSushiLifetimeScope` : VContainerの `LifetimeScope`。全レイヤーの依存関係をここで一括登録する。シーン上の参照(挙動設定アセット・`ARPlaneManager`・ARカメラ・Immersal `Localizer`)をSerializeFieldで受けてDIに渡す。`Localizer` が設定されたシーンでのみVPS機能(Service/ViewModel)を登録し、`VpsAnchorView` への注入は `autoInjectGameObjects` にXRSpaceを登録して行う
- `GlowingSushiEntryPoint` : `IStartable`(起動時に各サービス・ViewModelのInitializeを呼ぶ。VPS系は任意解決で初期化)/ `ITickable`(毎フレーム水族館と表面スポットのTickを駆動)
- `ImmersalTokenLoader` : Git管理外のResourcesからDeveloper Tokenを読み込み、`ImmersalSDK` のAwake前(DefaultExecutionOrder -5000)に設定するブートストラップ
- ViewModel・ServiceはVContainer型に依存しない。VContainerと全レイヤーを参照してよいのはこのRoot層のみ

### 4.8 Editor 層(エディタ専用ツール) — `namespace GlowingSushi.Editor`
- `GlowingSushiSetupMenu` : `GlowingSushi/Setup` メニュー。①マテリアル/プレハブ/設定/効果音/BGM生成、②シーンセットアップ(ARリグ・DIスコープ・各View構築)、③プロジェクト設定(`ARBackgroundRendererFeature` 追加・iOSカメラ権限)、④挙動パラメータの推奨値一括適用、⑤VPSセットアップ(ImmersalSDKプレハブ・マップごとのXRSpace+XRMap+アンカー構築)。シーンやプレハブのYAML手書きを避け、参照結線を確実にするためエディタスクリプトで生成する
- 寿司モデル `sushi02.fbx` は7貫の盛り合わせ(gari/Salmon_/ebi1/ebi2/negitoro/engawa/maguro)のため、①でガリを除く6貫を1貫ずつ個別プレハブ(`Sushi_maguro.prefab` 等)に分解する。各プレハブはメッシュ中心を原点へ合わせ、最長辺を約0.15mに正規化する

## 5. VPS 運用メモ

- Developer Token・Map ID(bench 148713 / table 148714 / vendingmachine 148694(bench/tableは2026-07-06撮り直し))は取得済み。トークンは `Assets/GlowingSushi/Resources/ImmersalToken.txt`(Git管理外)に保管
- VPSはXR Simulationでは検証できないため、現地での実機確認が必須
- `PlacementMode` でフォールバックを制御: マップが読めない環境では平面検出ベースの水族館/デモスポットで動作できる

## 6. フェーズ分け

- **Phase 1(完了)** : ローカル AR 水族館(発光 + 群泳 + 接近/逃走 + タッチ演出 + BGM)
- **Phase 2a(完了)** : 表面ふるまい(転がり/昼寝/散歩/ベイブレード)のローカル実装。平面検出したデモスポットで検証
- **Phase 2b(完了)** : Immersal VPSで実在の場所へ配置。自販機(148694)でエンドツーエンド動作確認済み(ローカライズ→天面で転がり→追従→固定)

## 7. 今後の課題・確認事項

- **ベンチ/テーブルの現地検証**: 新マップ(148713/148714)での再検証待ち。点群品質が不足なら再度撮り直し(ユーザー対応)
- **マップの誤ローカライズ**: 特徴が似ているため別の場所のマップにローカライズすることがある。対策候補: 端末GPSとマップのWGS84座標(メタデータに含まれる)を比較し、近距離のマップのみをローカライズ対象に絞る
- iOS実機でのパフォーマンス(粒子数)確認
- 状態HUDのデバッグ表示を本番用UI(タイトル画面+スキャン誘導表示)へ置き換える

### 7.1 Phase 1 で採用した挙動パラメータ(暫定)

`Assets/GlowingSushi/Settings/SushiBehaviorSettings.asset` でインスペクタから調整可能。初期値:

| 分類 | パラメータ | 初期値 |
|---|---|---|
| 水族館 | 群れ数 / 色パレット / 水平間隔 / 高さ差 | 3群 / シアン・オレンジ・マゼンタ / 1.6m / 0.4m |
| 群れ | 個体数 / 引き戻し半径 / 平面上の出現高さ | 14匹 / 0.9m / 0.6m |
| Boid | 近傍半径 / 分離半径 | 0.8m / 0.25m |
| Boid重み | 分離 / 整列 / 結合 / ふらつき | 1.8 / 1.8 / 1.8 / 0.15 |
| 軌道 | 半径 / 周期 / Seek重み / 上下揺れ / バンク強度 | 0.5m / 18s / 1.5 / 0.1m / 30 |
| 速度 | 通常 / 最低 / 逃走 / 接近 / 操舵力上限 / 垂直減衰 | 0.45 / 0.2 / 1.5 / 0.8 m/s / 2.0 / 0.6 |
| 接近専用個体 | 数 / 色 / 判定間隔 / 確率 / 最大時間 / 停止距離 | 2匹 / 金色 / 4s / 0.5 / 6s / 0.5m |
| タッチ・逃走 | ヒット半径 / 伝播半径 / 逃走時間 | 0.15m / 0.4m / 3s |
| 表面ふるまい共通 | スポット半径 / 個体数 / 表面オフセット | 0.25m / 3匹 / 0.035m |
| 転がり | 往復の振れ幅 / 周期 / 接地半径 | 0.12m / 2.0s / 0.04m |
| 昼寝 | 呼吸周期 / 上下幅 | 3.5s / 0.004m |
| 散歩 | 速さ / 方向の変わりやすさ | 0.05m/s / 1.2rad/s |
| ベイブレード | スピン / 移動速さ / 衝突半径 / クールダウン | 720°/s / 0.25m/s / 0.08m / 0.3s |
| VPS配置 | 追従の整定時間(以降固定) | 10s |
| 発光 | パルス周期 / 最小 / 最大 / 逃走時係数 | 2s / 0.35 / 1.5 / 1.5 |
| 軌跡粒子 | 放出密度 / 寿命 / サイズ / 拡散半径 / 初速 / マテリアルHDR強度 | 150個/m / 1.5〜2.5s / 0.01〜0.025m / 0.04m / 0.02〜0.1m/s / ×2.5 |

- 粒子の「光り」はマテリアル(`GlowParticle.mat`)のBaseColorのHDR強度で決まる(startColorはLDRクランプされるため)。量はプレハブ内TrailParticlesの`Rate over Distance`

- 群れの結束の考え方: 「引き戻し半径 < 群れ間隔」を守ると群れ同士が混ざらない。結合重みを分離と同等以上にすると密集する
- 設定アセットは生成時の値を保持するため、スクリプト側の推奨値変更後はメニュー「4. 挙動パラメータを推奨値へ更新」で反映する
