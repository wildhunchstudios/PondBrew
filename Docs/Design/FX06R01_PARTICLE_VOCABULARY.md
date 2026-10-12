# FX-06R-01 particle vocabulary contract

Source/build: `54126ecc84488b10f88f7b200c50280f5e759878` / `Windows-6000.3.21f1-fx06r01-vocabulary-2`. Original local C#/HLSL production; no generation service, external assets, new packages or asset imports. This is a foundation for R-02â€“06, not those finished actions.

| Resource | Standard | Low | Ownership |
|---|---:|---:|---|
| Prewarmed mesh-renderer slots |96|96|Existing world pool; inactive slots remain allocated.|
| Concurrent action / ambient motes |64 /32|24 /8|Separate partitions; neither ambient particle count nor emitter pressure can steal action admission.|
| Action / ambient logical emitters |8 /4|4 /2|Distinct owner+source within a partition; total12 /6. No deferred emission queue.|
| Owned particle meshes |5|5|Bubble and ring192 triangles each; droplet, puff and spark96 each;672 total triangles /451 vertices across vocabulary. Borrowed original mesh remains separate.|
| Owned particle material / shader |1 /1|1 /1|Shared URP unlit alpha blend; original vertex-alpha masks. No textures, lights, global bloom or VFX Graph.|
| Attributable FX renderers / transforms |112 /120|112 /120|No topology increase from FX-05. Existing ceilings112 /180 retained.|

`FxParticleRequest` selects a shape, color, source-local motion and size/fade. Drift uses velocity; curves use a quadratic bend; spirals wrap the curve with a tapered local radius. Optional destination follows a moving endpoint. Camera billboarding uses the existing explicitly cached player camera; spin is optional. Endpoints disappearing/disabled and invalid/stale owner tokens cancel the mote. No model, save, economy, stock, audio or camera callback is carried by a request.

Finite bounds: lifetime greater than0 and at most1.5s; diameter greater than0 and at most0.30m, ending size0â€“0.30m; endpoint displacement at most3m, bend at most1m, velocity at most2m/s, spiral radius0â€“0.30m, turnsÂ±4 and spinÂ±720degrees/s. Normalized color channels0â€“1, nonzero input alpha; malformed input is rejected before claiming a slot. The caller must keep paths inside its actual vessel/source/destination contract. These validation bounds do not authorize every maximum at once or prove visibility through geometry.

Fade enters over the first6% of life, stays readable to35%, and smooths out by expiration. Low reduces alpha to80% and particle/emitter allowances. Existing diagnostic reduced motion cancels active paths, converts rich particles to stationary source-local shapes at65% size, removes velocity/travel/radius/turns/spin/expansion, and caps lifetime at0.6s. Diagnostic switches remain nonpersistent; saved comfort options still belong to FX-20.

No GameObject, renderer, mesh, material, property block, collider or AudioSource is created for each emission. Ninety-six property blocks are warmed once. Reset clears owner, source/destination, motion, color/property block, lifetime and transform, restores the borrowed mesh, and detaches the material. Dispose releases the five owned meshes/material and owned pool objects; borrowed palette/mesh and parent survive. Legacy `Emit`/`EmitTravel` stay compatible with existing lab/hand/tank callers.

Raster budget for the measured operator/aisle/overview fixtures: conservative average full-screen particle layers below0.15 and peak overlap at most32, with nonzero visible sample pixels. The actual additive float-target audit rasterizes only particles with alpha above0.01; it deliberately excludes opaque/HUD occlusion. It measures submitted transparent particle coverage/overlap, not total URP scene overdraw or a vendor GPU counter. The heat map aligns with exported screen pixels. The stress fixture uses96/32 mixed shapes with0.10â€“0.17m starting diameters and at most1.1Ã— expansion; larger/closer/different bursts need new view/cost evidence. The budget is not universal over arbitrary camera distances or all parameter maxima.

Real-camera/HUD captures and repeated fixed-camera eight-second off/Standard/Low/off windows are in `Docs/Evidence/fx-06r-01`. Simulation freezes only for the explicitly marked ValidationMode vocabulary fixture; actual HUD and focus queries remain alive. Phase images are manually advanced diagnostic samples, not native-frame-rate motion or gameplay actions. The caption appears only in that fixture. Normal gameplay keeps current hatch/dose/collection behavior until the corresponding assignments.

Follow-on integration must use the existing world owner, post-commit cue and real accepted operation; it must not call this API for speculative success. Pause/focus/modal/arrange, moved/removed identity, disable, reset and reload use existing cancellation paths. Missing rich shader/material must fail locally without preventing legacy presentation or gameplay. R-02 starts with real first/repeat Pondlet hatch; R-03 covers world and modal dosing; R-04 growth; R-05 collection; R-06 aquarium; R-07 integrated cost; R-08 human acceptance.
