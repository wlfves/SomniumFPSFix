# Changelog

All notable changes to this project are documented here.

## 1.2.0

- Added an on-screen "Somnium FPS Fix active" label to the title screen so you
  can confirm at a glance that the mod loaded.
- Switched to applying the fix entirely in memory; no game files are written to
  disk and none are shipped with the mod.
- First public release: README and license.

## 1.1.0

- Behaviour-matching fix: restores the original standing-still and moving
  values so the Somnium feels identical to the unmodified game at any frame rate.
- Added the version-size guard and the in-memory safety-net fallback.

## 1.0.0

- Initial working fix for the high-FPS slow-motion runaway crash during Psyncs.
