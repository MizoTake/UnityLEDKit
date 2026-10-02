# LED Gallery

`Scenes/LEDGallery.unity` を開いてPlayしてください。カメラは固定です。
標準URP Litの床、球、キューブに、映像の色から生成したSpot Lightが当たります。
パネルのLedPanelをInspectorで選び、Transition（0=動画A、1=Prism B）、Brightness、Resolutionなどを変更してください。サンプルControllerはなく、Inspectorの値を上書きしません。
再生は `Shared Video Player` の標準VideoPlayerへ任せています。Source/Secondary Sourceへ別々のVideoPlayerも指定できます。
LedPanelLightingの `Override Light Parameters` は標準でOFFです。色・フェード・輝度・サイズはLedPanelへ追従し、個別の光量・距離・角度調整が必要な場合だけONにします。照射対象と影の設定は別に変更できます。
Play中の変更は終了時に戻ります。初期値の保存はEditModeで行ってください。
動画と静止画は数学的に生成した素材です。外部の映像・音楽・画像を使っていません。

別のURPプロジェクトでは必要に応じて `Tools > LED Wall > Use Gallery URP Settings` でサンプルのForward+ / HDR / Rendering Layers設定へ切り替えられます。このメニューはGraphics Settingsと現在のQualityのURP Assetを変更します。
コアは `com.mizotake.led-wall` にあり、サンプルAssetsを参照しません。
