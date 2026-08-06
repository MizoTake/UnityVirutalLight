# Virtual Light 0.1.0

Virtual Light implements the first URP MVP described by the repository's virtual-light simulation specification. It is intended for manually placed or procedurally registered supplemental lights that remain independent of Unity's standard `Light` component.

## Requirements

- Unity 6000.0 or newer in the Unity 6.0 line
- Universal Render Pipeline 17.0.4
- Shader Model 4.5 for the included receiver shader
- Compute shader support for 16x16 tiled selection; unsupported devices use direct evaluation from the dynamically sized structured light buffer

## Component workflow

Add `VirtualLight` to a GameObject and select Point, Spot, or Rectangle Area. Position comes from `transform.position`; Spot and Area direction comes from `transform.forward`; Area orientation also follows Transform roll. Range and Area Size are explicit and ignore Transform scale.

The Inspector clamps negative intensity, invalid radius, cone angle order, Area size, and Area sample count. A negative Transform scale is shown as unsupported.
`Affect Opaque` can be disabled in the Inspector when a Virtual Light should remain registered for custom shader consumers without contributing to the included opaque receiver.

## Runtime API

```csharp
var system = VirtualLightSystem.Current;
var descriptor = VirtualLightDescriptor.Default;
descriptor.Position = transform.position;
descriptor.LinearColor = Color.cyan.linear;
var handle = system.Register(in descriptor);

descriptor.Position += Vector3.right;
system.Update(handle, in descriptor);
system.Unregister(handle);
```

Handles contain an ID and generation. Updating or unregistering a stale handle has no effect on a newer light that reused the same slot.

## Spot beam occlusion

Spot lights can use `VirtualLightBeamOcclusion` with Collider hierarchies marked by `VirtualLightOccluder`. The nearest accepted Physics hit can drive an impact marker and optional legacy beam truncation, but it does not clip PBR contribution with a single plane. See [Spot beam occlusion](beam-occlusion.md) for setup and limits.

The optional Beam shader provides a depth-aware participating-media approximation with a finite source aperture, an analytic camera-ray/frustum interval, stable stratified sampling, a Gaussian high-energy core inside a soft outer envelope, and a normalized forward/isotropic phase mixture. The repository's advanced Arena example demonstrates six independently moving beams, HDR core-to-halo presentation, and accumulated PBR influence. See [Arena beam presentation](arena-beams.md).

The package Basic sample is a static Point, Spot, and Rectangle Area comparison with no sample C# scripts or UGUI dependency. Advanced repository examples use a built-in immediate-mode status overlay.

## Quality and selection

The package does not define a maximum active-light or shadowed-Spot count. Light records, tile indices, and shadow metadata use dynamically sized GPU buffers; shadow-enabled Spots receive dynamically allocated `Texture2DArray` slices. `Low`, `Medium`, `High`, and `Ultra` select 256, 512, 768, and 1024-pixel shadow-slice resolution rather than limiting light count. Actual capacity remains subject to platform texture-array dimensions, GPU memory, allocation success, and the target frame-time budget. If the shadow array cannot be allocated, lights remain active and are evaluated without custom shadow visibility.

## Shader integration

`MizoTake/Virtual Light/Lit` is a forward URP receiver shader with URP Lit-compatible material inputs and opaque, alpha-clipped, or transparent render states. Custom URP shaders can include `Runtime/Shaders/VirtualLight.hlsl` and call `MizotEvaluateVirtualLights`. Shader Graph users can call `VirtualLight_float` as a Custom Function.

The receiver shader uses URP's metallic-roughness BRDF implementation and supports URP Lit's metallic/specular workflow, normal, height, occlusion, emission, detail, surface, culling, and render-state inputs plus optional clear coat. Use **Tools > Virtual Light > Convert URP Lit Materials in Loaded Scenes** to convert deduplicated `Universal Render Pipeline/Lit` materials used by active or inactive Renderers while preserving their compatible properties and textures. Point and Spot lights use windowed inverse-square attenuation; Rectangle Area lights use centered stratified samples weighted by represented emitter area. Shadow-enabled Spots multiply their own BRDF contribution by visibility from the same custom shadow slice sampled by the beam volume. See [PBR lighting model](pbr-model.md) for the input channels, intensity semantics, equations, and current area-light limits.

## Known scope limits

Version 0.1.0 implements custom per-Spot shadow slices for opaque Renderers under registered `VirtualLightOccluder` hierarchies and shares that visibility with the analytic beam volume. It does not guarantee alpha-clipped or transparent shadow casting, transparent transmission, Point or Rectangle Area shadows, full multiple scattering, volumetric penumbra, 2D/3D light fields, temporal accumulation, automatic VPL generation, or ray tracing.
