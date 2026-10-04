# 検証記録

## 2026-10-04 UnityLEDKitへの名称変更

originのfetch/push URLを `git@github.com:MizoTake/UnityLEDKit.git` へ変更し、README・パッケージの導入手順・案内URL・サンプルのMITリンクを新しいリポジトリ名へ揃えました。UnityCLIでproductNameをUnityLEDKitへ変更し、計測用ビルドと実行スクリプトの実行ファイル名も揃えています。
UnityCLIでC#コンパイル、EditMode 21/21、PlayMode 13/13が成功し、失敗・skipは0でした。UPM構成監査はエラー0・警告0、サンプル93ファイルとtgzの173ファイルはSHA-256が一致しました。README等のローカルリンク10件に欠落がなく、変更ファイルのUTF-8・BOM保持と計測スクリプトの構文も確認しています。
新規プロジェクトへのtgz導入で6シェーダー・2パネル・標準VideoPlayer・URP Lit床の参照が解決しました。結果は `Logs/LEDWall/consumer-result.json` と `Logs/LEDWall/distribution-result.json` にあります。配布tgzのSHA-256は `16bd858737e022b505dcf083df224a722e4cc8f50934b21b652e0b7dd9709c78` です。

## 2026-10-03 MITライセンス整備

独自コード・ドキュメント・サンプルの生成素材をMITライセンスへ変更しました。リポジトリのLICENSEとUPMのLICENSE.mdを同じ本文・Copyright (c) 2026 MizoTakeで揃え、package.jsonにlicense: MITを設定しました。メディアとサーフェスマップのPROVENANCE.json、両生成コード、READMEとパッケージの使用手順を更新しています。
UnityCLIでC#コンパイル、EditMode 21/21、PlayMode 13/13が成功し、失敗・skipは0でした。UPM構成監査はエラー0・警告0、サンプル93ファイルとtgzの173ファイルはSHA-256が一致しました。元の動画・画像は変更せず、AssetsとSamples~の24件の素材ハッシュを確認しています。生成コードのMIT表記もPython ASTとEditor上のSurfaceEvidenceで確認しました。
新規プロジェクトへのtgz導入では6シェーダー・2パネル・標準VideoPlayer・URP Lit床が解決しました。MIT本文とmetadataの照合結果はLogs/LEDWall/mit-license-result.jsonにあります。最終tgzのSHA-256はe9e0ed12294e5b4eece50cdda56df1e2ef3e5d3f7668f57bd577e05df2e8400cです。

## 2026-10-03 Compute集計とRenderer Feature

Unity 6.0.79f1 / URP 17.0.4 / RTX 4070 / D3D11 / Linearで、2段階のCompute reductionとLightに依存しないColor Spillを追加しました。サンプルの標準はColor Spillです。LedPanelLightingは無効で保存し、Inspectorまたは単発メニューで切り替えます。
ComputeアセットとRenderer Featureの存在確認テストが失敗することを確認してから実装しました。追加したGPUテストはHDRフェード・UV・クロップ・3×2領域でのBlit比較（各RGB誤差0.008以内）、標準LitへのLightなしの色加算、LayerMask、発光面の裏側、距離制限、輝度0、色カット、Panel disable、リソース解放を対象にしています。
最終のUnityCLI検証はC#コンパイル成功、EditMode 21/21、PlayMode 13/13成功で、失敗・skipは0でした。途中のEditorセッションでは、準備済みのVideoPlayerがPlay直後もpausedのままで既存の動画テストが失敗しました。未保存のシーンがないことを確認してEditorを開き直した後、動画テストを変更しない状態で一式が成功しています。再生停止の原因は特定できていません。テストログはLogs/LEDWall/{editor,playmode}-tests.jsonです。
Render Graphのパスは入力Depth/Normalを宣言し、サンプルの発光色テクスチャと受信マスクを明示的に参照します。各記録でPassDataとグラフハンドルを設定し、受信マスクはカメラのdescriptorを基に必要なformatだけ変更します。同じ受信LayerMaskは共有します。カメラ色のコピーとグローバルテクスチャの公開は行いません。受信LayerがカメラのCullingMaskと重ならない場合はパスを投入しません。

Windows Development Playerはビルド成功、エラー0・警告1です。警告は既存PipelineパッケージのRuntimePipelineConfig未設定によるPlayer側Pipeline無効化の通知です。計測用ソースはビルド時のみAssetsへコピーし、AssetDatabase.DeleteAssetで削除しました。Frame Timing Statsも元の設定へ戻しています。
計測用ソースを含まない最終Windows x64ビルドもSucceeded、エラー0・警告1でした。生成されたRuntime DLLにColor Spill、Compute samplerと現在の診断プロパティが含まれることを確認しました。結果はLogs/LEDWall/spill-build-result.json、実行ファイルはBuilds/LEDGallery/UnityLEDSystem.exeです。
1280×720、元の動画、15 Hzの集計、3秒ウォームアップ＋12秒計測を3方式それぞれ3回実行しました。非表示ウィンドウで自動描画が省略された初回の黒い画像は除外し、以後はSubmitRenderRequestで各フレームを明示描画しました。最終画像の色数を確認して、描画していない結果を採用しないようにしています。GPUタイムスタンプは非表示Playerでは取得できず、GPU処理時間・FPSの比較は行っていません。

| 方式 | 所有テクスチャ | 集計バッファ | 集計CPU区間の中央値 | LED用Light |
| --- | ---: | ---: | ---: | ---: |
| Blit＋Light | 39,200,864 bytes | 0 | 0.023178 ms | 14 |
| Compute＋Light | 36,864 bytes | 229,376 bytes | 0.012882 ms | 14 |
| Compute＋Color Spill | 2,160 bytes | 229,376 bytes | 0.012558 ms | 0 |

CPU区間はGPU命令の発行を計測し、GPUの実行時間を示すものではありません。メモリは集計クラスが所有するRTと部分和バッファで、動画、カメラ、材質テクスチャ、Render Graphの一時Depth/Normal/マスクは除外しています。Color Spillの全画面描画コストは表のCPU集計区間に含みません。詳細はLogs/LEDWall/player-benchmark-summary.jsonとplayer-{Blit,Compute,Spill}-{1,2,3}.jsonです。再現用にBuild-LightingBenchmark.ps1とRun-LightingBenchmark.ps1を追加しました。

READMEのGallery静止画とGIFを現在のColor Spillで撮り直しました。GIFは960×540、12秒、144フレーム、8,108,733 bytesです。実キャプチャ143フレームの動画番号は143種類、GIF描画は137種類でした。床のROI(x=260..499,y=410..499)の平均RGBの最大最小差は12.934 / 8.194 / 14.735です。SHA-256は66159bd90d56f473a37b43cbc3433e7d10c18546083afbdd8e6cb41787ecb718です。Logs/LEDWall/spill-gif-result.jsonに記録しました。

UPM構成監査はエラー0・警告0、新規tgz導入では6シェーダー・Computeアセット・2パネル・1つのVideoPlayerと標準Lit床の参照が解決しました。Color Spillが有効、Light方式が無効、Missing ScriptとサンプルControllerがないことを確認しました。配布ファイルは173件のSHA-256を照合しています。
最終tgzのSHA-256は56e5cae928bee3a7f998a21df6568c184b7c9d3f0ed49b86118e10b37b92f236です。サンプル同期は内容が変わったファイルだけを書き換え、同一ファイルを開いている状態でも不要な上書きを避けます。全93ファイルのSHA-256を照合します。

Color Spillは不透明受信面へ加える拡散色です。受信材質のBaseMap・Metallic・Smoothnessを使うPBR評価、画面外の遮蔽物、透明物の受光、独自頂点変形は対象外です。通常のPBR受光と影にはLight方式を使います。D3D12・macOS・モバイル・WebGL・XRは未検証です。

以下は以前の実装時点の記録です。

2026-10-02、Unity 6.0.79f1 / URP 17.0.4 / NVIDIA GeForce RTX 4070 / Direct3D11 / Linear色空間で確認しました。Editor操作、テスト、ビルド、新規プロジェクトの起動にはUnityCLIを使用しています。

| 検証 | 結果 | 記録 |
| --- | --- | --- |
| C#コンパイル | 成功、エラーなし | UnityCLI `recompile` / `recompile_status` |
| EditMode | 15/15成功、失敗・skipなし | `Logs/LEDWall/editor-tests.json` |
| PlayMode | 5/5成功、失敗・skipなし | `Logs/LEDWall/playmode-tests.json` |
| Windows Playerビルド | Succeeded、エラー0、警告1 | `Logs/LEDWall/windows-build.json` |
| UPM構成監査 | エラー0、警告0 | `audit_upm_package.py --fail-on warning` |
| 新規プロジェクトへのtgz導入 | 成功 | `Logs/LEDWall/consumer-result.json` |
| 新規プロジェクトへの公開Git URL導入 | 成功、PackageSource=Git、タグ対象のコミットと一致 | `Logs/LEDWall/git-consumer-result.json` |
| 配布ファイルの一致 | 109ファイルのSHA-256が原本と一致 | `Logs/LEDWall/distribution-result.json` |
| 公開前ソース確認 | UTF-8、検出対象の秘密鍵・トークン・個人パス・生成ディレクトリの混入なし | `Logs/LEDWall/source-audit.json` |
| 公開リモートのファイル照合 | 233ファイルのGit blobがローカルと一致、欠落・混入なし | `Logs/LEDWall/remote-tree-result.json` |
| Git archiveからのUPM構成監査 | 空白・日本語を含む新規パスでエラー0、警告0 | `audit_upm_package.py --fail-on warning` |

GPUテストではRGBの3素子を個別描画し、A/Bフェードの0・0.5・1を確認しました。GPU集計色が実際のライトへ渡り、通常のURP Litの床ピクセルの受光色が変わること、異なるRendering Layersでは受光しないことも検証しました。
ライトの標準設定が自動であること、LedPanelの輝度・サイズへの追従、明示的な個別調整、CullingMask・Rendering Layers・影設定の再生成後の維持を確認しました。
標準VideoPlayerのフレーム進行、一時停止・再開・停止、LedPanelをdisableしても共有動画の再生を止めないことを確認しました。反射行列とGPUぼかし後の平面反射、描画リソースの解放もテスト対象です。
今回の個別調整OFFの仕様は、追加したテストの失敗を確認してから実装し、成功へ変わることを確認しました。

ライブ再生ではInspectorと同じSerializedObject経由でTransition=0.37、Brightness=3.25へ変更し、後のフレームでも値が保持されることを確認しました。カメラ位置は固定、GalleryControllerは0件、生成ライトは14灯、動画フレームは進行していました。

ビルド警告1件は既存UnityCLI接続用Pipelineパッケージによる「RuntimePipelineConfigがないためPlayerではPipelineを無効にする」という通知です。
新規プロジェクトでは配布tgzをUPMへ登録し、tgz内のサンプルをAssetsへコピーしました。5シェーダー、2パネル、1つの標準VideoPlayer、映像・静止画・照明シェーダーの参照が解決し、床は `Universal Render Pipeline/Lit` でした。Missing ScriptやサンプルControllerはありません。元プロジェクトのAssetsやLibraryは使用していません。
最初の検証フォルダーではUnityのBurstパッケージ展開時にアクセス拒否が発生しました。別の新規フォルダーでの再実行は成功しました。

サンプル画像は `Assets/LEDGallery/Preview` にあります。Galleryは動画A、PrismFadeは静止画Bで、床の色も変わります。RgbMacroでは3色のLED素子を確認できます。

配布tgz：`Builds/UPM/com.mizotake.led-wall-0.1.0.tgz`

SHA-256：`1604d5570de97b02e1c55a2936d7f6a30d82a1c20c9d1a6c65df36ea487dc306`

公開タグv0.1.0の導入は `Tools/Verify-Consumer.ps1` で確認しました。PackageSourceはGit、解決されたコミットは `a0ee21b0395bc4574bfc33af1f7523729a499c75` で公開タグv0.1.0と一致しました。5シェーダー・2パネル・1つの標準VideoPlayerの参照が解決し、Missing ScriptやサンプルControllerはありません。
現在のリポジトリは [MizoTake/UnityLEDKit](https://github.com/MizoTake/UnityLEDKit) です。この検証時点でmainとv0.1.0タグをpushしました。
Windows Playerはビルドまでの確認です。再生・GPU受光の実測はEditorのPlayModeで行いました。D3D12、macOS、モバイル、WebGL、XR、他のURPバージョンでの実行は未検証です。この過去の検証時点では独自コード・生成メディアの再利用ライセンスは未指定でした。

## READMEの画像・GIF

READMEに `Assets/LEDGallery/Preview/RgbMacro.png` を掲載し、R/G/B素子の並びと隙間を確認できるようにしました。
`Documentation/Images/LitColorSpill.gif` は、ライトの設定を変えず、固定カメラでUnityの実描画を記録した12秒のループGIFです。960×540、12 fps、144フレーム、7,292,836 bytesです。実キャプチャは142フレームで、記録された動画フレーム番号も142種類でした。GIFの再サンプリングとパレット変換後にも136種類の異なる描画フレームがありました。
床の領域（x=260〜499、y=410〜499）の平均RGB値の最大・最小差は約15.94 / 9.47 / 17.37（0〜255の値）でした。映像の動きと床の受光色の変化を目視でも確認しています。GIFのループ指定、12,000 msの再生時間、README内の画像リンクの存在も確認しました。
最初のキャプチャでは動画フレームが進まなかったため、その結果は採用せず、未保存のシーン変更がないことを確認してEditorを開き直し、再記録しました。録画スクリプトには動画のフレーム進行チェックを追加しています。撮影後はカメラの位置・回転を元に戻しました。
生成手順は `Tools/Record-ReadmePreview.ps1`、詳細な検証値は `Logs/LEDWall/readme-media-result.json` にあります。コア実装・サンプルシーン・v0.1.0タグは変更していません。

## 2026-10-03: LED表面・色光・時間応答・サンプルの改善

UnityCLIで接続中のUnity 6000.0.79f1 / URP 17.0.4、Windows / D3D11 / RTX 4070を使用しました。
新しい依存パッケージとRuntimeのサンプルControllerは追加していません。Packageのversionとv0.1.0タグは維持し、改善内容をUnreleasedとして記録しました。現在の実装のGit導入先は#mainです。

| 検証 | 結果 | 根拠 |
|---|---|---|
| C#再コンパイル | エラー0 | UnityCLI recompile/recompile_status |
| EditMode | 19/19成功、skip 0 | Logs/LEDWall/editor-tests.json |
| PlayMode | 10/10成功、skip 0 | Logs/LEDWall/playmode-tests.json |
| Windows x64ビルド | Succeeded、エラー0、警告1 | Logs/LEDWall/quality-build-result.json |
| UPM監査 | エラー0、警告0 | audit_upm_package.py --fail-on warning |
| サンプル同期 | 93ファイル、SHA-256一致 | Tools/Sync-Sample.ps1 |
| tgz内容 | 156ファイル、元ファイルとのSHA-256一致 | Logs/LEDWall/distribution-result.json |
| 新規プロジェクトへtgz導入 | 5シェーダー、2パネル、標準VideoPlayer 1件、標準Lit床、Missing Scriptなし | Logs/LEDWall/consumer-result.json |
| 新規プロジェクトのRuntime実装 | 現在のRuntimeファイルすべてのSHA-256一致 | 消費側Library/PackageCacheとの比較 |
| GPU生成サーフェスマップ | 9枚、由来ファイルのSHA-256一致 | Assets/LEDGallery/Surfaces/PROVENANCE.json |

面積平均の変更前に、64×64画像中の8ピクセル幅の明るい帯について、期待平均0.125に対して旧9点サンプリングが0を返す失敗を確認しました。GPU縮小ピラミッドへの変更後に成功しました。LedPanelのTintと物理表面プロパティの追加も、存在確認テストの失敗から成功へ変わることを確認しました。
GPUテストはRGBとDiffusedについて、Fill=0.35/0.72/0.9、Diffusion=0/0.7で、近距離と遠距離の平均HDR放射輝度が1から±0.035以内になることを検証しています。発光とは独立して基板が環境光を受けること、TintとMaterial UV/クロップが表示と照明で一致すること、柔らかなCookieの中心と外周、色の急変時の履歴リセットと時間応答のフレーム分割への独立性も検証しました。
動画テストでは準備後のフレーム停止が出たため、サンプルの実再生でフレーム進行を確認したうえで、テストに継続描画するカメラを追加しました。途中のテスト実行はコンパイルで中断されたため、コンパイル完了を待つ一式の検証をやり直しています。最終10件のPlayModeテストでは、標準VideoPlayerの再生・一時停止・再開・停止、共有ソースの所有権も成功しました。
ビルドの警告1件は既存PipelineパッケージのRuntimePipelineConfig未指定による、Player内Pipeline無効化の通知です。

サンプルでは14灯、照明用RenderTexture 22枚でした。Profiler.GetRuntimeMemorySizeLongによる照明用RenderTextureの合計は39,202,272 bytesです。これはテクスチャのメモリー推定値で、動画・カメラ・サーフェスマップは含みません。Sampling Resolutionで縮小ピラミッドの精度とメモリーを調整できます。GPU時間の比較、モバイル・WebGL・XRの性能は未測定です。

更新したGIFは960×540、12秒、144フレームです。実キャプチャ143フレーム中の動画フレーム番号は142種類、デコード後のGIF描画フレームは137種類でした。床の領域(x=260..499,y=410..499)の平均RGB値の最大最小差は12.904 / 7.742 / 16.275です。動画と床の受光色の変化を目視でも確認しました。詳細はLogs/LEDWall/quality-media-result.jsonにあります。
GIFは8,554,096 bytes、SHA-256は79fe35625550378f39addf92be691107f4707dd73caf85bdc526ccd4a89e7213です。
Gallery静止画は元のポスター、RGB拡大はグレーの検証入力を使い、Unityの実描画を撮影しました。変更前後の比較は同じポスターとカメラ位置です。撮影処理はカメラとLedPanelの値を復元しています。変更後の粒にはレンズのハイライト、シーンには面取り筐体と床・布・金属の素材差があります。
