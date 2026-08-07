# Virtual Light Advanced Examples

このフォルダーは、Package ManagerからImportする最小サンプルよりも複雑な検証・演出・性能調整を扱います。

## 開く順序

1. Package Manager sampleの`Virtual Light Core Feature Matrix`
   - Directional、Circle/Rectangle Point、Circle/Rectangle Spot、Rectangle Areaの現在対応している組み合わせを静的に比較します。
   - Shape境界とTransform rollはこのCore sampleで確認します。
2. `Scenes/VirtualLightFeatureLab.unity`
   - Point / Spotを4秒ごとにCircleとRectangleへ切り替え、runtime Shape更新と30度rollを確認します。
   - metallic/smoothnessの違い、全LightTypeのcustom shadow、128×128 Goboで同じ形にマスクされるsurface／beam／impact、最初のhitで止まるhard-stop beamを確認します。
3. `Scenes/VirtualLightAreaDirectionSample.unity`
   - 同一設定のRectangle Areaを3灯並べ、片面の正方向、片面の背面、両面放射を比較します。
   - `Transform.forward`と`Two Sided`だけで変わる前後方向性を確認します。
4. `Scenes/VirtualLightArenaSample.unity`
   - 円形beamと一致させるためCircle固定の6台のmoving headと、3灯のhouse fillを同時に動かします。
   - Fan、Cross、Converge、Soloの4フェーズを24秒で切り替え、6本すべてのbeamをGoboでマスクし、最初の分類済みColliderで止めます。

Package側のCore Feature Matrixは、6ステーションを静的に比較する導入・Shape確認用の1シーンです。全LightTypeのshadow、SpotのGobo / beam / impact、Rectangle Areaの方向性、moving head演出、負荷測定は役割を混在させず、このAdvancedフォルダーと`Assets/VirtualLightExamples/PerformanceBenchmark`で確認してください。

方向性の計算と制限は`Documentation/AreaDirectionGuide.md`、その他の詳細は`Documentation`内の各ガイドを参照してください。
