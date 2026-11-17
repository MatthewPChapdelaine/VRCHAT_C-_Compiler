# VRChat Physical C++ Compiler MVP

This repository contains a Unity/UdonSharp implementation scaffold for **TinyC++**, a
physical AST compiler and stack VM for VRChat.

## Project layout

```text
Assets/
└── UdonCompilerMVP/
    ├── Prefabs/
    │   └── CompilerNode_Base.prefab
    └── Scripts/
        ├── CompilerNode.cs
        ├── SocketTrigger.cs
        └── UdonCompilerAndVM.cs
Tests/
└── FIRST_TEST_PROGRAM.md
vrchat_udon_c_compiler_mvp.md
```

## Scripts

- `CompilerNode.cs` represents a synced physical AST block. CONST and VAR blocks cycle
  their values with pickup use; grabbing a block detaches it from its parent.
- `SocketTrigger.cs` recognizes block colliders entering AST sockets and requests a
  magnetic snap.
- `UdonCompilerAndVM.cs` iteratively traverses the physical AST, emits flat bytecode,
  and exposes compile, step, run, and reset controls.

## Unity setup

1. Open a VRChat Unity project in the Unity version supported by your VCC/VPM
   toolchain.
2. Copy or add `Assets/UdonCompilerMVP` to the project's `Assets` folder.
3. Import UdonSharp, VRC SDK3, and TextMeshPro through VCC.
4. Create the hierarchy described in `vrchat_udon_c_compiler_mvp.md`.
5. Add `CompilerNode` to the block root prefab and configure each block's node type.
6. Add `SocketTrigger` to socket objects and assign `ownerNode`, `slotIndex`, and optional
   snap audio.
7. Add `UdonCompilerAndVM` to the mainframe and assign the workbench root, console text,
   and four RAM displays.

The C# files are designed for UdonSharp's fixed-array restrictions. The VM intentionally
does not use recursion or managed AST/list allocations.

## Supported block types

| Type | Opcode behavior |
| --- | --- |
| SEQ | Sequence the left statement, then the right statement |
| ASSIGN | Store an expression result in Var_A through Var_D |
| ADD / SUB / GT | Binary integer expression operations |
| WHILE | Execute a body while a condition is non-zero |
| VAR | Read a register |
| CONST | Push a literal integer |

See [the countdown test](Tests/FIRST_TEST_PROGRAM.md) for
the first end-to-end program.
