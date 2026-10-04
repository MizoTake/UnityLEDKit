# LED Wall for URP の使い方

## UPMからの導入

Package Managerの「Install package from git URL…」へ `https://github.com/MizoTake/UnityLEDKit.git?path=/Packages/com.mizotake.led-wall#main` を入力します。GitコマンドがPATHに必要です。
`LED Wall for URP` のSamplesから `LED Gallery` をImportすると、シーン、動画、マテリアル、設定、固定カメラがAssetsにコピーされます。作成されたフォルダー内の `Scenes/LEDGallery.unity` を開きます。
必要な場合は `Tools > LED Wall > Use Gallery URP Settings` でサンプルのForward+ / HDR設定を適用し、Playします。このメニューはGraphics Settingsと現在のQuality設定のURP Assetを変更するので、使用中の設定を控えてから適用してください。
UPMで導入するコアは `com.mizotake.led-wall` です。Gitリポジトリ全体をUnityプロジェクトとして開く場合は、コアとサンプルが既に入っています。

## 動作条件と構成

対象はUnity 6.0 / URP 17です。既存URP、UnityのVideoPlayer、Compute Shaderを使用します。Lightを使用する方式と、Render GraphのRenderer Feature方式を選べます。コアが新しい外部ライブラリを要求することはありません。
`Runtime` はサンプルAssetsを参照しません。`Samples~/LEDGallery` はPackage ManagerからImportして使用できます。
非同期GPU readbackを使うSpot Light方式はPlay時に動きます。Renderer Feature方式はEditModeでもポスターによる色加算をプレビューできます。

## LED表示と2テクスチャの切り替え

QuadなどUV0が0〜1の平面に `Mizotake/LED Wall/RGB LED` または `Diffused LED` のマテリアルを割り当てます。Unity標準Quadの正面はローカル-Zです。
マテリアル単体でも `Video / Texture`、`Second Video / Texture`、`Crossfade A to B` を設定できます。0はA、1はB、0.5は線形色空間で半々です。
`LedPanel` を付けると、Texture/SecondaryTexture、Source/SecondarySource、Transition、Resolution、BrightnessValueなどをコンポーネントから制御できます。コンポーネントがある場合、これらの値はMaterialPropertyBlockでマテリアル設定を上書きします。
2枚目が未指定の `LedPanel` は1枚目を両入力へ渡します。マテリアル単体では未指定の2枚目は黒です。
Resolutionは動画の画素数ではなく、物理LEDセルの列数・行数です。RGB型は1セルにR/G/Bの3素子があります。スクリーン上で細かすぎる素子は微分値を使って平均色へなじませます。
SourceとSecondary Sourceには標準の `UnityEngine.Video.VideoPlayer` を直接指定します。API OnlyモードならVideoPlayer自身の出力テクスチャを使用し、複数パネルで共有できます。Render Textureモードの出力も使用できます。準備完了・最初のフレーム取得前やStop後はTexture/Secondary Textureを使用します。再生・停止・ループ・音声はVideoPlayer側で設定します。LedPanelのdisableは共有VideoPlayerを停止せず、外部のRenderTextureも解放しません。
サンプルのカメラは固定です。Play中でもLedPanelのInspectorからTransitionやBrightnessを変更でき、サンプルのControllerによる上書きはありません。Play中の変更はUnity標準どおりPlay終了時に戻るため、保存したい初期値はEditModeで設定してください。
コンポーネント経由のクロップはContent RectのUVスケールとオフセットです。HDR Brightnessはパネルの発光です。Bloomを使う場合はカメラのHDR/Post ProcessingとVolumeを有効にします。

## LEDの基板とレンズ

RGB LEDとDiffused LEDは、HDR発光にURP PBRの表面照明を組み合わせます。Housing Colorは黒い基板の色、Housing Smoothnessは基板の滑らかさ、Lens Smoothnessはレンズの滑らかさ、Lens Curvatureは粒の曲面法線の強さです。Physical Surface Lightingは標準1で、0にすると表面の環境光応答を無効にします。法線の起伏は遠距離で減衰し、DepthNormalsにも反映します。
Brightnessは粒の平均発光量です。RGBの角丸素子、円形レンズ、周期的な拡散光は面積を正規化しているため、FillやDiffusionを変更しても平均輝度が大きく変わりにくい設計です。近距離の粒の中心は面積が小さいほど強いHDR値になります。Bloomやトーンマッピング後の見かけの明るさまで同一になる保証ではありません。
LedPanelを使用する場合、Tintはコンポーネント側で設定します。映像、Tint、Brightness、クロップを照明と共有します。MaterialのBase Map Tiling/Offsetは映像のUVを変更し、LEDセルの数には影響しません。
粒を残すため、サンプルのBloomはIntensity 0.18、Threshold 1.4、Scatter 0.45です。Bloomの強度、パネルのBrightness、カメラの露出・トーンマッピングは見た目に影響するため、併せて調整してください。

## Renderer Featureで映像の色を加える

1. URP RendererのAdd Renderer Featureから `LedColorSpillRendererFeature` を追加します。Spill Shaderには `Hidden/Mizotake/LED Wall/Color Spill` を指定します。サンプルには設定済みです。
2. LedPanelと同じGameObjectに `LedColorSpill` を追加します。Compute Shader未指定時はパッケージのResources内の集計シェーダーを使用します。ビルドでも同じアセットを使用します。
3. Receiver Maskで色を受けるGameObjectのLayerを選びます。受信側は通常のURP Litのまま使用できます。標準サンプルでは床のWaterレイヤーだけを選びます。

Render Graphを有効にしたURPが必要です。Compatibility Modeの実装は含みません。DepthとNormalを要求し、LayerMaskで選んだ可視の不透明Rendererから受信マスクを描きます。同じLayerMaskの複数パネルは、そのカメラで同じマスクを共有します。カメラ色はコピーせず、透明物とポスト処理の前にHDR色を加算します。
GPU集計は、パネルの各領域をタイル単位で加算し、2回目のdispatchで領域平均へまとめる方式です。映像のフル解像度HDRピラミッドは作りません。Light方式と同じ論理サンプリング座標を使い、Tint・クロップ・A/Bフェードも共有します。
Emitter Gridは標準4×2、最大8×4。Updates Per Secondは標準30 Hzです。Response SecondsはGPU内の指数補間、Scene Cut Thresholdは領域ごとの正規化色差によるカット判定です。入力変更、クロップ変更、大きなフェード変更、clip/URL変更、シークで履歴をリセットします。同じ平均色を持つ映像のカットを必ず検出するものではありません。
パネルのワールド位置、右・上方向のベクトル、面積、ローカル-Zの発光面を参照し、受信面の法線、距離、発光面の向きで各領域の寄与を重み付けします。Strengthは芸術的な色加算ゲインです。物理単位で校正されたGIではありません。Override Distributionは標準OFFで、Rangeは対角長×1.3、Soft Distanceは領域サイズから求めます。ONにすると個別の距離とソースの柔らかさを指定できます。
Screen Occlusionは画面内の深度を3点参照する簡易遮蔽です。0で無効になります。画面外の物体、透明物、深度に現れない物体の遮蔽は扱いません。受信マスクは深度一致を使用し、背後の受信面へ前景物体の色が漏れるのを抑えます。独自頂点変形の再現や透過受信には対応していません。
この方式は標準Litの描画結果へ拡散色を足します。BaseMap、Metallic、Smoothnessを読み取るPBR評価は行いません。金属の正確な応答、追加光源としての影、カメラ外の受光が必要な場合はLedPanelLightingを使用してください。Color SpillとLightを同時に有効にすると両方の寄与が加算されます。
サンプルではColor Spillを標準で有効にし、LedPanelLightingは無効で保存しています。`Tools > LED Wall > Gallery Lighting` のメニューで方式を一度だけ切り替えられます。Inspectorから個別に設定することもでき、実行中に値を固定するControllerはありません。

## 通常のURP LitをSpot Lightで照らす

`LedPanel` と同じGameObjectに `LedPanelLighting` を追加し、Sampling Shaderに `Hidden/Mizotake/LED Wall/Lighting Sampler` を割り当てます。
Sampling Backendは標準Computeで、2段階のタイル・領域集計を使用します。Blitを選ぶと従来の2×2面積平均ピラミッドを使用します。最終グリッドの色だけをAsyncGPUReadbackで取得し、実際のSpot Lightへ渡します。Sampling Resolutionは入力画像の最大寸法の目安で、Light Gridに揃えた2の累乗へ切り上げます。非常に細かい明部を残す場合は高く、集計処理を抑える場合は低く設定します。
床や周囲の物体は `Universal Render Pipeline/Lit` のままで受光します。専用床シェーダー、投影用UV、Renderer Featureは不要です。色空間がLinearの場合は、線形サンプルをLight.colorの入力へ変換して渡します。
`Override Light Parameters` は標準でOFFです。色はLedPanelの映像とTransition、光量はBrightnessとワールド面積、照射距離はパネルのワールド対角長を参照します。輝度やTransformを変更すると生成ライトも追従します。自動モデルは光量ゲイン5、距離は対角長×1.3、角度115度、内角比0.72、床方向へのバイアス0.5で面光源を近似します。
個別調整が必要な場合だけ `Override Light Parameters` をONにします。Intensityは輝度・面積に掛ける照明ゲイン、Rangeは照射距離、Spot Angle/Inner Spot Ratioは広がり、Downward Biasは床方向への傾きです。ONでも色・フェード・輝度・位置はLedPanelへ追従します。生成された子Lightを直接変更すると自動更新で戻るため、設定はLedPanelLightingで行います。
`Culling Mask`、`Rendering Layers`、`Render Mode` と影の設定は個別調整のON/OFFにかかわらず指定できます。ライトを再生成するLight Grid変更時にも反映します。URPの受光対象を限定する場合はURP AssetのRendering Layersを有効にし、LightのRendering Layersと受光RendererのRendering Layer Maskを合わせます。Culling Maskは標準Light.cullingMaskへ渡しますが、URPの描画方式による制限があるため、Forward+の受光制御はRendering Layersを使用してください。サンプルURP設定では有効にしています。
デフォルトは4×2個、15 Hz、Sampling Resolution 2048です。光源は最大8×4個まで設定でき、パネルの面積をライト数で分割するためライト数を増やしても総光量が大きく跳ねない構成です。
Soft Distributionは標準でONです。GPUで生成した64×64の柔らかなCookieを共有し、各領域の縦横比に合わせて配光します。OFFにすると通常のSpot Lightの配光へ戻ります。Cookieは色模様ではなく配光の重みです。URP Asset側でもLight Cookiesを有効にしてください。
Response Secondsは標準0.08秒で、GPUから届いた色へ線形色空間で指数補間します。0では即時反映します。Scene Cut Thresholdは標準0.55で、パネル全体の正規化した色差がしきい値を超えると補間履歴をリセットします。0は色差によるカット判定を無効にします。入力テクスチャ・クロップ・動画のclip/URL変更、逆方向のフレーム移動と大きなシークでは履歴をリセットします。カット判定は色統計による近似であり、同じ平均色のカットを必ず識別できるものではありません。
InspectorにはSampling Size、完了した集計数、履歴リセット数、最後のreadback待ち時間を表示します。CPU ProfilerではLED Emission.Compute reduction、LED Lighting.GPU area reduction、LED Lighting.Update generated lightsを確認できます。集計マーカーはGPU命令を発行するCPU区間であり、GPU処理時間そのものではありません。
大きな光源を複数のSpot Lightで近似する表現です。物理的な面光源GIや映像の鏡像ではありません。標準URPの距離・角度減衰、マテリアル、ライトのRendering Layerによる受光規則に従います。
通常のライトとして壁などにも色が乗ります。Cast Shadowsをオンにすれば遮蔽物の影を利用できますが、追加ライト影のコストが増えます。サンプルでは影をオフにしています。
複数ライトを受けるのでForward+を推奨します。通常のForwardではURP AssetのPer Object Limitにより受光するライトが制限されます。標準Litが通常の追加ライトとして受光します。

## 平面反射が必要な場合

サンプルの標準床は鏡像を使用していません。
別途Reflective Floorマテリアルと `LedPlanarReflection` を組み合わせると、平面反射カメラ、HDR RenderTexture、GPUの2方向ぼかし、mip roughness、フレネルによる映像反射を使えます。
床オブジェクトのupが反射面法線です。Source CameraとReflection Blur Shaderを指定します。反射面自体は反射描画中だけ除外します。Reflected Layersも調整できます。サンプルと同じWaterレイヤーは既定で除外します。
カメラが面より下の場合、非URP、ステレオカメラ、カメラ未指定の場合は反射を描画しません。1面・1ソースカメラの方式で、XRの両眼反射には対応していません。

## 性能と制約

GPU処理はLED表示、Compute集計、受信マスクと色加算、またはCookieと通常のURP照明です。Color Spillは色をCPUへ読み戻しません。サンプルでLight方式を選ぶと14灯、各パネル15 Hzの色更新になります。Blitピラミッドは入力寸法に応じたGPUメモリーを使います。Light方式にはreadbackの遅延があり、Response Secondsを0にしても残ります。Color Spillにも設定した更新間隔と時間補間の遅延があります。
AsyncGPUReadback非対応端末やShader未指定では照明を無効にしてLastErrorに理由を返します。Windows/D3D11以外の端末、WebGL、XR、モバイル性能と動画デコーダーは別途検証が必要です。
VideoClipやURLを変更する場合の準備・再生はVideoPlayer側で管理します。VideoClipのコーデック対応は実行OSに依存します。動画例はH.264 / yuv420p / BT.709です。WebGLではVideoClipではなくVideoPlayerのURL再生を設定してください。
MaterialPropertyBlockは他の値を維持しますが、個別に上書きしたRendererはSRP Batcherの一括化対象から外れます。多数のパネルを並べる場合は少数マテリアルでの静的設定や別のインスタンシング方式を検討してください。
disable時にReflection/Lighting/Color Spillが所有するRenderTexture、集計バッファ、生成ライト、隠しMaterialを解放します。Light方式はGPU要求が保留中なら完了を待ってから解放します。VideoPlayerとその出力の寿命は所有側で管理します。Color Spillは全画面の領域積分とDepth/Normal/受信マスクの費用があり、Light方式より速いとは限りません。Emitter GridとScreen Occlusionを含めて対象端末で計測してください。

## 参考

Windows Development Playerの3回比較では、集計用の所有テクスチャ＋バッファはBlit＋Light 39,200,864 bytes、Compute＋Light 266,240 bytes、Compute＋Color Spill 231,536 bytesでした。CPU命令発行の中央値はBlit 0.0232 ms、Compute 0.0129 msです。Color Spillの画面全体の処理、Depth/Normal/受信マスクの一時リソースはこの集計費用に含みません。非表示PlayerのGPUタイムスタンプは取得できず、GPU時間やFPS改善の評価はしていません。詳細と再現コマンドはリポジトリのREADMEとVALIDATION.mdにあります。

- [URPのカスタム照明](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/use-built-in-shader-methods/lighting)
- [RenderPipeline.SubmitRenderRequest](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/rendering/renderpipeline/submitrenderrequest)
- [UPMパッケージ構成](https://docs.unity3d.com/6000.0/Documentation/Manual/cus-layout.html)
- [URPのRendering Layersの有効化と受光対象の設定](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/features/rendering-layers-lights.html)
- [Git URLからのUPM導入](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-git.html)

## ライセンス

コード・ドキュメント・サンプルの生成素材はMITライセンスで提供します。本文はパッケージ直下の `LICENSE.md` にあります。
