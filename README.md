# GlowingSushi 🍣✨

AR + VPS で「光る寿司」と暮らす iPhone アプリ。

- **AR水族館**: 光る寿司たちが魚群のように輪を描いて泳ぎ、軌跡に発光粒子を残す。金色の寿司は時々こちらに寄ってくる。タッチすると光の弾けるエフェクトと音とともに逃げていく
- **街の寿司(VPS)**: Immersal VPSで実在の場所を認識し、自販機の上でゴロゴロ転がる寿司、ベンチで昼寝・散歩する寿司、テーブルの上でベイブレードのように回転して火花を散らしてぶつかり合う寿司が出現する

## 技術スタック

| 項目 | 内容 |
|---|---|
| Unity | 6000.3.13f1 (URP) |
| AR | AR Foundation 6.3 (ARKit) / XR Simulation(エディタ検証) |
| VPS | Immersal SDK 2.3(サーバーローカライズ) |
| DI | VContainer |
| リアクティブ | R3 + ObservableCollections.R3 |

## アーキテクチャ

MVVMの一方向依存を厳守: `View → ViewModel → Service → Domain`(ViewModelはViewを知らない)。

```
Assets/GlowingSushi/Scripts/
├── Domain/     純粋C#(Boid計算・表面運動・設定)    → GlowingSushi.Domain
├── ViewModel/  プレーンC#(ReactivePropertyで公開)  → GlowingSushi.ViewModel
├── Service/    SDK/入力のラップ(DI登録)           → GlowingSushi.Service
├── View/       MonoBehaviour(購読して反映のみ)     → GlowingSushi.View
├── Root/       コンポジションルート(LifetimeScope)  → GlowingSushi.Root
└── Editor/     セットアップメニュー                → GlowingSushi.Editor
```

機能要件・クラス設計・調整パラメータの詳細は [docs/SPEC.md](docs/SPEC.md) を参照。

## セットアップ

1. リポジトリをクローンし、Unity 6000.3系で開く
2. **Immersal Developer Tokenを配置**(Git管理外):
   `Assets/GlowingSushi/Resources/ImmersalToken.txt` にトークン文字列のみを保存
   ([developers.immersal.com](https://developers.immersal.com) で取得)
3. メニュー **GlowingSushi → Setup** を番号順に実行:
   1. アセット生成(マテリアル・寿司プレハブ・効果音・BGM)
   2. シーンセットアップ(ARリグ・DI・各View)
   3. プロジェクト設定(ARレンダラー・iOSカメラ権限)
   4. 挙動パラメータを推奨値へ更新
   5. VPSセットアップ(ImmersalSDK・マップ・アンカー)
   6. タイトルシーンセットアップ
4. シーン上のアセット参照が未結線の場合はConsoleのログに従い手動でドラッグ

### VPSマップを自分の場所にする場合

1. iOSアプリ「Immersal Mapper」で対象の場所をスキャンし、Map IDを取得
2. `GlowingSushiSetupMenu.cs` の `VpsMaps` のマップID・アンカー定義を書き換えてメニュー5を再実行
3. 各XR Mapのインスペクタから点群(Visualization)をダウンロードし、点群を目印にアンカーを配置
   (ダウンロード時のみImmersalSDKのDeveloper Token欄へ一時的にトークンを貼り、**終わったら必ず空に戻す**)

## シーン構成

- `Assets/GlowingSushi/Scenes/Title.unity` — 起動シーン。タイトル+浮遊マグロ+タップスタート
- `Assets/GlowingSushi/Scenes/Main.unity` — ARコンテンツ本体

## 動作確認

- **エディタ**: XR SimulationでMain.unityからPlay(WASD+右ドラッグで移動)。VPSはエディタでは動作しない(ImmersalSDKは自動で無効化される)
- **実機(iOS)**: Title→Mainの通し確認とVPS検証は実機で行う。`GlowingSushiLifetimeScope` の **Placement Mode** で平面検出時のコンテンツ(水族館/デモ/なし)を切り替え可能
- 画面左上の状態HUDでSDK初期化・ローカライズ試行/成功・スポット配置数を確認できる
