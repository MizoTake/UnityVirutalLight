# Feature Lab Guide

`VirtualLightFeatureLab.unity`は、Virtual Lightの機能を項目ごとに比較するためのシーンです。

Directional、Point/SpotのCircle/Rectangle Shape、Rectangle Areaの静的な基本構成比較はPackage Manager sampleの`Virtual Light Core Feature Matrix`で確認できます。このFeature Labは、同じPoint / Spotを4秒ごとにCircleとRectangleへ切り替えるruntime更新、PBR、Spot shadow、beam/impact、first-hit occlusionの確認に集中します。

## 確認項目

- Point: 動的な位置更新と、Circleの球形範囲／RectangleのTransform追従box範囲のruntime切替
- Spot: Circle cone／Rectangle square pyramidのruntime切替、inner/outer cone、custom shadow、beam volume、first-hit occlusion
- Rectangle Area: 1/2/4/8/16サンプルによる面光源近似
- PBR: smoothness 0.1〜0.9、metallic、clear coat、normal/mask/emission入力
- `VirtualLightOccluder`: custom Spot shadowへ描画するRenderer階層の明示

Physics first-hitは中央Raycastの衝突面をimpactとhard-stop beamへ使う機能です。このシーンでは`Truncate Visual At First Hit`を有効にし、同じ非alloc Raycastの結果で`Raymarch Bounds`自体を最初のhit直前まで短縮します。hitより先のproxyを描画しないためshader内discardで切る方法よりoverdrawを抑えやすく、自動probeは30 Hz、Physics候補は専用`VirtualLightOccluder`レイヤーへ制限しています。移動パネルはkinematic Rigidbodyを`FixedUpdate`から動かし、`Auto Sync Transforms`や毎frameの`Physics.SyncTransforms()`を必要としません。

`Beam Impact - Analytic Footprint`は4頂点のQuadと専用shaderを使い、正面では円、斜面ではfinite-aperture beamと面の交差から求めた楕円になります。楕円の中心は中央Rayのhit pointから斜面方向へ移動し、1cmの離隔はbeam軸ではなく面法線方向へ適用されます。inner/outer cone間は滑らかに減衰し、scene depthとhit planeが一致しないpixelは描画しません。浅すぎる入射で交線が放物線・双曲線になる場合は、巨大な疑似円を出さず非表示になります。中央hitで断面全体を止めるhard-stopと、off-axisを含むopaque receiver／beam volumeの輪郭を表すcustom shadow sliceは用途が異なり、このシーンでは両方を確認できます。

Play ModeではPointの軌道、Spotの照準、Point / Spotが4秒ごとにCircleとRectangleへ切り替わること、Rectangle時にTransform rollが30度になること、Rectangle Areaの強度変化、遮蔽パネルの移動、beamがパネルより先へ出ないこと、遮蔽パネル上でimpactの長短軸と中心が追従することを確認してください。Rectangle Spotではdirect lightとcustom shadowが四角になりますが、beam volumeとimpact footprintは現状の対応範囲どおり円形のままです。
