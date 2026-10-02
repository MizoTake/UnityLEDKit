# 検証記録

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

公開Git URLは `Tools/Verify-Consumer.ps1 -PackageUrl 'https://github.com/MizoTake/UnityLEDSystem.git?path=/Packages/com.mizotake.led-wall#v0.1.0'` で導入を確認しました。PackageSourceはGit、解決されたコミットは `a0ee21b0395bc4574bfc33af1f7523729a499c75` で公開タグv0.1.0と一致しました。5シェーダー・2パネル・1つの標準VideoPlayerの参照が解決し、Missing ScriptやサンプルControllerはありません。
リポジトリは [MizoTake/UnityLEDSystem](https://github.com/MizoTake/UnityLEDSystem) です。mainとv0.1.0タグをpushしています。
Windows Playerはビルドまでの確認です。再生・GPU受光の実測はEditorのPlayModeで行いました。D3D12、macOS、モバイル、WebGL、XR、他のURPバージョンでの実行は未検証です。独自コード・生成メディアの再利用ライセンスは未指定です。

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
