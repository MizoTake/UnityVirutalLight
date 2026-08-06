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

The smooth window reaches zero at the configured range without replacing the inverse-square core. Spot lights multiply this by a linearly remapped cone term between the outer and inner cone cosines. `Surface Penumbra Sharpness = 0` uses the standard squared term. Increasing it continuously focuses only the inner-to-outer transition toward an eighth-power term while keeping attenuation at one on the inner boundary and zero on the outer boundary. This makes the visible receiver highlight follow the inner cone without changing beam bounds or shadow projection.

## Spot shadow visibility

Shadow-enabled Spot lights use a custom light-space shadow-map `Texture2DArray`. The array and its matrix metadata are sized dynamically, with a separate slice for each eligible Spot. Visibility from that slice multiplies only the owning light's BRDF contribution; it does not impose a shared axial cutoff on the receiver. The participating-media beam shader samples the same slice during raymarching so opaque PBR and beam-volume visibility remain associated with the same light.

Only opaque Renderers below registered `VirtualLightOccluder` hierarchies are in the current caster path. Alpha-clipped and transparent shadow casting are not guaranteed. If the shadow array or metadata buffers cannot be allocated, affected lights continue without shadow visibility rather than being removed from direct lighting.

## Rectangle Area lights

Rectangle Area lights use centered stratified point samples over the emitting rectangle. Sample layouts are symmetric for the supported counts: 1x1, 2x1 or 1x2, 2x2, 4x2 or 2x4, and 4x4. Each sample is weighted by its represented area and by the emitter-facing cosine.

`Intensity` is treated as emitted radiance for Rectangle Area lights, so increasing Area Size increases total emitted power. This differs from Point and Spot intensity, which controls the punctual-light strength directly.

The sampled-area method is intentionally bounded and deterministic, but it is not an analytic area-light solution. It can approximate the diffuse footprint and multiple specular samples, while Linearly Transformed Cosines (LTC) or another analytic integration method remains a future option for smoother rectangle-shaped highlights.

## Reference model

- [Unity URP shading model](https://docs.unity3d.com/Manual/urp/shading-model.html)
- [Unity Graphics URP BRDF implementation](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl)
- [Filament physically based rendering guide](https://google.github.io/filament/main/filament.html)
- [Khronos glTF 2.0 metallic-roughness material model](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)
- [Physically Based Shading at Disney](https://disneyanimation.com/publications/physically-based-shading-at-disney/)
