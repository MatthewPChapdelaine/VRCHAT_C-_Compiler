# CompilerNode prefab

Create `CompilerNode_Base.prefab` in Unity from the following components:

- `CompilerNode` on the root
- `VRC_Pickup` on the root
- `VRCObjectSync` on the root
- `Rigidbody` with `isKinematic = true` and `useGravity = false`
- A dedicated trigger collider on the parent snap plug
- One or two child socket transforms with `SocketTrigger`
- A `TextMeshProUGUI` label where block names and values are displayed

Configure the root script's `nodeType` and `valueOrVarIndex` for each derived block
prefab. `SocketTrigger.ownerNode` should point back to the block root.
