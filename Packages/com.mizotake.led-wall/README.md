# LED Wall for URP

Unity 6.0 / URP 17用のLED表示と映像連動照明です。

Package Managerの「Install package from git URL…」に次のURLを入力します。

```text
https://github.com/MizoTake/UnityLEDKit.git?path=/Packages/com.mizotake.led-wall#main
```

- `Mizotake/LED Wall/RGB LED`：RGB素子、距離に応じたフィルタリング、HDR発光、PBR基板とレンズ。
- `Mizotake/LED Wall/Diffused LED`：平均発光量を揃えた拡散レンズ型のLED表示。
- 2種類のテクスチャ・動画を `_Transition` で線形フェード。
- `LedPanelLighting`：映像・Tint・UV・フェード・輝度・サイズからSpot Lightを自動設定して標準URP Litを照明。GPU面積平均、線形色の時間補間、カット検知、柔らかなCookie配光。個別調整は標準でOFF。
- `LedColorSpill` + `LedColorSpillRendererFeature`：Computeで集計した映像色を不透明な受信面へGPU内で加算。Light生成・CPU readback不要。LayerMask、距離減衰、面の向き、簡易画面内遮蔽を使用。サンプルの標準方式。
- `LedPanelLighting`の標準集計は2段階Compute reduction。比較用のBlitピラミッドも選択可能。
- `Mizotake/LED Wall/Reflective Floor`：必要な場合だけ使う平面反射床。
- `LED Gallery` サンプル：オリジナル動画、静止画、固定カメラ、URP設定、Inspectorから調整できるシーン。

Package ManagerのSamplesから `LED Gallery` をImportし、作成されたフォルダー内の `Scenes/LEDGallery.unity` を開いてPlayしてください。必要に応じて `Tools > LED Wall > Use Gallery URP Settings` でサンプル用のForward+ / HDR設定を適用します。
パネルのInspectorでTransitionやBrightnessを変更でき、サンプル側のスクリプトが値を固定しません。Source/Secondary Sourceには標準VideoPlayerを指定します。再生操作はVideoPlayer側で行います。
手順と制約は [Documentation~/index.md](Documentation~/index.md) にあります。
プロジェクト全体の導入・検証手順は [UnityLEDKit](https://github.com/MizoTake/UnityLEDKit) にあります。
コード・ドキュメント・サンプルの生成素材は [MITライセンス](LICENSE.md) で提供します。
