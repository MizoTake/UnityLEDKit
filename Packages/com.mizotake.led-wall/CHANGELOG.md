# Changelog

## Unreleased

- 独自コード・ドキュメント・サンプルの生成素材をMITライセンスとして公開。
- 2段階Compute reductionを追加。Light方式の標準集計をComputeへ変更し、比較用のBlitバックエンドを維持。
- LedColorSpillとRender Graph対応LedColorSpillRendererFeatureを追加。Light生成・CPU readbackなしで、選択した不透明受信面へ映像色を加算。
- 発光面・距離・面積・法線による拡散寄与、LayerMask、共有受信マスク、任意の画面内深度遮蔽、GPU内の色補間・履歴リセットを追加。
- Galleryの標準をColor Spillへ変更。Inspectorと単発メニューでLight方式へ切り替え可能。
- Compute/BlitのHDR・フェード・UV集計比較、LightなしのLit受信、マスク・発光面の向き・距離制限・カット・解放のGPUテストを追加。
- LEDの黒い基板とレンズにURP PBR照明、曲面法線、粗さを追加。深度法線にも粒の形状を反映。
- RGB・拡散型の粒の面積を正規化し、Fill・Diffusion・距離による平均発光量の差を抑制。
- 9点サンプリングをGPUの面積平均ピラミッドへ変更。細かな明部を照明へ反映。
- LedPanelのTint、MaterialのUV変換、Content Rectを表示と照明で共有。
- 線形色空間での時間補間、映像の色カット・入力変更・シーク時の履歴リセットを追加。
- 自動生成ライトに柔らかなCookie配光とサンプリング診断表示を追加。
- 床・布・金属のGPU生成サーフェスマップ、面取り筐体、標準Litの側壁をサンプルへ追加。Bloomを粒が残る設定へ調整。
- 近距離／遠距離の平均輝度、PBR受光、Tint・クロップ、Cookie、時間応答のテストを追加。

## 0.1.0 - 2026-10-02

- RGB素子型と拡散型のHDR LED表示を追加。
- 2テクスチャ/動画の線形フェードと標準VideoPlayerからの直接入力を追加。
- GPUで集計した色とLedPanelの輝度・サイズを通常のURP Spot Lightへ渡す自動照明を追加。個別調整は標準でOFF。
- 自動生成ライトのCullingMask、Rendering Layers、影の設定を追加。再生成時も設定を維持。
- 任意で使えるURP平面反射床とGPUぼかしを追加。
- 外部素材を使わず生成した動画・静止画、標準Lit受光体、固定カメラを含むGalleryサンプルを追加。Inspectorの操作を上書きするControllerは使用しない。
- EditMode/PlayModeテストとUnityCLI検証・UPM書き出し手順を追加。
- UnityLEDSystemリポジトリからのGit / タグ指定UPM導入手順とパッケージの案内URLを追加。
