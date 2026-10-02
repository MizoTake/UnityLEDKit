# UnityLEDSystem

Unity 6.0.79f1 / URP 17.0.4向けのLED表示と映像連動照明のプロジェクトです。
コアは `Packages/com.mizotake.led-wall`、動作するサンプルは `Assets/LEDGallery` にあります。

![LED Gallery](Assets/LEDGallery/Preview/Gallery.png)

## サンプルを見る

1. このリポジトリをcloneし、Unity Hubから開きます。
2. `Assets/LEDGallery/Scenes/LEDGallery.unity` を開いてPlayします。サンプル用URP設定は適用済みで、カメラは固定です。
3. `01 / RGB LED` または `02 / Diffused LED` のLedPanelをInspectorで選びます。`Transition` は0で動画A、1で静止画B、0.5で半々になります。Brightness、Resolution、Diffusionも再生中に調整できます。
4. `Shared Video Player` の標準VideoPlayerが動画を再生します。別の動画・URLへの差し替え、再生・停止・ループ・音声はVideoPlayer側で設定します。LedPanelのSourceとSecondary Sourceには別々のVideoPlayerも指定できます。

サンプルのControllerによるプロパティの上書きはありません。Play中の変更はUnity標準どおり終了時に戻るため、保存したい初期値はEditModeで変更してください。
床、球、キューブは通常の `Universal Render Pipeline/Lit` です。映像をGPUで集計した色からSpot Lightを生成し、映像の色で周囲を照らします。

表示用シェーダーはRGB素子型と拡散レンズ型です。HDR発光、Bloom、ACES、Forward+、SSAOを使用しています。任意で使える平面反射床とGPUぼかしもコアにあります。

## ライトの自動設定

LedPanelと同じGameObjectの `LedPanelLighting` は映像、フェード、輝度、パネルのワールドサイズ・向きを参照します。
`Override Light Parameters` は標準でOFFです。輝度やTransformを変更すると生成ライトも追従します。光量・照射距離・角度を個別に調整したい場合だけONにしてください。
Culling Mask、Rendering Layers、影の設定は別に指定できます。生成ライトの数を変更しても再適用します。生成された子Lightの値は自動更新されるため、設定はLedPanelLightingのInspectorで行います。
URPの受光対象の限定にはRendering Layersを使用してください。サンプルではURP AssetのRendering Layersを有効にしています。

## UPM導入

Unity 6.0 / URP 17のプロジェクトで、Package Managerの「Install package from git URL…」へ次のURLを入力します。GitコマンドがPATHに必要です。

```text
https://github.com/MizoTake/UnityLEDSystem.git?path=/Packages/com.mizotake.led-wall#v0.1.0
```

`Packages/manifest.json`へ追加する場合の依存エントリーです。

```json
"com.mizotake.led-wall": "https://github.com/MizoTake/UnityLEDSystem.git?path=/Packages/com.mizotake.led-wall#v0.1.0"
```

Package Managerの `LED Wall for URP` > Samplesから `LED Gallery` をImportします。作成されたフォルダー内の `Scenes/LEDGallery.unity` を開いてPlayしてください。
別プロジェクトで必要な場合は `Tools > LED Wall > Use Gallery URP Settings` を選択します。このメニューはGraphics Settingsと現在のQualityのURP Assetを変更するので、使用中の設定を控えてから適用してください。
このリポジトリをプロジェクトとして開く場合はサンプルが既にあるため、重複Importは不要です。

ローカル導入では「Install package from disk…」でコアの `package.json` を指定できます。
`Tools/Export-Upm.ps1` はサンプルを `Samples~/LEDGallery` へ同期し、`Builds/UPM` にUPM用tgzを書き出します。「Install package from tarball…」での導入にも対応します。

## 権利と動画の生成元

`NeonOrbits.mp4` は12秒、1280×720、30 fps、音声なしの数学的なアニメーションです。`Prism.png` とポスターもこのプロジェクトで生成しています。サンプルメディアには外部の映像、音楽、画像、ブランド素材を使用していません。
生成コードは `Tools/generate_sample_video.py`、由来とハッシュは `Assets/LEDGallery/Media/PROVENANCE.json` に記録しています。
GPUのH.264 NVENC、Baseline、Bフレームなし、BT.709の色メタデータで出力しています。

```powershell
python Tools/generate_sample_video.py
python Tools/generate_sample_video.py --still-only
```

生成には既存環境のNumPyとFFmpegを使用します。NVENCがない生成環境では `--encoder libx264` を指定できます。
独自コード・生成メディアの再利用ライセンスは未指定です。公開・UPM導入可能であることからMITやCC0などの許諾を推定しないでください。権利の扱いは [パッケージのLICENSE.md](Packages/com.mizotake.led-wall/LICENSE.md) に記載しています。Unity標準パッケージ・テンプレートには各提供元の条件が適用されます。

## 検証

開いたEditorにUnityCLIのPipelineサーバーが接続できる環境で実行します。

```powershell
unity status --json
./Tools/Verify-Unity.ps1
./Tools/Export-Upm.ps1
./Tools/Verify-Distribution.ps1
./Tools/Verify-Consumer.ps1
./Tools/Verify-Consumer.ps1 -PackageUrl 'https://github.com/MizoTake/UnityLEDSystem.git?path=/Packages/com.mizotake.led-wall#v0.1.0'
```

テストは反射の数式、シェーダーコンパイル、RGB素子のGPU出力、A/Bフェード、GPU色集計、通常のURP Litの受光とRendering Layers、ライトの自動追従・個別調整・再生成、標準VideoPlayerの再生・一時停止・停止を対象にしています。
実行ログは `Logs/LEDWall` に保存します。Library、Temp、ビルド出力はGitへ含めません。
導入、API、性能、制約は [パッケージの使用手順](Packages/com.mizotake.led-wall/Documentation~/index.md)、実施済みの検証と未検証範囲は [VALIDATION.md](VALIDATION.md) を参照してください。
