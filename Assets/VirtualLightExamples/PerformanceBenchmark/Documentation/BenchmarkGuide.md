# Virtual Light Performance Benchmark

このフォルダーには、Unity標準`Light`とVirtual Lightを同じシーンで切り替えて測定する常設ベンチマークがあります。

## 影付きSpotの比較条件

影付きSpotは次の条件を揃えています。

| 項目 | Unity標準 | Virtual Light |
| --- | --- | --- |
| ライト | Realtime Spot | Virtual Spot |
| Transform / Range / Cone / Color / Intensity | 同じ値 | 同じ値 |
| Shadow解像度 | Custom 512×512 | Medium 512×512 |
| Soft shadow | Medium、9 fetch、5×5 tent | 9 fetch、3×3 weighted |
| caster / receiver geometry | 同一Transform・同一PrimitiveのStandard用複製 | 同一Transform・同一PrimitiveのVirtual用複製 |
| receiver shader | `Universal Render Pipeline/Lit` | `MizoTake/Virtual Light/Benchmark Receiver` |
| Spot数 | 1 / 4 / 8 / 16 | 1 / 4 / 8 / 16 |

Virtual Spotは`Shape = Circle`へ固定し、Unity標準のcone Spotと円形投影条件を揃えています。Rectangle Spotはdirect light / custom shadowの投影形状が異なるため、この同等条件ベンチマークには混在させません。

PC用URP AssetのAdditional Light Shadow Atlasは2048×2048です。512×512を縮小せず格納できる最大数が16であるため、64灯と128灯は影付き同等比較から除外しています。

Virtual側のreceiverは、URP Lit互換shaderの多数の未使用variantを測定対象から外すため、同じmetallic / smoothnessのBRDF初期化とVirtual Light評価だけを行う専用shaderです。Standard / Virtualのreceiver geometryとsurface parameterは一致させています。

影生成方式は同一ではありません。標準URPはatlas、projected depth、hardware comparison、caster-side biasを使います。Virtual LightはTexture2DArray、radial linear depth、manual comparison、receiver-side biasを使います。そのため、このシーンは同じ製品要件と近いサンプル負荷の比較であり、ピクセル完全一致の比較ではありません。

## 手動確認

`Assets/VirtualLightExamples/PerformanceBenchmark/Scenes/VirtualLightPerformanceBenchmark.unity`を開いてPlayするか、Playerを起動します。

- `←` / `→`: 灯数と影条件を変更
- `S`: Unity標準を表示
- `V`: Virtual Lightを表示
- `Space`: Standard / Virtualを切替
- `R`: 現在の条件を両方式で測定
- `A`: 全条件を測定
- `P`: 現在の画面をBMPで保存

測定中はUIを非表示にします。結果と画像はUIに表示される出力先へ保存します。既定値は`Application.persistentDataPath/VirtualLightBenchmarks/<UTC timestamp>/`です。

## Windows Playerビルド

Unityメニューの`Tools > Virtual Light > Build Performance Benchmark Windows Player`を実行します。Release Playerは次へ出力されます。

```text
Builds/VirtualLightPerformanceBenchmark/VirtualLightPerformanceBenchmark.exe
```

引数なしで起動すると、手動操作できる画面を維持します。自動計測例は次のとおりです。

```powershell
Builds\VirtualLightPerformanceBenchmark\VirtualLightPerformanceBenchmark.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -force-d3d12 --benchmark-auto --benchmark-only shadowed --benchmark-warmup 120 --benchmark-samples 300 --benchmark-repeats 2 --benchmark-output D:\VirtualLightBenchmarkResult --benchmark-quit -logFile D:\VirtualLightBenchmarkResult\Player.log
```

`-batchmode`と`-nographics`は描画計測として拒否します。`--benchmark-quit`は保存完了後にだけPlayerを終了します。

## 出力

- `results.json`: 環境、同等条件、各生サンプル、median、p95、GPU timing取得可否
- `summary.csv`: Standard / Virtualを集計しやすい表
- `screenshots/*.bmp`: 最初のrepeatにおける各条件の実画面
- `Player.log`: `-logFile`で出力先を指定した場合のPlayerログ

GPU recorderの有効サンプルが要求数の95%未満なら、`gpuTimingSupported=false`として扱います。欠損値を0 msの測定成功値として比較しないでください。

## シーン再生成

生成規則を変更した場合は`Tools > Virtual Light > Rebuild Performance Benchmark Scene`を実行します。生成後のEditModeテストは、128灯ずつの参照、Transform一致、Custom 512、Soft Medium、Material keyword、Build Settings登録、missing componentを検査します。
