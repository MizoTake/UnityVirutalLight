# Arena Guide

`VirtualLightArenaSample.unity`は、複数のVirtual Lightとbeam volumeを同時に扱うライブ演出向けの例です。

## 24秒のショーフェーズ

- Fan: 6本を扇状にスイープ
- Cross: 左右のfixtureが交差
- Converge: 中央付近へ収束
- Solo: 1本だけを順番に強調

各Spotは独立したshadow sliceを持ち、対応するopaque PBRとbeam raymarchが同じvisibilityを参照します。beam同士は遮蔽せず、RGB radianceを加算します。house fillは舞台を読みやすくするRectangle Areaで、Unity標準`Light`は使用していません。

各SpotのInner Angleは、beam materialの`Core Half-Width`をOuter Angleの実半径へ換算して設定しています。これによりopaque receiver上の最大照度域と、空中で見える高輝度beam coreの太さが同じになります。Outer Angleは低エネルギーの外周とshadow投影範囲を維持するため、受光円だけを小さく見せる目的で狭めないでください。

`Surface Penumbra Sharpness = 1`は、Inner AngleとOuter Angleの境界を動かさず、opaque receiver上の外周光だけを低エネルギー化します。Inner内は最大照度、InnerからOuterは8乗減衰となるため、接地点の明部はbeam coreの太さへ揃いながら、Outer側の淡い光とshadow範囲は残ります。標準的な二乗減衰へ戻す場合は0に設定します。

Hierarchyでは`Lighting/Moving Beam Array`、`Lighting/House Virtual Fills`、`Runtime/Arena Beam Show Controller`を確認してください。
