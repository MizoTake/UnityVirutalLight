# Virtual Light Advanced Examples

このフォルダーは、Package ManagerからImportする最小サンプルよりも複雑な検証・演出・性能調整を扱います。

## 開く順序

1. `Scenes/VirtualLightFeatureLab.unity`
   - Point、Spot、Rectangle Areaを同一画面で比較します。
   - metallic/smoothnessの違い、Spot shadow、first-hit occlusion、beam volumeを確認します。
2. `Scenes/VirtualLightAreaDirectionSample.unity`
   - 同一設定のRectangle Areaを3灯並べ、片面の正方向、片面の背面、両面放射を比較します。
   - `Transform.forward`と`Two Sided`だけで変わる前後方向性を確認します。
3. `Scenes/VirtualLightArenaSample.unity`
   - 6台のmoving headと3灯のhouse fillを同時に動かします。
   - Fan、Cross、Converge、Soloの4フェーズを24秒で切り替えます。

Package側の`Basic Virtual Lights`は、3種類のライトを静的に比較する1シーンだけです。初回導入確認にはPackage sampleを使い、実運用に近い確認にはこのAdvancedフォルダーを使ってください。

方向性の計算と制限は`Documentation/AreaDirectionGuide.md`、その他の詳細は`Documentation`内の各ガイドを参照してください。
