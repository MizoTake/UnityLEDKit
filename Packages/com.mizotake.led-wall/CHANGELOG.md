# Changelog

## 0.1.0 - 2026-10-02

- RGB素子型と拡散型のHDR LED表示を追加。
- 2テクスチャ/動画の線形フェードと標準VideoPlayerからの直接入力を追加。
- GPUで集計した色とLedPanelの輝度・サイズを通常のURP Spot Lightへ渡す自動照明を追加。個別調整は標準でOFF。
- 自動生成ライトのCullingMask、Rendering Layers、影の設定を追加。再生成時も設定を維持。
- 任意で使えるURP平面反射床とGPUぼかしを追加。
- 外部素材を使わず生成した動画・静止画、標準Lit受光体、固定カメラを含むGalleryサンプルを追加。Inspectorの操作を上書きするControllerは使用しない。
- EditMode/PlayModeテストとUnityCLI検証・UPM書き出し手順を追加。
- UnityLEDSystemリポジトリからのGit / タグ指定UPM導入手順とパッケージの案内URLを追加。
