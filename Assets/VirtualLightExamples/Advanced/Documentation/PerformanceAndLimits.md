# Performance and Limits

## 主な負荷

- active light数に応じたstructured buffer upload
- 16x16 screen tileごとのlight selection
- Rectangle Areaのsample数
- shadow付きSpot数とshadow map解像度
- beam volumeのraymarch step数

Beam materialはLow 12、Default 20、High 32 stepです。Highは見た目を確認してから限定的に使用してください。Profilerでは`beginCameraRendering`付近のCPU処理、GPU Profilerではtile culling、shadow、beam passを分けて確認します。

`VirtualLightBeamOcclusion`の自動Physics probeはcomponent既定で最大60 Hz、Feature LabとArenaでは30 Hzです。高refresh-rate表示でraycast数をrender frame数へ比例させず、30fps未満では各frame更新を維持します。`Maximum Refresh Rate = 0`は毎render frame更新なので、必要性を計測してから選択してください。CPU Profilerでは`VirtualLight.BeamOcclusion.Refresh`、`.PhysicsQuery`、`.UpdateVisuals`を分けて確認します。

`Truncate Visual At First Hit`は新しいqueryやcutoff用shader分岐を追加せず、既存hit距離でbeam proxyを短縮します。遮蔽時の画面上のboundsとoverdrawは小さくなりますが、残ったpixelのraymarch step数はLow 12、Default 20、High 32のままです。AdvancedシーンではCollider候補を専用`VirtualLightOccluder`レイヤーへ限定し、marker判定前に無関係なColliderをPhysics queryから除外しています。中央Rayによる断面全体のhard-stopが要件なら有効にし、off-axisの部分遮蔽だけが必要ならcustom shadowを使用してください。impact footprintは4頂点・1 drawの解析shaderで、1 pixelあたりscene depthを1回参照し、追加raymarchやCollider生成は行いません。

packageは固定のlight数やshadow slice数を設定していません。実際の上限はGPU buffer、Texture2DArray、VRAM、frame-time budgetで決まります。shadow resource確保に失敗したライトは消えず、unshadowedとして評価されます。

## 現在の非対応範囲

- Point/Rectangle Area shadow
- Rectangle Spotに対応する四角形のbeam volume / impact footprint
- transparent shadow transmission
- temporal accumulationとmultiple scattering
- generated VPL/light field
- analytic LTC area light
- Rectangle Areaの指向角、barn door、IES配光

ConsoleのWarning/Error、Frame Debuggerのpass順、ProfilerのCPU/GPU負荷を別々に確認してください。
