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
| 配布ファイルの一致 | 109ファイルのSHA-256が原本と一致 | `Logs/LEDWall/distribution-result.json` |
| 公開前ソース確認 | UTF-8、検出対象の秘密鍵・トークン・個人パス・生成ディレクトリの混入なし | `Logs/LEDWall/source-audit.json` |

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

公開Git URLの検証はpush後に `Tools/Verify-Consumer.ps1 -PackageUrl 'https://github.com/MizoTake/UnityLEDSystem.git?path=/Packages/com.mizotake.led-wall#v0.1.0'` で実行します。
Windows Playerはビルドまでの確認です。再生・GPU受光の実測はEditorのPlayModeで行いました。D3D12、macOS、モバイル、WebGL、XR、他のURPバージョンでの実行は未検証です。独自コード・生成メディアの再利用ライセンスは未指定です。
