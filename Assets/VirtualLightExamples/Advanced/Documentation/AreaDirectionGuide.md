# Rectangle Area Direction Guide

`VirtualLightAreaDirectionSample.unity`は、Rectangle Area Lightの面法線と`Two Sided`の違いを、同一条件の3灯で比較するシーンです。

Rectangle Areaは独立したLight Typeです。Point / Spotで選べるRectangle Shapeとは別の機能であり、このSceneではPoint / Spot Shapeを比較しません。

## 3つの比較条件

- Forward-Facing / One Sided: `Transform.forward`を下向きにし、下側のReceiverを照らします。
- Back-Facing / One Sided: `Transform.forward`を上向きにし、下側のReceiverへは放射しません。
- Back-Facing / Two Sided: 上向きのまま`Two Sided`を有効にし、反対側の下向きにも放射します。

3灯はColor、Intensity、Range、Area Size、Area Sample Countを同じ値にしています。見た目の差は回転と`Two Sided`だけから発生します。

## 方向性の意味

Rectangle Area Lightではローカル`+Z`、つまり`Transform.forward`が発光面の法線です。片面では面法線とReceiver方向の内積を`max(0, dot)`として使うため、前方半球だけが照明対象になります。`Two Sided`では`abs(dot)`を使い、前後両半球へ放射します。

Scene ViewではVirtual Lightの矢印Gizmo、Game Viewでは各Emitterから伸びるシアンの矢印でローカル`+Z`を確認できます。

## 調整時の注意

- Area Sample Countは面上のサンプル密度であり、方向の広がりを狭くするパラメータではありません。
- 現在のRectangle AreaはLambert型の半球放射です。Spotのcone angleに相当する指向角、barn door、IES配光は未対応です。
- Rectangle Areaのcustom shadow mapは未対応です。`Cast Shadow`を有効にしてもSpotと同じ影は生成されません。
- Transformの負のscaleは使わず、Rotationで向きを変更してください。

Inspectorで`Show Sample Points`を有効にすると、面積近似に使うサンプル位置も確認できます。
