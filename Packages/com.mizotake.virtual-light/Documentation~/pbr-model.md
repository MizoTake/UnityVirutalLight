# PBR lighting model

Virtual Light evaluates its direct lighting through the same URP BRDF helpers used by URP Lit. The included `MizoTake/Virtual Light/Lit` shader initializes `BRDFData` from base color, metallic, smoothness, alpha, and optional clear coat values, then evaluates every selected virtual light with `LightingPhysicallyBased`.

This keeps the package's material response aligned with URP's metallic-roughness workflow instead of maintaining a second, subtly different Cook-Torrance implementation.

## Standard URP lighting

The material's **Receive Standard Lighting** option is enabled by default. When enabled, the shader evaluates URP's `UniversalFragmentPBR` path before adding Virtual Lights, so main and additional lights, their shadows, baked lighting, reflection probes, ambient lighting, and SSAO behave like URP Lit. When disabled, those standard contributions are skipped while Virtual Lights and material emission remain active. Shader debug-display variants continue to use URP's standard debug output regardless of this option.

## Material inputs

The receiver shader exposes the following PBR inputs:

- Base Map and Base Color
- Normal Map and Normal Scale
- Metallic Map: red is metallic and alpha is smoothness when the metallic/specular alpha channel is selected
- Occlusion Map: green is ambient occlusion
- Occlusion Strength
- Emission Map and Emission Color
- Clear Coat Mask and Clear Coat Smoothness

When no Metallic Map is assigned, the Metallic and Smoothness properties provide constant values. Smoothness can alternatively come from Base Map alpha, matching URP Lit's smoothness-channel option. Ambient occlusion affects indirect and direct lighting through URP's ambient-occlusion factor; emission is added after lighting.

## Distance and cone attenuation

Point and Spot lights use windowed inverse-square distance attenuation:

```text
attenuation = saturate(1 - (distance / range)^4)^2 / max(distance^2, 0.0001)
```

The smooth window reaches zero at the configured range without replacing the inverse-square core. Circle Point uses world-space distance for both the window and inverse-square terms. Rectangle Point uses the maximum absolute Transform-local X/Y/Z distance for the window, producing a box whose half-extent is Range, while retaining world-space distance for inverse-square energy falloff.

Spot lights multiply distance attenuation by a linearly remapped shape term between the outer and inner boundaries. Circle Spot uses the forward-direction cosine and creates a circular cone. Rectangle Spot takes the maximum absolute horizontal/vertical slope in the Transform-oriented light basis and creates a square pyramid; Inner/Outer Angle apply equally on both axes. `Surface Penumbra Sharpness = 0` uses the standard squared term. Increasing it continuously focuses only the inner-to-outer transition toward an eighth-power term while keeping attenuation at one on the inner boundary and zero on the outer boundary. Rectangle Spot direct-light boundaries and shadow projection follow Transform roll. The optional beam and impact visual path remains circular.

## Gobo / Cookie masks

Each light can reference a grayscale `Texture2D`. Textures are deduplicated for the selected lights, GPU-resampled into a 128x128 `Texture2DArray`, and addressed by a separate metadata buffer so the 80-byte light record stays unchanged. Point uses an equirectangular lookup, Spot projects through its Outer Angle, Directional uses Transform position as the center and Range as world size, and Rectangle Area uses Area Size. Missing textures resolve to a white fallback and preserve the previous output.

Spot beam and impact renderers receive the owning light's source texture through a `MaterialPropertyBlock`. Surface, beam, and impact use the same RGB-luminance multiplied by alpha mask rule.

## Directional lights

Directional lights use `transform.forward` as the direction in which light rays travel. The BRDF therefore receives `-transform.forward` as the surface-to-light direction. Their unmasked direct-light attenuation is constant at one and the light is included in every screen tile. `Intensity` scales the directional light color directly. Transform position and Range define Gobo anchoring and the single camera-centered non-cascaded shadow coverage, but do not add distance attenuation.

## Shadow visibility

Shadow-enabled lights use a custom light-space shadow-map `Texture2DArray`. Spot uses one perspective slice, Point uses six 94-degree faces, Rectangle Area uses front/back center-projection slices, and Directional uses one camera-centered non-cascaded orthographic slice. Visibility multiplies only the owning light's BRDF contribution; it does not impose a shared axial cutoff on the receiver. The participating-media Spot beam shader samples the same Spot slice during raymarching so opaque PBR and beam-volume visibility remain associated with the same light.

Only opaque Renderers below registered `VirtualLightOccluder` hierarchies are in the current caster path. Alpha-clipped and transparent shadow casting are not guaranteed. If the shadow array or metadata buffers cannot be allocated, affected lights continue without shadow visibility rather than being removed from direct lighting.

## Rectangle Area lights

Rectangle Area lights use centered stratified point samples over the emitting rectangle. Sample layouts are symmetric for the supported counts: 1x1, 2x1 or 1x2, 2x2, 4x2 or 2x4, and 4x4. Each sample is weighted by its represented area and by the emitter-facing cosine.

`Intensity` is treated as emitted radiance for Rectangle Area lights, so increasing Area Size increases total emitted power. This differs from Point and Spot intensity, which controls the punctual-light strength directly.

The sampled-area method is intentionally bounded and deterministic, but it is not an analytic area-light solution. Its shadow is likewise a center-projection approximation rather than a multi-sample soft penumbra. It can approximate the diffuse footprint and multiple specular samples, while Linearly Transformed Cosines (LTC) remains a planned alternative with the current sampler retained as a fallback.

## Reference model

- [Unity URP shading model](https://docs.unity3d.com/Manual/urp/shading-model.html)
- [Unity Graphics URP BRDF implementation](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl)
- [Filament physically based rendering guide](https://google.github.io/filament/main/filament.html)
- [Khronos glTF 2.0 metallic-roughness material model](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)
- [Physically Based Shading at Disney](https://disneyanimation.com/publications/physically-based-shading-at-disney/)
