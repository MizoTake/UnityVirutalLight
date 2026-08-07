# Changelog

## [Unreleased]

- Added a Type-independent Circle/Rectangle shape for Point and Spot Virtual Lights. Rectangle Point uses a Transform-aligned box range, Rectangle Spot uses a Transform-aligned square-pyramid cone and rolled shadow projection, tiled culling covers box corners, and Unity Pyramid lights convert to Spot + Rectangle while non-equivalent Box Spot lights remain unchanged.
- Reorganized the script-free UPM sample into a labeled six-station core feature matrix covering Directional, Circle/Rectangle Point, Circle/Rectangle Spot, Rectangle Area, rolled boundary guides, isolated receiver materials, and links to specialized Advanced/benchmark examples.
- Updated the repository-only Feature Lab to switch its Point and Spot between Circle and Rectangle at runtime, apply a 30-degree Rectangle roll, restore the Spot target wiring, report the active Shape in its overlay, and keep beam-backed Arena Spots explicitly on Circle.
- Added a project-wide Virtual Light System Settings window for persisted shadow quality, depth bias, normal bias, and caster-layer defaults shared by Editor rendering and Player startup.
- Added Directional Virtual Lights with position- and range-independent direct lighting, full-screen tile selection, forward-direction editor gizmos, and Unity Directional Light conversion. Custom Directional shadows remain unsupported.
- Added a per-material **Receive Standard Lighting** option to the Virtual Light Lit shader so URP main/additional lights, shadows, baked lighting, reflection probes, ambient lighting, and SSAO can be included or excluded independently of Virtual Lights and emission.
- Added an Editor command that converts deduplicated URP Lit materials used by active or inactive Renderers in all loaded scenes while preserving compatible textures, texture transforms, parameters, keywords, render queue, instancing, and GI flags with Undo support.
- Added Editor commands for replacing selected or current-stage Unity Directional, Point, Spot, and Rectangle `Light` components with parameter-preserving `VirtualLight` components, including dependency-safe skipping and grouped Undo/Redo.
- Expanded the Virtual Light Lit receiver to use URP Lit-compatible metallic/specular, normal, height, occlusion, emission, detail, alpha clipping, transparency, culling, lightmapping, instancing, and auxiliary-pass inputs.
- Made converted Specular Highlights and Receive Shadows settings apply to Virtual Light contribution as well as URP light contribution.
- Added explicit zero-light buffer bindings for Direct3D 12 so Virtual Light receiver and beam shaders remain drawable before any light or shadow is registered.
- Allowed compute-shader integrations to opt out of Virtual Light shadow resources while preserving shadow support for regular mesh receivers and beam volumes.
- Removed the Basic sample's implicit UGUI dependency by replacing its Canvas overlay with a package-local immediate-mode overlay.
- Exposed the existing Affect Opaque setting in the Virtual Light Inspector.
- Added a minimal Unity 6000.0.79f1 validation project with UniCli Server 1.6.0 and stable URP project settings.
- Split samples into a script-free package Basic scene and repository-only advanced Feature Lab and Arena examples with detailed guidance.
- Replaced fixed-size Spot impact markers with finite-aperture cone-plane footprints, including exact oblique ellipses, surface-normal offsets, grazing-angle rejection, a configurable aspect-ratio limit, and a 60 Hz automatic Physics refresh cap.
- Added Profiler markers, adaptive saturated-hit buffers, center-ray validation for SphereCast impacts, and avoidance of redundant beam, impact, activation, and occlusion-distance writes while retaining a non-allocating steady-state Physics path.
- Added a four-vertex analytic impact shader with inner/outer cone falloff and scene-depth surface clipping, replacing the Feature Lab's uniformly emissive Sphere proxy.
- Added a Spot surface penumbra control that preserves Inner/Outer cone boundaries while concentrating visible receiver highlights toward the Inner cone with a branch-coherent multiply-only profile.

## [0.1.0] - 2026-07-11

- Added the initial Unity 6 / URP package layout.
- Added runtime registration, update, removal, generated-light clearing, and quality APIs.
- Added Point, Spot, and four-level sampled Rectangle Area lights.
- Added dynamically sized structured GPU upload, 16x16 screen-tile culling, and direct structured-buffer evaluation when compute selection is unavailable, without a package-authored light-count ceiling.
- Added an opaque URP PBR receiver shader and reusable HLSL include using URP's metallic-roughness BRDF.
- Added normal, mask, ambient-occlusion, emission, and clear-coat receiver inputs.
- Added windowed inverse-square attenuation and centered Rectangle Area sample layouts.
- Added optional Spot first-hit Physics probing with classified Collider blockers, a visible beam proxy, and an impact marker.
- Replaced the rectangular beam proxy with a Spot-angle-matched raymarched cone using Beer-Lambert-style haze integration and depth-softened intersections.
- Added a second Arena sample scene with six moving heads and Fan, Cross, Converge, and Solo comparison phases.
- Made beam volumes non-occluding, changed Arena blockers to explicit category markers, and corrected SphereCast cutoff to use projected surface contact instead of sphere-center travel.
- Reworked beam integration around extinction, single-scattering albedo, source/view transmittance, normalized Henyey-Greenstein phase, additive radiance, and per-sample scene-depth fading.
- Added a controllable Gaussian core radius, optional hot-core desaturation, and a normalized Henyey-Greenstein/isotropic phase mixture for thicker HDR core-to-halo beams without expanding their geometric or shadow bounds.
- Removed disconnected source discs and triangular depth-edge slices by intersecting camera rays with a finite-aperture beam frustum, using stable stratified sampling, and replacing discontinuous cell noise with interpolated value noise.
- Stabilized thin-beam intersections by solving from the proxy-box entry, added correct orthographic camera rays, and aligned finite-aperture volume shadows through an equivalent virtual projection apex.
- Added a dynamically sized custom Spot shadow-map `Texture2DArray` with per-light slices shared by the opaque PBR receiver and participating-media beam; allocation failure leaves affected lights active but unshadowed.
- Fixed sample Volume Profile sub-asset persistence, widened the Spot core, shortened terminal fading, softened beam edges, aligned beam/surface hues, and moved the Arena camera closer to the receiver interaction.
- Added custom Inspector, Scene gizmos, handles, package tests, and a basic animated sample.
