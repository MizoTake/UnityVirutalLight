# Spot beam occlusion

`VirtualLightBeamOcclusion` provides a deterministic first-hit Physics probe for Spot virtual lights. It performs a non-allocating Raycast or SphereCast along the Spot forward axis and can drive an impact Transform or optional legacy beam-volume truncation at the nearest accepted Collider.

The scalar Physics hit distance is not used to cut opaque PBR lighting with a plane. Shape-aware visibility comes from the package's custom Spot shadow maps instead.

When Cast Shadow is enabled, each eligible Spot receives a slice in a dynamically sized shadow-map `Texture2DArray`. Opaque Renderers beneath registered `VirtualLightOccluder` hierarchies are rendered into that light's slice. Both `Mizot/Virtual Light/Lit` and `Mizot/Virtual Light/Beam` sample the same visibility, so blockers affect only the associated light and overlapping beam colors continue to add. If GPU shadow resources cannot be allocated, the light remains active and is evaluated unshadowed.

## Setup

1. Add `VirtualLight` and set Type to Spot.
2. Enable Cast Shadow when custom surface and beam-volume shadows are required.
3. Add `VirtualLightBeamOcclusion` to the same GameObject when a Physics-driven impact or legacy beam truncation is required.
4. Add `VirtualLightOccluder` to blocker hierarchies. Opaque descendant Renderers can cast custom shadows, while descendant Colliders are candidates for the Physics probe.
5. Optionally assign a unit Cube using `Mizot/Virtual Light/Beam` to Beam Visual and an impact Transform to Impact Visual.

When optional first-hit truncation is active, Beam Visual is positioned halfway between the light and the visible endpoint and aligned with the Spot Transform. With `Fit Visual To Spot Cone` enabled, local Z matches the visible distance while local X and Y match the Spot cone's end diameter. Without truncation, the raymarch bound remains at the authored Spot range and per-sample shadow visibility shapes the beam. See [Arena beam presentation](arena-beams.md) for its scattering controls and quality levels.

With `Fit Impact To Spot Cone` enabled, the assigned impact Transform uses local X for the major diameter, local Y for the minor diameter, and keeps its authored local Z thickness. A unit Quad using `Mizot/Virtual Light/Impact Footprint` is the preferred representation. The shader uses one analytic radial profile, receives the Spot inner/outer cone ratio through a `MaterialPropertyBlock`, and samples scene depth once to clip the proxy away from geometry that is not on the hit plane. The calculation intersects the finite-aperture beam frustum with the Collider contact plane, so a perpendicular receiver produces a circle while an oblique receiver produces an exact shifted ellipse. The position offset is applied along the surface normal instead of the beam axis. If the plane produces an unbounded parabola/hyperbola, or the finite ellipse exceeds `Maximum Impact Aspect Ratio`, the impact is hidden instead of creating an unstable, screen-filling proxy.

`Probe Radius` changes the central ray into a SphereCast so thin or fast-moving blockers remain easier to detect. The default is zero because a thick physics sweep is not an optical beam. When SphereCast is enabled, cutoff distance is derived from the surface contact projected onto the light axis instead of the swept sphere-center distance, preventing the radius from shortening the beam early. A fitted optical footprint is shown only when a follow-up center Raycast also reaches an accepted surface; an off-axis SphereCast contact does not invent a floating ellipse. `Surface Offset` adds a small numerical gap at the surface.

Beam Visual automatically receives `VirtualLightBeamVolume`; Colliders accidentally placed inside any marked beam volume are always ignored. For production scenes, keep Require Occluder Marker enabled and combine `VirtualLightOccluder` classification with a dedicated Layer Mask. This prevents fixtures, choreography helpers, and unrelated Collider hierarchies from becoming accidental blockers.

`Maximum Refresh Rate` caps automatic Play Mode Physics probes at 60 Hz by default and uses `Time.unscaledTime`; set it to zero only when a probe is required on every rendered frame. `RefreshNow()` always performs an immediate probe. The implementation reuses its hit buffer, emits no managed allocation in the steady-state query path, and skips unchanged Transform, activation, and occlusion-distance writes. A full 32-hit buffer grows geometrically up to 256 and repeats the query; `HitBufferSaturated` reports when that safety ceiling is still insufficient instead of silently treating a truncated query as complete. Profiler markers are available under `VirtualLight.BeamOcclusion.Refresh`, `.PhysicsQuery`, and `.UpdateVisuals`.

## Scope

The Physics probe examines only the configured central Raycast or SphereCast and therefore cannot describe an off-axis silhouette. It is intended for impacts and intentionally hard-stopped laser or scanner effects. It does not provide transparent transmission or replace the per-sample custom shadow lookup.

The custom shadow path currently targets registered opaque Renderers for Spot lights. Alpha-clipped and transparent casting are not guaranteed, and Point shadows, Rectangle Area shadows, multiple scattering, and physically complete volumetric penumbra are outside the current scope. The shadow texture and GPU metadata grow with demand instead of enforcing a package-authored count, but platform resource limits and frame-time remain practical constraints.
