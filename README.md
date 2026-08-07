# Virtual Light

Virtual Lightは、Unity標準の`Light`とは独立して動作する、Unity 6 / Universal Render Pipeline（URP）向けのGPU駆動ライトパッケージです。Directional、Circle/Rectangle Point、Circle/Rectangle Spot、Rectangle Areaの仮想ライトをコンポーネントまたはRuntime APIから登録し、付属の受光シェーダーやカスタムシェーダーで評価できます。

> [!IMPORTANT]
> 現在のpackage manifestは`0.1.0`です。`master`には[CHANGELOG](Packages/com.mizotake.virtual-light/CHANGELOG.md)の`Unreleased`に記載された開発中の変更が含まれており、固定リリースタグはまだありません。

## UPMで導入

Unityの**Window > Package Management > Package Manager**を開き、**Install package from Git URL**に次のURLを入力すると、このリポジトリ内のパッケージだけを導入できます。

操作手順とGit URLのサブフォルダー指定は、[Unity公式マニュアル](https://docs.unity3d.com/ja/6000.0/Manual/upm-ui-giturl.html)も参照してください。

**UPM Git URL:** [https://github.com/MizoTake/UnityVirutalLight.git?path=/Packages/com.mizotake.virtual-light](https://github.com/MizoTake/UnityVirutalLight.git?path=/Packages/com.mizotake.virtual-light)

```text
https://github.com/MizoTake/UnityVirutalLight.git?path=/Packages/com.mizotake.virtual-light
```

`Packages/manifest.json`へ直接追加する場合は、`dependencies`へ次の1行を追加します。

```json
{
  "dependencies": {
    "com.mizotake.virtual-light": "https://github.com/MizoTake/UnityVirutalLight.git?path=/Packages/com.mizotake.virtual-light"
  }
}
```

このURLは既定ブランチの最新状態を追従します。再現可能なバージョン固定URLは、リリースタグを作成した段階で案内する予定です。

## 動作要件

- Unity `6000.0`（Unity 6.0）
- Universal Render Pipeline `17.0.4`
- 付属受光シェーダーはShader Model 4.5を使用
- Git URLから導入する場合は、Unityから利用できるGitクライアントが必要
- Compute Shader対応環境では16x16 screen tile単位のライト選択を使用し、非対応環境ではdynamic structured bufferの直接評価へフォールバック

開発プロジェクトはUnity `6000.0.79f1`で構成されています。

## 現在実装済みの範囲

### ライトとRuntime API

- Directional、Point、Spot、Rectangle Areaの4種類と、Point / Spotから独立して選べるCircle / Rectangle Shape
- `VirtualLight`コンポーネントによる手動配置とInspector編集
- **Tools > Virtual Light**から、現在の編集Stageまたは選択中のUnity標準`Light`をパラメーター付きで`VirtualLight`へ置換
- `VirtualLightSystem.Current` / `IVirtualLightSystem`による登録、更新、解除
- 世代付きhandleによる、再利用済みslotへの古いhandle操作の防止
- active light数に合わせて拡張されるGPU buffer。パッケージ側の固定light数上限はなし
- Rectangle Areaの片面／両面方向性とsample数設定

### 受光とURP統合

- URP Lit互換の入力を持つ`MizoTake/Virtual Light/Lit`シェーダー
- metallic/specular workflow、normal、height、occlusion、emission、detail、clear coat、alpha clip、transparent surfaceをサポート
- マテリアル単位の**Receive Standard Lighting**で、Virtual LightとUnity標準照明の併用／分離を選択可能
- `Runtime/Shaders/VirtualLight.hlsl`を使ったカスタムURP shader / Shader Graph Custom Functionからの評価
- **Tools > Virtual Light > Convert URP Lit Materials in Loaded Scenes**による、読み書き可能なURP Litマテリアルの変換
- Renderer Assetを変更せず、URPのForward / Forward+から利用可能

### Spot shadowとbeam表現

- `VirtualLightOccluder`配下の不透明Rendererを対象にした、Spotごとのcustom shadow-map slice
- opaque receiverとbeam volumeで同じSpot shadowを共有
- `VirtualLightBeamOcclusion`によるColliderのfirst-hit判定、impact footprint、最初のhitで止めるbeam-proxy truncation
- finite-aperture beam frustum、depth fade、安定化したsamplingを使う加算合成beam volume
- 正面では円、斜面では有限楕円となる解析的なimpact footprint

## Unity標準Lightとの違い

ここでいうUnity標準Lightは、このプロジェクトと同じUnity 6.0 / URP 17.0.4で使用する`UnityEngine.Light`と`UniversalAdditionalLightData`を指します。Virtual Lightは標準Lightの完全互換実装ではなく、標準Lightから独立したGPU管理、矩形の影響形状、runtime Rectangle Area、beam演出を追加する補助ライトシステムです。標準Lightの仕様は[Unity公式のURP Light component](https://docs.unity3d.com/ja/6000.0/Manual/urp/light-component.html)と[URP rendering path比較](https://docs.unity3d.com/ja/6000.0/Manual/urp/rendering-paths-comparison.html)を基準にしています。

| 観点 | Virtual Light | Unity標準Light / URP | 実用上の違い |
| --- | --- | --- | --- |
| ライト種別と形状 | Directional、Circle/Rectangle Point、Circle/Rectangle Spot、Rectangle Area。Rectangle Pointはbox、Rectangle Spotはroll可能なsquare pyramidとして評価 | Directional、Point、Spot、Area。通常のPointは球、Spotは円錐、AreaはRectangle / Disc | Virtual LightはPoint / Spotの矩形境界を選べますが、Disc Areaと平行光線のBox Spotは扱えません |
| Area Light | 1 / 2 / 4 / 8 / 16点で近似するRectangle Areaをruntime評価し、片面／両面を選択可能 | URPのArea LightはRectangle / Discを選べますが、ModeはBakedに固定 | 動くRectangle Areaや即時変更はVirtual Light向きです。Virtual Lightは解析的なArea Lightではなく、sample数に応じた近似です |
| runtime操作と灯数 | Componentに加えてhandle付きAPIで登録、更新、解除し、light bufferを需要に応じて拡張。パッケージ固有の固定灯数上限なし | `Light`をruntime操作可能。灯数はrendering pathとplatformの制限を受け、Unity 6のForward+はカメラあたり最大256灯 | Virtual Lightは標準Lightの灯数枠とは独立しますが、無制限または高速という意味ではありません |
| 受光shader | `MizoTake/Virtual Light/Lit`、HLSL include、Shader Graph Custom Functionのいずれかが必要 | URP Litなど標準のLit系shaderがmain / additional lightを既定で受光 | 標準URP Lit、Terrain、Particle、独自shaderなどへVirtual Lightが自動では反映されません。対象shaderごとの組み込みが必要です |
| 標準照明との併用 | Materialごとの**Receive Standard Lighting**で、標準照明とVirtual Lightを加算するか、Virtual Lightとemissionだけに分離するかを選択可能 | 標準lighting loop内でdirect light、shadow、baked lighting、ambient、reflection probe、SSAOなどを統合 | 既定は併用です。Virtual Lightだけを比較したいreceiverでは標準照明をOFFにできます |
| Realtime / Mixed / BakedとGI | Virtual Light自身はruntime direct lightで、lightmap、Mixed Lighting、Light Probe / APVへの注入を行わない | Realtime / Mixed / Bakedを選び、Baked GI、lightmap、Light Probe / APVなどの標準workflowを利用可能 | 環境照明や静的sceneのbakeは標準Lightを使用する必要があります。標準照明を併用したreceiverは、標準側からのbaked lightingやprobeを引き続き受けられます |
| shadow | Spotのみ。明示的に登録した`VirtualLightOccluder`配下のopaque RendererをlightごとのTexture2DArray sliceへ描画し、surfaceとbeamで共有 | Directional / Point / SpotのHard / Soft shadow、Directional cascade、per-light strength、bias、near plane、quality、resolutionなどを利用可能 | Virtual LightはDirectional / Point / Rectangle Area shadowに未対応で、shadow設定もproject-wide quality / bias / caster layerが中心です |
| Cookieと照射対象の分離 | Light Cookieなし。direct lightは`Affect Opaque`、shadow casterはglobal layer maskと`VirtualLightOccluder`で制御 | Directional / Spotの2D Cookie、PointのCubemap Cookie、Culling Mask、Rendering Layers、Custom Shadow Layersを利用可能 | 模様投影、per-lightのlight linking、照明対象とshadow対象の細かな分離は標準Lightが適しています |
| transparent | 付属Lit shaderはtransparent surfaceのrender stateを持ち、Virtual Lightを加算可能 | URP Litのtransparent surfaceが標準lighting loopを利用 | Virtual Lightはtransparent専用の透過lighting modelを持たず、alpha clip / transparent casterのcustom shadowとtransparent shadow transmissionは保証対象外です |
| beamとimpact | Spotにfinite-aperture beam、single-scattering近似、Collider first-hit、斜面上の楕円impact footprintを追加可能 | 標準Light component単体は空気中の可視beamやimpact meshを生成しない | moving head、laser、stage beamの一体制御はVirtual Lightの特徴です。ただしRectangle Spotでもbeam / impact形状は円形です |
| 色と強度 | HDR colorと相対的なIntensityを使用。Rectangle AreaはIntensityをemitted radianceとして扱い、面積を増やすと総出力も増加 | Color、Color Temperature、Intensity、Indirect Multiplierを標準Inspectorで設定 | 変換時は色温度をRGBへ反映しますが、同じIntensity値による見た目や測光的な一致は保証しません |
| 対応範囲 | Unity 6.0、URP 17.0.4、Shader Model 4.5を対象。Forward / Forward+で利用 | Unity標準機能としてrendering pipeline、shader、baking、editor toolingと統合 | Virtual LightはBuilt-in Render Pipeline / HDRPのdrop-in代替ではなく、URP向けの専用実装です |

### このプロジェクトの特徴

- Unity標準Lightとは別のGPU bufferでruntime lightを管理し、Component配置とproceduralな大量生成を同じAPIへ統合できる
- Point / SpotのtypeとCircle / Rectangle shapeを分離し、標準Pointにはないbox境界、標準Spotとは異なるsquare-pyramid境界をTransform roll込みで制御できる
- 標準URPではBaked固定のRectangle Areaを、sampled approximationとしてruntimeに移動・変更できる
- 1つのSpotについてsurface lighting、custom shadow、visible beam、impactを同じTransformと強度から同期できる
- 既存の環境照明を標準Lightへ任せたままVirtual Lightを追加でき、receiver単位で標準照明の有無を比較できる
- Renderer AssetへRenderer Featureを追加せず、`RenderPipelineManager.beginCameraRendering`からglobal GPU resourceを更新する

### デメリットと採用時の注意

- 標準URP Litへ自動では反映されないため、material変換またはshader実装が必要です。対応していないshaderはVirtual Lightを受光しません
- `UnityEngine.Light`型の参照を維持できず、Cookie、bake mode、culling / rendering layer、shadow詳細も変換できないため、既存sceneを完全自動移行できません
- 標準LightのDirectional / Point shadow、cascade、per-light shadow品質、Baked GI、Light Probe / APV、Cookie、Rendering Layersが必要な用途では置き換えになりません
- custom Spot shadowではoccluder hierarchyを明示的に登録する必要があり、alpha clip / transparent caster、透明透過、Point / Directional / Area shadowは現在の保証範囲外です
- light数に固定上限はありませんが、light buffer、tile index、shadow slice、shadow caster draw、VRAM、GPU時間が灯数と解像度に応じて増えます。Unity標準Lightより常に高速という測定結果はありません
- Rectangle Areaは有限sample近似、beamはhomogeneous single-scattering近似です。analytic LTC、IES、barn door、multiple scattering、完全なvolumetric penumbraは実装していません
- 対象がUnity 6.0 / URP 17.0.4に限定され、Built-in Render PipelineやHDRP向けの互換backendはありません

### 推奨する使い分け

- 環境の主照明、太陽、bake、GI、probe、Cookie、厳密なlayer分け、Directional / Point shadowにはUnity標準Lightを使用します
- 多数の動的な補助灯、矩形Point / Spot、runtime Rectangle Area、stage beam / impact演出にはVirtual Lightを使用します
- 一般的なsceneでは標準Lightを基礎照明、Virtual Lightを演出・補助照明として併用し、特殊receiverまたは比較sceneだけ**Receive Standard Lighting**をOFFにする構成を推奨します

## クイックスタート

1. Package Managerで**Virtual Light Core Feature Matrix** sampleをImportします。
2. GameObjectへ**Add Component > Rendering > Virtual Light**から`VirtualLight`を追加します。
3. 受光するRendererのマテリアルに`MizoTake/Virtual Light/Lit`を使用します。既存のURP Litマテリアルは変換ツールを利用できます。
4. Directional、Point、Spot、Rectangle Areaを切り替えます。Point / SpotではTypeとは別にCircle / Rectangle Shapeを選び、色、強度、範囲、cone angle、area sizeなどを調整します。Directionalは位置と範囲に依存せず、`transform.forward`を光線の進行方向として使用します。
5. 独自システムから動的に生成する場合は、[`Documentation~/index.md`](Packages/com.mizotake.virtual-light/Documentation~/index.md)のRuntime API例を参照してください。

標準のURP Litマテリアルは、そのままではVirtual Lightを受光しません。付属シェーダーへの変更、変換ツール、またはHLSL組み込みのいずれかが必要です。

### Unity標準Lightからの置換

**Tools > Virtual Light > Convert Light Components in Current Stage**は、通常のScene編集では読み込まれているScene内、Prefab StageではそのPrefab内の`Light`を置換します。**Convert Selected Light Components**は、選択GameObject自身に付いている`Light`だけを置換します。Light Inspectorのcontext menuから1件ずつ実行することもできます。

Directional、Point、cone / pyramid Spot、Rectangleのtype、shape、color、color temperature、intensity、enabled、適用可能なrange、Spot angle、Rectangle size、shadow有効状態を対応する値へ引き継ぎます。PyramidはSpot + Rectangleへ変換します。Disc、Box、Tube、既存`VirtualLight`と競合するもの、未知の`Light`必須componentを持つものは元の`Light`を残してスキップします。`Light`型の参照、Cookie、bake設定、culling／rendering layer、shadow詳細は引き継げません。Directional／Point／Rectangle shadowは未対応で、Spot shadowには`VirtualLightOccluder`が必要です。変換はUndo可能ですが、SceneやPrefabは自動保存しません。

## サンプル

| 種類 | 場所 | 内容 |
| --- | --- | --- |
| Core Feature Matrix | Package Managerの**Virtual Light Core Feature Matrix** | Directional、Circle/Rectangle Point、Circle/Rectangle Spot、Rectangle Areaをラベルと境界guide付きで比較するscript-freeの静的scene |
| Feature Lab | `Assets/VirtualLightExamples/Advanced` | Point / SpotのCircle・Rectangle runtime切替、PBR比較、Spot shadow、first-hit hard-stop beam / impactを確認 |
| Area / Arena | `Assets/VirtualLightExamples/Advanced` | Rectangle Areaの方向性、最初のColliderで止まる複数beam、6台のmoving head演出を確認 |
| Performance | `Assets/VirtualLightExamples/PerformanceBenchmark` | tiled/direct経路、light数、shadow数、CPU/GPU負荷を計測 |

UPMからImportできるのはVirtual Light Core Feature Matrix sampleです。Advanced examplesはパッケージには含まれず、このリポジトリの開発プロジェクト側にあります。

## 現在の制限

- Directional、PointおよびRectangle Areaのshadowは未対応
- alpha clip / transparent objectのshadow castingとtransparent shadow transmissionは保証対象外
- light field、temporal accumulation、multiple scattering、generated VPL、ray tracingは未対応
- Rectangle Areaはsampled approximationで、analytic LTC、barn door、IES配光には未対応
- 固定light数上限は設けていませんが、実際の上限はGPU buffer、Texture2DArray、VRAM、allocation、frame-time budgetに依存
- shadow resourceを確保できない場合、対象ライトは無効化せずunshadowedとして評価

## リポジトリ構成

- `Packages/com.mizotake.virtual-light`: 配布対象のUPMパッケージ
- `Packages/com.mizotake.virtual-light/Samples~/Basic`: Package ManagerからImportする最小sample
- `Packages/com.mizotake.virtual-light/Documentation~`: package user向けの設計、設定、制限の詳細
- `Assets/VirtualLightExamples/Advanced`: repository-onlyの高度なsceneとガイド
- `Packages/com.mizotake.virtual-light/Tests`: EditMode / PlayMode tests

詳細は[package README](Packages/com.mizotake.virtual-light/README.md)と[package documentation](Packages/com.mizotake.virtual-light/Documentation~/index.md)を参照してください。

## 開発

リポジトリルートをUnity HubからUnity `6000.0.79f1`プロジェクトとして開けます。パッケージはembedded packageとして配置され、package compilation、tests、sample scene、player compilationを同じプロジェクトで確認できます。

## License

[MIT License](Packages/com.mizotake.virtual-light/LICENSE.md)
