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

### 3.2 群泳(魚群のような動き)
- 複数の寿司が Boid アルゴリズム(分離・整列・結合)で群れとして自然に泳ぐ
- ふらつき(wander)成分を加え、単調な周回にならないようにする

### 3.3 近接・逃走インタラクション
- 一部の寿司がプレイヤー(カメラ)に近づいてくる「接近」状態を持つ
- タッチ操作で寿司に触れると、その個体(および必要に応じて周辺個体)が逃走ベクトルへ状態遷移し、群れから離れるように泳ぐ
- 一定時間後、通常の群泳状態へ復帰する

### 3.4 VPS 配置(屋外)
- Immersal でのローカライズ成功後、`XRSpace` 配下に配置したアンカーポイントに寿司群を出現させる
- アンカーは地上付近(街中を泳ぐ)と空中(浮遊して泳ぐ)の両方を想定する
- アンカーの実体は「配置ポイント + そこに紐づく寿司群パラメータ」

### 3.5 フォールバック
- VPS マップが無い、またはローカライズ失敗時は AR Foundation の平面検出のみでローカルに寿司群を出現させ、発光・群泳・近接逃走インタラクションは同様に動作させる

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
- `SushiViewModel` : `ReactiveProperty<Vector3> Position` / `ReactiveProperty<Quaternion> Rotation` / `ReactiveProperty<SushiState> State` / `ReactiveProperty<float> GlowIntensity` を公開。`Interact(Vector3 touchWorldPos)` などのコマンドメソッドを持つ
- `SushiSchoolViewModel` : `ObservableCollections.R3` で個体群(`SushiViewModel` のコレクション)を管理し、毎フレーム Boid 計算を駆動する
- `ArPlacementViewModel` : AR平面検出状態を公開し、検出時に群れの出現をトリガーする
- `VpsPlacementViewModel` : Immersal ローカライズ状態を公開し、成功時に対応するアンカーの群れ出現をトリガーする

### 4.5 Service 層(VContainer で DI 登録、ViewModel から注入) — `namespace GlowingSushi.Service`
- `SushiSpawnService` : 群れの初期配置データ(`SushiSpawnData` 群)の生成(ViewModelの実体化はViewModel層の責務)
- `TouchInputService` : Input System(EnhancedTouch)のタッチ入力を `Observable<Ray>` として公開(エディタではTouchSimulationでマウスをタッチ扱い)
- `ArPlaneDetectionService` : `ARPlaneManager.trackablesChanged` を Observable にラップ(Phase 1では初回検出平面のみ通知)
- `ICameraPoseService` / `CameraPoseService` : ARカメラ(プレイヤー視点)の位置・回転を公開。接近行動でViewModelが参照する
- `VpsLocalizationService` : Immersal `Localizer` の UnityEvent を R3 の Observable に変換(Phase 2)

### 4.6 View 層(MonoBehaviour、`[Inject]` で ViewModel を受け取り購読のみ行う) — `namespace GlowingSushi.View`
- `SushiView` : `Position` / `Rotation` を購読して Transform を更新、`GlowIntensity` を購読して `MaterialPropertyBlock` 経由で Emission を更新、`State` に応じたアニメーション再生(アニメーションはPhase 1では未実装)
- `SushiSchoolView` : `SushiSchoolViewModel` のコレクション変更に追従して `SushiView` の生成・破棄を行う
- `VpsAnchorView` : `XRSpace` 配下に配置する、街中/空中それぞれのアンカーの見た目上の置き場所(Phase 2)

### 4.7 Root 層(コンポジションルート) — `namespace GlowingSushi.Root`
- `GlowingSushiLifetimeScope` : VContainerの `LifetimeScope`。全レイヤーの依存関係をここで一括登録する。シーン上の参照(挙動設定アセット・`ARPlaneManager`・ARカメラ)をSerializeFieldで受けてDIに渡す
- `GlowingSushiEntryPoint` : `IStartable`(起動時に各サービス・ViewModelのInitializeを呼ぶ)/ `ITickable`(毎フレーム `SushiSchoolViewModel.Tick(Time.deltaTime)` を駆動)
- ViewModel・ServiceはVContainer型に依存しない。VContainerと全レイヤーを参照してよいのはこのRoot層のみ

### 4.8 Editor 層(エディタ専用ツール) — `namespace GlowingSushi.Editor`
- `GlowingSushiSetupMenu` : `GlowingSushi/Setup` メニュー。①マテリアル/プレハブ/設定アセット生成、②シーンセットアップ(ARリグ・DIスコープ構築)、③プロジェクト設定(`ARBackgroundRendererFeature` 追加・iOSカメラ権限)。シーンやプレハブのYAML手書きを避け、参照結線を確実にするためエディタスクリプトで生成する

## 5. VPS 運用における未確定事項

- Immersal の App ID・スキャン済み Map ID は**現時点で未取得**。そのため実機での屋外ローカライズ検証は今回のフェーズでは行えない
- `VpsLocalizationService` はマップ未接続でもアプリがビルド・動作できるよう、フォールバック(平面検出のみでの動作)を仕様として明記する

## 6. フェーズ分け

- **Phase 1** : ローカル AR 完結(発光 + 群泳 + 接近/逃走インタラクション)。エディタ・実機で検証可能
- **Phase 2** : Immersal VPS による街中/空中への固定配置。Map ID 取得後に接続・検証

## 7. 今後の確認事項

- VPS アンカー(地上/空中)の配置数・配置方法(エディタでの手動配置か、座標オフセット設定による自動配置か)
- Immersal アカウント・App ID 取得のスケジュール

### 7.1 Phase 1 で採用した挙動パラメータ(暫定)

`Assets/GlowingSushi/Settings/SushiBehaviorSettings.asset` でインスペクタから調整可能。初期値:

| 分類 | パラメータ | 初期値 |
|---|---|---|
| 群れ | 個体数 / 引き戻し半径 / 平面上の出現高さ | 10匹 / 1.5m / 0.6m |
| Boid | 近傍半径 / 分離半径 | 1.0m / 0.35m |
| Boid重み | 分離 / 整列 / 結合 / ふらつき | 1.5 / 1.0 / 1.0 / 0.6 |
| 速度 | 通常 / 逃走 / 接近 / 操舵力上限 | 0.6 / 1.5 / 0.8 m/s / 2.0 |
| 接近 | 判定間隔 / 確率 / 最大時間 / 停止距離 | 5s / 0.3 / 6s / 0.5m |
| タッチ・逃走 | ヒット半径 / 伝播半径 / 逃走時間 | 0.15m / 0.4m / 3s |
| 発光 | パルス周期 / 最小 / 最大 / 逃走時係数 | 2s / 0.5 / 2.5 / 1.5 |
