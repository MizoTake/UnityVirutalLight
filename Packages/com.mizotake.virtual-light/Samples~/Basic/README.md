# Virtual Light Core Feature Matrix

Open `Scenes/VirtualLightBasicSample.unity` for a script-free overview of the currently supported core light combinations.

## Stations

| Station | Configuration | What it demonstrates |
| --- | --- | --- |
| Directional | Directional | Global contribution that does not depend on position or Range. |
| Circle Point | Point + Circle | The existing spherical influence range. |
| Rectangle Point | Point + Rectangle | A Transform-aligned box range. The orange square guide is rolled 30 degrees. |
| Circle Spot | Spot + Circle | The existing circular cone with Inner/Outer Angle. |
| Rectangle Spot | Spot + Rectangle | A square-pyramid cone using the same angle on both axes. The blue square guide is rolled 30 degrees. |
| Rectangle Area | Rectangle Area, 16 samples | A sampled emitting surface; this is a light Type and is separate from Point/Spot Shape. |

Point and Spot pairs use matching colors and comparable parameters so the Shape difference is easy to identify. The colored circle/square meshes are explanatory boundary guides. Select each `VirtualLight` GameObject in Scene view to inspect the package's actual influence Gizmo and serialized parameters.

All six Virtual Lights are active in one scene, so the low-intensity Directional station provides a small shared baseline and adjacent finite lights can overlap near station edges. Use this scene as a feature/layout overview rather than an isolated photometric measurement.

The receiver and environment materials use `MizoTake/Virtual Light/Lit` with **Receive Standard Lighting** disabled. This isolates Virtual Light contribution from URP main/additional lights, ambient lighting, reflection probes, and baked lighting. The scene intentionally contains no Unity `Light`, sample C# script, UGUI, post-processing, beam volume, or custom shadow setup.

## Where to inspect advanced features

The repository development project keeps specialized examples outside the distributable UPM sample:

- `Assets/VirtualLightExamples/Advanced/Scenes/VirtualLightFeatureLab.unity`: runtime mutation, PBR response, custom shadows, Gobo-masked beam/impact, first-hit occlusion, and impact footprint workflows.
- `Assets/VirtualLightExamples/Advanced/Scenes/VirtualLightAreaDirectionSample.unity`: Rectangle Area forward/back-face and Two Sided comparison.
- `Assets/VirtualLightExamples/Advanced/Scenes/VirtualLightArenaSample.unity`: multiple moving Circle Spot beams and dynamically allocated shadow slices.
- `Assets/VirtualLightExamples/PerformanceBenchmark`: tiled/direct evaluation, light-count scaling, shadow cost, and performance reports.

Rectangle Spot direct lighting and custom shadow projection can be square. Beam and impact proxy bounds remain circular, while an assigned Gobo/2D Cookie masks their visible cross-section and the opaque receiver with the same texture. Directional uses a camera-centered non-cascaded shadow, Point uses six faces, and Rectangle Area uses a center-projection approximation.
