# Feature Lab Guide

`VirtualLightFeatureLab.unity`は、Virtual Lightの機能を項目ごとに比較するためのシーンです。

## 確認項目

- Point: 全方向の距離減衰と動的な位置更新
- Spot: inner/outer cone、custom shadow、beam volume、first-hit occlusion
- Rectangle Area: 1/2/4/8/16サンプルによる面光源近似
- PBR: smoothness 0.1〜0.9、metallic、clear coat、normal/mask/emission入力
- `Affect Opaque`: package付属のopaque receiverへの寄与を個別に停止
- `VirtualLightOccluder`: custom Spot shadowへ描画するRenderer階層の明示

Physics first-hitは中央Raycast/SphereCastの衝突面をimpactや任意のvisual truncationへ使う機能です。`Beam Impact - Analytic Footprint`は4頂点のQuadと専用shaderを使い、正面では円、斜面ではfinite-aperture beamと面の交差から求めた楕円になります。楕円の中心は中央Rayのhit pointから斜面方向へ移動し、1cmの離隔はbeam軸ではなく面法線方向へ適用されます。inner/outer cone間は滑らかに減衰し、scene depthとhit planeが一致しないpixelは描画しません。浅すぎる入射で交線が放物線・双曲線になる場合は、巨大な疑似円を出さず非表示になります。opaque receiverとbeam volumeの正確な可視性は、ライトごとのcustom shadow sliceが担当します。この2つは同じ機能ではありません。

Play ModeではPointの軌道、Spotの照準、Rectangle Areaの強度変化、遮蔽パネルの移動、遮蔽パネル上でimpactの長短軸と中心が追従することを確認してください。
