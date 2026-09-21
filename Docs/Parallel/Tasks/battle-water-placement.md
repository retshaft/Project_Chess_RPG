# Battle water placement

- Status: Review (placement saved; Play verification blocked by existing OutgameRuleRegressionTests CS0246 errors)
- Owner: Codex main, codex/integration, original workspace
- Scope: Assets/Scenes/Test/BattleScene.unity; dedicated Assets/Resources/Materials/Water/ShallowWater_Battle.mat (+meta); Docs/Water.md; Docs/BattleWater.png.
- Preserve: current unsaved scene edits, battle code, board tiles/colliders, other scenes, shared reference materials.
- Work: reuse existing shallow-water mesh/shader/camera support, fit 16x16 battle board at shallow depth, ensure selection and battle rendering remain usable, Play and console verification.
- Necessary shader fix: Assets/Resources/Shader/Water/ShallowWater.shader, preserve water visibility when scene fog is disabled (URP ComputeFogIntensity returns zero in that case).
- Validation: camera renders with fog off/on; ShaderUtil messages 0; no water collider; downward ray reaches Plane/GrayboxBoardPlane. Fog restored off. Play remains off. Runtime interaction and Edit Mode suite not verified because of existing compilation errors.
