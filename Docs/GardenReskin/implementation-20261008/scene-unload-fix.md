Scene-unload regression found during live UI acceptance

The first five-page Core run rendered every page, but failed its error gate while
loading the next fixture: OriginalGameScene.OnDestroy called Level.Clear, whose
pooled visuals tried to SetParent to the same GameScene being destroyed.

The runtime repair explicitly begins pool shutdown before scene cleanup. Returning
an existing visual still removes its rented record and deactivates it; during
shutdown it stays in the hierarchy Unity is destroying instead of being reparented
or put back into the reusable stack. Renting from a shutting-down pool is rejected.
Normal in-scene reuse and restart remain unchanged.

The same path runs in Editor and Android/iOS. There is no Editor condition, error
filter, or special pre-clear in the acceptance driver. OctoberSkinValidation reloads
populated real scenes between pages and fails on any logged Error or Exception,
so a complete new run is the scene-unload regression test. The first failed log is
retained under validation/core-20261009-001554.log.

Changed runtime files: OriginalPrefabPool.cs and OriginalGameScene.cs only.
