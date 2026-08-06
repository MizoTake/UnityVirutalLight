# Virtual Light

Virtual Lightは、Unity標準の`Light`とは独立して動作する、Unity 6 / Universal Render Pipeline（URP）向けのGPU駆動ライトパッケージです。Point、Spot、Rectangle Areaの仮想ライトをコンポーネントまたはRuntime APIから登録し、付属の受光シェーダーやカスタムシェーダーで評価できます。

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

- Point、Spot、Rectangle Areaの3種類
- `VirtualLight`コンポーネントによる手動配置とInspector編集
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
- `VirtualLightBeamOcclusion`によるColliderのfirst-hit判定、impact footprint、任意のvisual truncation
- finite-aperture beam frustum、depth fade、安定化したsamplingを使う加算合成beam volume
- 正面では円、斜面では有限楕円となる解析的なimpact footprint

## クイックスタート

1. Package Managerで**Basic Virtual Lights** sampleをImportします。
2. GameObjectへ**Add Component > Rendering > Virtual Light**から`VirtualLight`を追加します。
3. 受光するRendererのマテリアルに`MizoTake/Virtual Light/Lit`を使用します。既存のURP Litマテリアルは変換ツールを利用できます。
4. Point、Spot、Rectangle Areaを切り替え、色、強度、範囲、cone angle、area sizeなどを調整します。
5. 独自システムから動的に生成する場合は、[`Documentation~/index.md`](Packages/com.mizotake.virtual-light/Documentation~/index.md)のRuntime API例を参照してください。

標準のURP Litマテリアルは、そのままではVirtual Lightを受光しません。付属シェーダーへの変更、変換ツール、またはHLSL組み込みのいずれかが必要です。

## サンプル

| 種類 | 場所 | 内容 |
| --- | --- | --- |
| Basic | Package Managerの**Basic Virtual Lights** | Point、Spot、Rectangle Areaを比較するscript-freeの静的scene。UPM導入後の初期確認向け |
| Advanced | `Assets/VirtualLightExamples/Advanced` | PBR比較、Rectangle Areaの方向性、Spot shadow、occlusion、複数beam、6台のmoving head演出。リポジトリをUnityプロジェクトとして開いて確認 |

UPMからImportできるのはBasic sampleです。Advanced examplesはパッケージには含まれず、このリポジトリの開発プロジェクト側にあります。

## 現在の制限

- PointおよびRectangle Areaのshadowは未対応
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
