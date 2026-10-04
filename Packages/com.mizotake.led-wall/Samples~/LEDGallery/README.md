# LED Gallery

`Scenes/LEDGallery.unity` を開いてPlayしてください。カメラは固定です。
標準URP Litの床、金属の球、面取りキューブ、側壁、布のサイドパネルに、映像の色から生成したSpot Lightが当たります。
パネルのLedPanelをInspectorで選び、Transition（0=動画A、1=Prism B）、Brightness、Resolutionなどを変更してください。サンプルControllerはなく、Inspectorの値を上書きしません。
再生は `Shared Video Player` の標準VideoPlayerへ任せています。Source/Secondary Sourceへ別々のVideoPlayerも指定できます。
LedPanelLightingの `Override Light Parameters` は標準でOFFです。色・フェード・輝度・サイズはLedPanelへ追従し、個別の光量・距離・角度調整が必要な場合だけONにします。照射対象と影の設定は別に変更できます。
Play中の変更は終了時に戻ります。初期値の保存はEditModeで行ってください。
動画と静止画は数学的に生成した素材です。外部の映像・音楽・画像を使っていません。
床・布・金属のAlbedo/Normal/Metallic SmoothnessマップはGPUで生成します。由来はSurfaces/PROVENANCE.jsonにあります。Tools > LED Wall > Create or Rebuild Galleryで再生成できます。
パネルのMaterialでPhysical Surface Lighting、Lens Curvature、Lens Smoothness、Housing Smoothnessを調整できます。照明のSoft Distributionは標準ON、Response Secondsは標準0.08秒です。TintはLedPanel側で変更し、表示と照明に共通で適用します。

別のURPプロジェクトでは必要に応じて `Tools > LED Wall > Use Gallery URP Settings` でサンプルのForward+ / HDR / Rendering Layers設定へ切り替えられます。このメニューはGraphics Settingsと現在のQualityのURP Assetを変更します。
コアは `com.mizotake.led-wall` にあり、サンプルAssetsを参照しません。

サンプルコードと生成素材は [MITライセンス](https://github.com/MizoTake/UnityLEDKit/blob/main/Packages/com.mizotake.led-wall/LICENSE.md) で提供します。
