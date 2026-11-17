# VRChat Physical C++ Compiler MVP (`TinyC++` UdonSharp)

A tangible, 3D visual C++ compiler and stack-based Virtual Machine operating entirely inside a VRChat World using **UdonSharp**, **Unity Physics**, and **`VRCObjectPool`**. Players construct an Abstract Syntax Tree (AST) by physically snapping 3D polyhedron tokens together on a workbench, compile the physical tree into flat bytecode via an iterative traversal engine, and step through execution on a 3D Runtime Mainframe.

---

## 1. Repository & Codespace Structure

When spinning this up in a GitHub Codespace for editing or integrating with a Unity VRChat Creator Companion (VCC) project, organize your scripts under `Assets/UdonCompilerMVP/Scripts/`:

```text
├── Assets/
│   └── UdonCompilerMVP/
│       ├── Prefabs/
│       │   └── CompilerNode_Base.prefab
│       └── Scripts/
│           ├── CompilerNode.cs
│           ├── SocketTrigger.cs
│           └── UdonCompilerAndVM.cs
└── README.md
```

> **Note for GitHub Codespaces:** While Unity requires a local GUI editor to bake scenes and upload to VRChat, you can configure your Codespace with the `.NET 8.0 SDK` and C# Dev Kit to lint, refactor, and run unit tests against the compiler's bytecode generation and VM execution logic headlessly.

---

## 2. MVP Feature Scope ("TinyC++" Subset)

Instead of parsing raw character strings on day one, the MVP operates at the **Token/AST Block level**. Players pull pre-tokenized 3D blocks from dispensers and snap them together on a vertical workbench.

* **Data Types:** 32-bit signed integers (`int`).
* **Variables:** 4 hard-coded global registers (`Var_A`, `Var_B`, `Var_C`, `Var_D`) represented as physical glass canisters on the wall.
* **Operations:**
  * Statement Sequencing (`;`)
  * Assignment (`=`)
  * Addition (`+`) & Subtraction (`-`)
  * Greater-Than Comparison (`>`)
  * Control Flow (`while` loops)

---

## 3. Scene Hierarchy & Block Specifications

### Unity Scene Hierarchy
```text
VRChat_Compiler_MVP (Scene Root)
├── 01_BlockPool_Manager (VRCObjectPool + Spawner Buttons)
├── 02_AST_Workbench (Root Snap Socket + Compile Lever)
├── 03_Compiler_Core (UdonCompilerAndVM.cs)
└── 04_Runtime_VM (4 Register Tubes + Instruction Tape + Step/Run Controls)
```

### Physical Block Prefab (`CompilerNode`)
Every grabbable C++ token shares a base prefab template containing:
* `VRC_Pickup` and `VRCObjectSync` (for multiplayer collaborative coding).
* `Rigidbody` set to `isKinematic = true` and `useGravity = false`.
* **1 Parent Snap Plug** (on the top/left face) with a small `SphereCollider` (`isTrigger = true`) on a dedicated Unity layer (e.g., `UserLayer22: AST_Plug`).
* **0 to 2 Child Snap Sockets** (on the bottom/right faces) with `SocketTrigger.cs` attached.

| Block Type (`nodeType`) | Visual Shape | Child Sockets | Purpose |
| :--- | :--- | :--- | :--- |
| **`0` : `SEQ` (`;`)** | Grey I-Beam | `Slot_0` (Statement), `Slot_1` (Next `;`) | Chains lines of code vertically |
| **`1` : `ASSIGN` (`=`)** | Blue Cube | `Slot_0` (Target Var), `Slot_1` (Expr) | Stores right-side value into left-side variable |
| **`2` : `ADD` (`+`)** | Red Pyramid | `Slot_0` (Left Expr), `Slot_1` (Right Expr) | Evaluates addition |
| **`3` : `SUB` (`-`)** | Orange Pyramid | `Slot_0` (Left Expr), `Slot_1` (Right Expr) | Evaluates subtraction |
| **`4` : `GT` (`>`)** | Purple Diamond | `Slot_0` (Left Expr), `Slot_1` (Right Expr) | Pushes `1` if Left > Right, else `0` |
| **`5` : `WHILE` (`while`)** | Yellow Arch | `Slot_0` (Condition `>`), `Slot_1` (Body `;`) | Loops body while `Slot_0` evaluates to non-zero |
| **`6` : `VAR` (`A`–`D`)** | Green Cylinder | None (Leaf Node) | Reads or targets a RAM register (`0`–`3`) |
| **`7` : `CONST` (`0`–`9`)** | White Sphere | None (Leaf Node) | Holds a literal integer (cycled via `Interact()`) |

---

## 4. Core UdonSharp Scripts

### 4.1 `CompilerNode.cs`
Attach this script to the root of every 3D block prefab. It manages AST references, pickup/drop alignment, and network ownership transfers.

```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class CompilerNode : UdonSharpBehaviour
{
    [Header("Node Identity")]
    [UdonSynced] public int nodeType; // 0=SEQ, 1=ASSIGN, 2=ADD, 3=SUB, 4=GT, 5=WHILE, 6=VAR, 7=CONST
    [UdonSynced] public int valueOrVarIndex; // For CONST: literal value. For VAR: 0=A, 1=B, 2=C, 3=D

    [Header("AST Connections")]
    public CompilerNode parentNode;
    public CompilerNode leftChild;  // Slot_0
    public CompilerNode rightChild; // Slot_1
    public Transform leftSocketTransform;
    public Transform rightSocketTransform;

    [Header("Visuals")]
    public TextMeshProUGUI labelText;

    public override void OnPickup()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        // Detach from parent AST node when physically grabbed
        if (parentNode != null)
        {
            parentNode.DetachChild(this);
            parentNode = null;
        }
    }

    public override void OnPickupUseDown()
    {
        // Allow players to cycle literal values or variable indices while holding the block
        if (nodeType == 7) // CONST
        {
            valueOrVarIndex = (valueOrVarIndex + 1) % 10;
            UpdateVisualLabel();
            RequestSerialization();
        }
        else if (nodeType == 6) // VAR
        {
            valueOrVarIndex = (valueOrVarIndex + 1) % 4;
            UpdateVisualLabel();
            RequestSerialization();
        }
    }

    public override void OnDrop()
    {
        // If snapped to a parent, lock precisely to socket coordinates on drop
        if (parentNode != null)
        {
            Transform targetSocket = (parentNode.leftChild == this)
                ? parentNode.leftSocketTransform
                : parentNode.rightSocketTransform;
            transform.SetPositionAndRotation(targetSocket.position, targetSocket.rotation);
        }
    }

    public void AttachChild(CompilerNode child, int slotIndex)
    {
        if (child == this || child.parentNode != null) return;

        if (slotIndex == 0 && leftChild == null)
        {
            leftChild = child;
            child.parentNode = this;
            child.transform.SetPositionAndRotation(leftSocketTransform.position, leftSocketTransform.rotation);
        }
        else if (slotIndex == 1 && rightChild == null)
        {
            rightChild = child;
            child.parentNode = this;
            child.transform.SetPositionAndRotation(rightSocketTransform.position, rightSocketTransform.rotation);
        }
    }

    public void DetachChild(CompilerNode child)
    {
        if (leftChild == child) leftChild = null;
        if (rightChild == child) rightChild = null;
    }

    public void UpdateVisualLabel()
    {
        if (labelText == null) return;
        if (nodeType == 7) labelText.text = valueOrVarIndex.ToString();
        if (nodeType == 6)
        {
            string[] varNames = new string[] { "Var_A", "Var_B", "Var_C", "Var_D" };
            labelText.text = varNames[valueOrVarIndex];
        }
    }

    public override void OnDeserialization()
    {
        UpdateVisualLabel();
    }
}
```

---

### 4.2 `SocketTrigger.cs`
Attach this script to the child `leftSocketTransform` (`slotIndex = 0`) and `rightSocketTransform` (`slotIndex = 1`) GameObjects on each block prefab.

```csharp
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class SocketTrigger : UdonSharpBehaviour
{
    public CompilerNode ownerNode;
    public int slotIndex; // 0 for Left/Top, 1 for Right/Bottom
    public AudioSource snapAudio;

    private void OnTriggerEnter(Collider other)
    {
        if (ownerNode == null) return;

        // Check if the entering collider belongs to a CompilerNode
        CompilerNode incomingNode = other.GetComponentInParent<CompilerNode>();
        if (incomingNode != null && incomingNode != ownerNode && incomingNode.parentNode == null)
        {
            // Force drop if the player is currently holding it for crisp magnetic snapping
            VRC_Pickup pickup = (VRC_Pickup)incomingNode.GetComponent(typeof(VRC_Pickup));
            if (pickup != null && pickup.IsHeld)
            {
                pickup.Drop();
            }

            ownerNode.AttachChild(incomingNode, slotIndex);

            if (snapAudio != null)
            {
                snapAudio.Play();
            }
        }
    }
}
```

---

### 4.3 `UdonCompilerAndVM.cs`
Attach this script to the Mainframe GameObject. Because UdonSharp does not support recursion or dynamic `List<T>` allocations, this script uses **fixed-size flat arrays** and an **explicit state stack** to traverse the 3D AST and emit bytecode.

```csharp
using UdonSharp;
using UnityEngine;
using TMPro;

public class UdonCompilerAndVM : UdonSharpBehaviour
{
    [Header("Physical References")]
    public CompilerNode workbenchRootSocket; // Dummy root node holding the first block in leftChild
    public TextMeshProUGUI consoleOutput;
    public TextMeshProUGUI[] registerDisplays; // 4 UI texts on the RAM tubes

    // Opcodes for the Virtual Machine
    private const int OP_PUSH_CONST = 10;
    private const int OP_LOAD_VAR   = 11;
    private const int OP_STORE_VAR  = 12;
    private const int OP_ADD        = 13;
    private const int OP_SUB        = 14;
    private const int OP_GT         = 15;
    private const int OP_JZ         = 16; // Jump if Zero
    private const int OP_JMP        = 17; // Unconditional Jump
    private const int OP_HALT       = 99;

    // Flat VM Memory (No dynamic allocations in Udon)
    private int[] bytecode = new int[512];
    private int bytecodePtr = 0;

    private int[] vmStack = new int[64];
    private int sp = 0; // Stack Pointer
    private int ip = 0; // Instruction Pointer
    private int[] registers = new int[4]; // Var_A, Var_B, Var_C, Var_D
    private bool isRunning = false;

    // Iterative traversal stacks
    private CompilerNode[] workNodeStack = new CompilerNode[128];
    private int[] workStateStack = new int[128];
    private int[] workPatchStack = new int[128];

    public void CompilePhysicalTree()
    {
        bytecodePtr = 0;
        isRunning = false;

        if (workbenchRootSocket == null || workbenchRootSocket.leftChild == null)
        {
            consoleOutput.text = "COMPILE ERROR: Workbench root socket is empty!";
            return;
        }

        // Iterative Post-Order / Custom AST Compilation
        int top = 0;
        workNodeStack[top] = workbenchRootSocket.leftChild;
        workStateStack[top] = 0;

        while (top >= 0)
        {
            CompilerNode node = workNodeStack[top];
            int state = workStateStack[top];

            if (node == null) { top--; continue; }

            if (node.nodeType == 7) // CONST
            {
                Emit(OP_PUSH_CONST);
                Emit(node.valueOrVarIndex);
                top--;
            }
            else if (node.nodeType == 6) // VAR (Read)
            {
                Emit(OP_LOAD_VAR);
                Emit(node.valueOrVarIndex);
                top--;
            }
            else if (node.nodeType == 1) // ASSIGN (=)
            {
                if (state == 0)
                {
                    if (node.leftChild == null || node.leftChild.nodeType != 6 || node.rightChild == null)
                    {
                        consoleOutput.text = "SYNTAX ERROR: Invalid '=' block connections!";
                        return;
                    }
                    workStateStack[top] = 1;
                    top++;
                    workNodeStack[top] = node.rightChild;
                    workStateStack[top] = 0;
                }
                else
                {
                    Emit(OP_STORE_VAR);
                    Emit(node.leftChild.valueOrVarIndex);
                    top--;
                }
            }
            else if (node.nodeType >= 2 && node.nodeType <= 4) // ADD, SUB, GT
            {
                if (state == 0)
                {
                    if (node.leftChild == null || node.rightChild == null)
                    {
                        consoleOutput.text = "SYNTAX ERROR: Binary operator missing operand!";
                        return;
                    }
                    workStateStack[top] = 1;
                    top++;
                    workNodeStack[top] = node.leftChild;
                    workStateStack[top] = 0;
                }
                else if (state == 1)
                {
                    workStateStack[top] = 2;
                    top++;
                    workNodeStack[top] = node.rightChild;
                    workStateStack[top] = 0;
                }
                else
                {
                    if (node.nodeType == 2) Emit(OP_ADD);
                    if (node.nodeType == 3) Emit(OP_SUB);
                    if (node.nodeType == 4) Emit(OP_GT);
                    top--;
                }
            }
            else if (node.nodeType == 0) // SEQ (;)
            {
                if (state == 0)
                {
                    workStateStack[top] = 1;
                    top++;
                    workNodeStack[top] = node.leftChild;
                    workStateStack[top] = 0;
                }
                else if (state == 1)
                {
                    workStateStack[top] = 2;
                    top++;
                    workNodeStack[top] = node.rightChild;
                    workStateStack[top] = 0;
                }
                else { top--; }
            }
            else if (node.nodeType == 5) // WHILE
            {
                if (state == 0)
                {
                    if (node.leftChild == null || node.rightChild == null)
                    {
                        consoleOutput.text = "SYNTAX ERROR: WHILE block missing condition or body!";
                        return;
                    }
                    workPatchStack[top] = bytecodePtr; // Save loop start address
                    workStateStack[top] = 1;
                    top++;
                    workNodeStack[top] = node.leftChild; // Condition
                    workStateStack[top] = 0;
                }
                else if (state == 1)
                {
                    Emit(OP_JZ);
                    int jumpOutPatchAddr = bytecodePtr;
                    Emit(0); // Placeholder for jump exit
                    workPatchStack[top] = (workPatchStack[top] << 16) | jumpOutPatchAddr;
                    workStateStack[top] = 2;
                    top++;
                    workNodeStack[top] = node.rightChild; // Loop Body
                    workStateStack[top] = 0;
                }
                else
                {
                    int loopStart = (workPatchStack[top] >> 16) & 0xFFFF;
                    int jumpOutPatchAddr = workPatchStack[top] & 0xFFFF;
                    Emit(OP_JMP);
                    Emit(loopStart);
                    bytecode[jumpOutPatchAddr] = bytecodePtr; // Backpatch exit jump
                    top--;
                }
            }
        }

        Emit(OP_HALT);
        consoleOutput.text = "COMPILED SUCCESS: " + bytecodePtr + " words emitted.";
        ip = 0;
        sp = 0;
    }

    private void Emit(int val)
    {
        bytecode[bytecodePtr++] = val;
    }

    public void StepVM()
    {
        if (ip >= bytecodePtr) return;
        int op = bytecode[ip++];

        switch (op)
        {
            case OP_PUSH_CONST:
                vmStack[sp++] = bytecode[ip++];
                break;
            case OP_LOAD_VAR:
                vmStack[sp++] = registers[bytecode[ip++]];
                break;
            case OP_STORE_VAR:
                registers[bytecode[ip++]] = vmStack[--sp];
                UpdateRAMVisuals();
                break;
            case OP_ADD:
                sp--;
                vmStack[sp - 1] = vmStack[sp - 1] + vmStack[sp];
                break;
            case OP_SUB:
                sp--;
                vmStack[sp - 1] = vmStack[sp - 1] - vmStack[sp];
                break;
            case OP_GT:
                sp--;
                vmStack[sp - 1] = (vmStack[sp - 1] > vmStack[sp]) ? 1 : 0;
                break;
            case OP_JZ:
                int target = bytecode[ip++];
                if (vmStack[--sp] == 0) ip = target;
                break;
            case OP_JMP:
                ip = bytecode[ip];
                break;
            case OP_HALT:
                isRunning = false;
                consoleOutput.text = "PROGRAM HALTED.";
                break;
        }
    }

    private void UpdateRAMVisuals()
    {
        for (int i = 0; i < 4; i++)
        {
            if (registerDisplays[i] != null)
                registerDisplays[i].text = registers[i].ToString();
        }
    }
}
```

---

## 5. First Test Program in VR

Once loaded into your scene, test the compiler and VM by physically snapping together this countdown program:

```cpp
Var_A = 3;
while (Var_A > 0) {
    Var_A = Var_A - 1;
}
```

1. Snap a **`SEQ` (`;`)** block into the Workbench Root socket.
2. Into `SEQ.Slot_0`, snap an **`ASSIGN` (`=`)** block with `Var_A` on the left and `CONST (3)` on the right.
3. Into `SEQ.Slot_1`, snap a **`WHILE`** block:
   * **Condition (`Slot_0`):** Snap a **`GT` (`>`)** block comparing `Var_A` and `CONST (0)`.
   * **Body (`Slot_1`):** Snap an **`ASSIGN` (`=`)** block setting `Var_A` to `SUB (-)` of `Var_A` and `CONST (1)`.
4. Pull the **Compile** lever (`CompilePhysicalTree()`), then press **Step** (`StepVM()`) repeatedly to watch `Var_A` count down from `3` to `0` on the RAM register tube.