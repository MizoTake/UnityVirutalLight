# Arena beam presentation

`MizoTake/Virtual Light/Beam` renders a lightweight participating-media approximation for Spot lights. A unit Cube is used only as a tight raymarch bound. The shader intersects each camera ray with a finite-aperture circular beam frustum, then integrates only the interval that contains participating-media radiance. `Source Aperture Radius` is specified in world-space meters and models the non-zero output lens instead of collapsing the beam to a mathematical point.

When `Truncate Visual At First Hit` and `Fit Visual To Spot Cone` are both explicitly enabled, `VirtualLightBeamOcclusion` sets the bound length to the current visible distance and its end diameter to:

```text
diameter = 2 * visibleDistance * tan(outerAngle / 2)
```

The volume uses a homogeneous single-scattering approximation. Extinction Density defines sigma-t, Single Scattering Albedo derives sigma-s, and the raymarch applies Beer-Lambert transmittance along both the source-to-sample and sample-to-camera paths. Forward Scattering uses the normalized Henyey-Greenstein phase function, while Isotropic Scattering Fraction blends in a normalized `1 / (4 * pi)` lobe so a strongly forward-scattering medium remains readable from side views. Because this is a convex combination of normalized phase functions, the mixture does not create energy by changing the view angle.

Core Radius is the half-energy radius of a Gaussian-shaped high-intensity core inside the wider radial envelope. Core Strength changes that core's incident-light gain; Beam Plateau and Edge Softness control the lower-energy outer body. Hot Core Mix optionally desaturates only the accumulated core contribution toward the source peak channel, approximating camera or fixture highlight saturation without whitening the halo. Source Fade, End Fade, and these radial controls shape incident light rather than changing medium density. Haze Variation is the only control that perturbs medium density, and it uses interpolated value noise so cell boundaries do not appear as hard haze blocks.

Opaque scene depth is evaluated per raymarch sample. A surface therefore fades only nearby samples instead of multiplying the complete beam by a zero intersection term. Multiple beam proxies never attenuate or collide with one another: they use RGB-only `Blend One One`, so their in-scattered radiance is added independently.

## Quality

The default material uses 20 stratified raymarch steps. Enable `_BEAM_QUALITY_LOW` for 12 steps or `_BEAM_QUALITY_HIGH` for 32 steps. A stable screen-space jitter offsets the strata without temporal accumulation, preventing coherent sampling discs while avoiding motion trails on rapidly moving heads. The imported samples use the default 20-step mode; High is opt-in because every additional step also evaluates medium variation and custom shadow visibility. Source Intensity is an artistic exposure for the normalized phase mixture. Distance Falloff interpolates between a collimated presentation beam at zero and inverse-square falloff at two. Bloom remains a camera post effect: use HDR core radiance to seed it instead of expanding the geometric beam or its shadow frustum.

## Arena sample

In the repository host project, open `Assets/VirtualLightExamples/Advanced/Scenes/VirtualLightArenaSample.unity`. Six moving heads are grouped by Stage Left and Stage Right, while three invisible Rectangle Area Virtual Lights under `Lighting/House Virtual Fills` keep the PBR stage readable without a Unity light component. `VirtualLightArenaBeamController` divides its 24-second loop into four six-second show phases:

- Fan Sweep: separated targets expose the contribution from each light.
- Cross: mirrored heads cross their beams over the stage.
- Converge: all lights aim near the central PBR receivers with reduced per-light intensity.
- Solo Impact: one light at a time runs at full authored intensity.

The controller moves aim targets rather than directly oscillating Euler angles. Every light uses a different phase, then follows its target with damped Quaternion interpolation. This produces independent pan and tilt motion while keeping the Virtual Light direction, raymarch volume intensity, impact state, and PBR contribution synchronized. Each `VirtualLightBeamVolume` stores its authored reference intensity so Solo and reduced-output phases scale the visible beam and surface light together.

Arena blockers are opt-in. `VirtualLightOccluder` is attached to the Venue Shell, Stage, Stage PBR Targets, and Light Influence Deck categories. Opaque Renderers below those markers are submitted to the custom Spot shadow maps, and their Colliders are eligible for the optional Physics first-hit effect. Moving-head housings, scenic reference geometry, and beam volumes are not blockers. The status overlay shows total authored PBR output for the current comparison phase.

Each shadow-enabled moving head receives its own slice in a dynamically sized custom shadow-map `Texture2DArray`. When a Beam volume declares a finite source aperture, its shadow projection uses the equivalent virtual apex behind the physical lens; this makes the shadow frustum cover the complete visible beam while the original VirtualLight cone still controls PBR energy. The opaque PBR receiver and that light's beam raymarch sample the same slice, so surface and volume visibility agree without introducing a Unity `Light` component. Multiple beams do not shadow one another and their visible radiance is still accumulated additively. The package defines no fixed moving-head or shadow-slice count; practical capacity depends on platform texture-array limits, GPU memory, and frame-time cost. A shadow allocation failure keeps the corresponding lights active but unshadowed.

## Limits

The raymarch is a homogeneous single-scattering approximation, not a full solution of the radiative transfer equation. Custom Spot shadow maps provide projective visibility for registered opaque occluders, but do not guarantee alpha-clipped or transparent shadow casting. The optional central Raycast or SphereCast remains an impact and legacy visual-truncation aid, not the surface-lighting visibility model. Exact shared-medium extinction, multiple scattering, transparent transmission, and physically complete volumetric penumbra require a more comprehensive volumetric backend.
