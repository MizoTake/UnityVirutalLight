# Virtual Light

Virtual Light is a Unity 6 / URP package for runtime-managed point, spot, and rectangle virtual lights. It uploads an 80-byte structured GPU record per active light through dynamically sized GPU buffers, performs 16x16 screen-tile selection on compute-capable hardware, falls back to direct structured-buffer evaluation when tiled selection is unavailable, and evaluates direct light through URP's metallic-roughness BRDF. The package does not impose a fixed light-count ceiling; practical capacity depends on platform GPU resource limits, memory, and the frame-time budget.

## Install

Add the package from disk in Package Manager, or add a file or Git reference for `com.mizotake.virtual-light`. The initial `0.1.0` release targets Unity `6000.0` and URP `17.0.4`.

This repository hosts the package under `Packages/com.mizotake.virtual-light`, so Git installation uses the repository URL with `?path=/Packages/com.mizotake.virtual-light`.

## Use

1. Import **Basic Virtual Lights** from Package Manager Samples.
2. Add `VirtualLight` components through **Add Component > Rendering > Virtual Light**.
3. Use the `MizoTake/Virtual Light/Lit` material shader on receiving objects. It follows URP Lit's texture, metallic/specular workflow, normal, height, occlusion, emission, detail, surface, culling, and render-state properties, and adds optional clear coat plus Virtual Light evaluation. **Receive Standard Lighting** is enabled by default and can be disabled per material when the receiver should ignore URP main/additional lights, their shadows, baked lighting, reflection probes, and ambient lighting while continuing to receive Virtual Lights and emission. Custom shaders can instead call `VirtualLight_float` from `Runtime/Shaders/VirtualLight.hlsl`.
4. Register procedural lights through `VirtualLightSystem.Current`, which exposes `IVirtualLightSystem`.

The Basic sample contains one static scene with Point, Spot, and Rectangle Area lights. It has no sample C# scripts and does not require UGUI.

For Spot lights, **Surface Penumbra Sharpness** controls how strongly the opaque receiver highlight follows the Inner Angle. Zero preserves the standard squared inner-to-outer falloff. One keeps the same Inner/Outer boundaries but concentrates the penumbra with a multiply-only eighth-power profile; beam geometry and shadow projection remain unchanged.

To convert materials already used by Renderers in open scenes, run **Tools > Virtual Light > Convert URP Lit Materials in Loaded Scenes**. The command finds `Universal Render Pipeline/Lit` materials on active and inactive Renderers, deduplicates shared references, and converts each material in place while preserving every same-named, same-typed shader property, texture scale/offset, local keyword, render queue, instancing flag, double-sided GI flag, and global-illumination flags. Because material assets are shared, the confirmation dialog warns that prefabs and other scenes using the same assets are also affected; Editor Undo is registered for the conversion. Embedded imported materials, Material Variants, and read-only assets are skipped with an actionable Console warning so imported or inherited data is not silently lost.

For a Spot beam that reports a Collider hit, add `VirtualLightBeamOcclusion` and mark accepted blocker hierarchies with `VirtualLightOccluder`. The Physics probe drives an optional finite-aperture impact footprint and legacy visual-truncation behavior; perpendicular surfaces receive a circular footprint and oblique surfaces receive the exact finite ellipse. It does not apply one axial cutoff plane to opaque lighting.

Use a unit Quad with `MizoTake/Virtual Light/Impact Footprint` for a soft inner/outer-cone impact that clips to scene depth instead of rendering a uniformly emissive floating disc.

Assign a unit Cube using `MizoTake/Virtual Light/Beam` as Beam Visual to render a Spot-angle-matched homogeneous single-scattering approximation. The shader clips each camera ray to a finite-aperture beam frustum and uses stable stratified sampling instead of exposing coherent box-space raymarch slices. A Gaussian high-energy core, lower-energy outer envelope, and normalized Henyey-Greenstein/isotropic phase mixture provide camera-dependent optical thickness without changing the geometric cone. Beam volumes never occlude one another and use additive RGB radiance. The repository host project provides the advanced Feature Lab, Rectangle Area direction, and six-moving-head Arena examples under `Assets/VirtualLightExamples/Advanced`.

When Cast Shadow is enabled on a Spot Virtual Light, registered opaque occluder Renderers are drawn into the package's custom shadow-map array. Its `Texture2DArray` and companion matrix buffers grow dynamically, with one slice assigned to each shadowed Spot for the current camera. The opaque PBR receiver and analytic beam volume sample the same slice, so a blocker affects only its owning light while overlapping beam radiance remains additive. If a shadow resource cannot be allocated, the light remains active and is evaluated unshadowed.

The package does not modify a renderer asset. GPU upload occurs at `RenderPipelineManager.beginCameraRendering`, so Forward and Forward+ URP renderer assets can share the same package setup.

## MVP boundary

This release covers manual Point, Spot, and sampled Rectangle Area lights, handle-safe runtime mutation, dynamically sized GPU upload, tiled selection with direct-evaluation fallback, opaque material lighting, custom Spot shadow maps shared with the beam volume, optional Physics first-hit effects, and editor handles. Point and Rectangle Area shadows, transparent shadow transmission, light fields, temporal accumulation, shared-medium multiple scattering, and generated VPLs remain outside the current scope.

## Development

Open the repository root as a Unity `6000.0.79f1` project. It embeds this package, includes UniCli Server `1.6.0`, and hosts advanced examples under `Assets/VirtualLightExamples/Advanced` for package compilation, tests, runtime checks, and player compilation.
