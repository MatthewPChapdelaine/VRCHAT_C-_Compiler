using TMPro;
using UdonSharp;
using UnityEngine;

public class UdonCompilerAndVM : UdonSharpBehaviour
{
    [Header("Physical References")]
    public CompilerNode workbenchRootSocket;
    public TextMeshProUGUI consoleOutput;
    public TextMeshProUGUI[] registerDisplays;

    private const int OP_PUSH_CONST = 10;
    private const int OP_LOAD_VAR = 11;
    private const int OP_STORE_VAR = 12;
    private const int OP_ADD = 13;
    private const int OP_SUB = 14;
    private const int OP_GT = 15;
    private const int OP_JZ = 16;
    private const int OP_JMP = 17;
    private const int OP_HALT = 99;

    private int[] bytecode = new int[512];
    private int bytecodePtr = 0;

    private int[] vmStack = new int[64];
    private int sp = 0;
    private int ip = 0;
    private int[] registers = new int[4];
    private bool isRunning = false;

    private CompilerNode[] workNodeStack = new CompilerNode[128];
    private int[] workStateStack = new int[128];
    private int[] workPatchStack = new int[128];

    public void CompilePhysicalTree()
    {
        bytecodePtr = 0;
        ip = 0;
        sp = 0;
        isRunning = false;

        for (int i = 0; i < registers.Length; i++)
        {
            registers[i] = 0;
        }
        UpdateRAMVisuals();

        if (workbenchRootSocket == null || workbenchRootSocket.leftChild == null)
        {
            SetConsole("COMPILE ERROR: Workbench root socket is empty!");
            return;
        }

        int top = 0;
        workNodeStack[top] = workbenchRootSocket.leftChild;
        workStateStack[top] = 0;
        workPatchStack[top] = 0;

        while (top >= 0)
        {
            CompilerNode node = workNodeStack[top];
            int state = workStateStack[top];

            if (node == null)
            {
                top--;
                continue;
            }

            if (node.nodeType == 7)
            {
                if (!Emit(OP_PUSH_CONST) || !Emit(node.valueOrVarIndex))
                {
                    return;
                }
                top--;
            }
            else if (node.nodeType == 6)
            {
                if (node.valueOrVarIndex < 0 || node.valueOrVarIndex > 3)
                {
                    SetConsole("SYNTAX ERROR: Invalid variable index!");
                    return;
                }
                if (!Emit(OP_LOAD_VAR) || !Emit(node.valueOrVarIndex))
                {
                    return;
                }
                top--;
            }
            else if (node.nodeType == 1)
            {
                if (state == 0)
                {
                    if (node.leftChild == null || node.leftChild.nodeType != 6 || node.rightChild == null)
                    {
                        SetConsole("SYNTAX ERROR: Invalid '=' block connections!");
                        return;
                    }
                    workStateStack[top] = 1;
                    if (!PushNode(ref top, node.rightChild))
                    {
                        return;
                    }
                }
                else
                {
                    if (!Emit(OP_STORE_VAR) || !Emit(node.leftChild.valueOrVarIndex))
                    {
                        return;
                    }
                    top--;
                }
            }
            else if (node.nodeType >= 2 && node.nodeType <= 4)
            {
                if (state == 0)
                {
                    if (node.leftChild == null || node.rightChild == null)
                    {
                        SetConsole("SYNTAX ERROR: Binary operator missing operand!");
                        return;
                    }
                    workStateStack[top] = 1;
                    if (!PushNode(ref top, node.leftChild))
                    {
                        return;
                    }
                }
                else if (state == 1)
                {
                    workStateStack[top] = 2;
                    if (!PushNode(ref top, node.rightChild))
                    {
                        return;
                    }
                }
                else
                {
                    if (node.nodeType == 2 && !Emit(OP_ADD)) return;
                    if (node.nodeType == 3 && !Emit(OP_SUB)) return;
                    if (node.nodeType == 4 && !Emit(OP_GT)) return;
                    top--;
                }
            }
            else if (node.nodeType == 0)
            {
                if (node.leftChild == null)
                {
                    SetConsole("SYNTAX ERROR: SEQ block missing its statement!");
                    return;
                }

                if (state == 0)
                {
                    workStateStack[top] = 1;
                    if (!PushNode(ref top, node.leftChild))
                    {
                        return;
                    }
                }
                else if (state == 1)
                {
                    if (node.rightChild == null)
                    {
                        top--;
                    }
                    else
                    {
                        workStateStack[top] = 2;
                        if (!PushNode(ref top, node.rightChild))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    top--;
                }
            }
            else if (node.nodeType == 5)
            {
                if (state == 0)
                {
                    if (node.leftChild == null || node.rightChild == null)
                    {
                        SetConsole("SYNTAX ERROR: WHILE block missing condition or body!");
                        return;
                    }
                    workPatchStack[top] = bytecodePtr;
                    workStateStack[top] = 1;
                    if (!PushNode(ref top, node.leftChild))
                    {
                        return;
                    }
                }
                else if (state == 1)
                {
                    if (!Emit(OP_JZ))
                    {
                        return;
                    }
                    int jumpOutPatchAddr = bytecodePtr;
                    if (!Emit(0))
                    {
                        return;
                    }
                    workPatchStack[top] = (workPatchStack[top] << 16) | jumpOutPatchAddr;
                    workStateStack[top] = 2;
                    if (!PushNode(ref top, node.rightChild))
                    {
                        return;
                    }
                }
                else
                {
                    int loopStart = (workPatchStack[top] >> 16) & 0xFFFF;
                    int jumpOutPatchAddr = workPatchStack[top] & 0xFFFF;
                    if (!Emit(OP_JMP) || !Emit(loopStart))
                    {
                        return;
                    }
                    bytecode[jumpOutPatchAddr] = bytecodePtr;
                    top--;
                }
            }
            else
            {
                SetConsole("SYNTAX ERROR: Unknown block type " + node.nodeType + "!");
                return;
            }
        }

        if (!Emit(OP_HALT))
        {
            return;
        }

        SetConsole("COMPILED SUCCESS: " + bytecodePtr + " words emitted.");
        ip = 0;
        sp = 0;
        isRunning = true;
    }

    private bool PushNode(ref int top, CompilerNode node)
    {
        top++;
        if (top >= workNodeStack.Length)
        {
            SetConsole("COMPILE ERROR: AST nesting is too deep!");
            top = workNodeStack.Length - 1;
            return false;
        }

        workNodeStack[top] = node;
        workStateStack[top] = 0;
        workPatchStack[top] = 0;
        return true;
    }

    private bool Emit(int val)
    {
        if (bytecodePtr >= bytecode.Length)
        {
            SetConsole("COMPILE ERROR: Bytecode exceeds 512 words!");
            return false;
        }

        bytecode[bytecodePtr++] = val;
        return true;
    }

    public void StepVM()
    {
        if (!isRunning)
        {
            return;
        }

        if (ip < 0 || ip >= bytecodePtr)
        {
            isRunning = false;
            SetConsole("RUNTIME ERROR: Instruction pointer out of range.");
            return;
        }

        int op = bytecode[ip++];

        switch (op)
        {
            case OP_PUSH_CONST:
                if (ip >= bytecodePtr || sp >= vmStack.Length)
                {
                    RuntimeStackError();
                    return;
                }
                vmStack[sp++] = bytecode[ip++];
                break;
            case OP_LOAD_VAR:
            {
                if (ip >= bytecodePtr)
                {
                    RuntimeStackError();
                    return;
                }
                int loadVarIndex = bytecode[ip++];
                if (loadVarIndex < 0 || loadVarIndex >= registers.Length || sp >= vmStack.Length)
                {
                    RuntimeStackError();
                    return;
                }
                vmStack[sp++] = registers[loadVarIndex];
                break;
            }
            case OP_STORE_VAR:
            {
                if (ip >= bytecodePtr || sp <= 0)
                {
                    RuntimeStackError();
                    return;
                }
                int storeVarIndex = bytecode[ip++];
                if (storeVarIndex < 0 || storeVarIndex >= registers.Length)
                {
                    RuntimeStackError();
                    return;
                }
                registers[storeVarIndex] = vmStack[--sp];
                UpdateRAMVisuals();
                break;
            }
            case OP_ADD:
            case OP_SUB:
            case OP_GT:
                if (sp < 2)
                {
                    RuntimeStackError();
                    return;
                }
                sp--;
                if (op == OP_ADD)
                {
                    vmStack[sp - 1] = vmStack[sp - 1] + vmStack[sp];
                }
                else if (op == OP_SUB)
                {
                    vmStack[sp - 1] = vmStack[sp - 1] - vmStack[sp];
                }
                else
                {
                    vmStack[sp - 1] = vmStack[sp - 1] > vmStack[sp] ? 1 : 0;
                }
                break;
            case OP_JZ:
                if (ip >= bytecodePtr || sp <= 0)
                {
                    RuntimeStackError();
                    return;
                }
                int target = bytecode[ip++];
                if (vmStack[--sp] == 0)
                {
                    ip = target;
                }
                break;
            case OP_JMP:
                if (ip >= bytecodePtr)
                {
                    RuntimeStackError();
                    return;
                }
                ip = bytecode[ip];
                break;
            case OP_HALT:
                isRunning = false;
                SetConsole("PROGRAM HALTED.");
                break;
            default:
                isRunning = false;
                SetConsole("RUNTIME ERROR: Unknown opcode " + op + ".");
                break;
        }
    }

    public void RunVM()
    {
        int safety = 10000;
        while (isRunning && safety-- > 0)
        {
            StepVM();
        }

        if (isRunning)
        {
            isRunning = false;
            SetConsole("RUNTIME ERROR: Execution step limit exceeded!");
        }
    }

    public void ResetVM()
    {
        ip = 0;
        sp = 0;
        isRunning = false;
        for (int i = 0; i < registers.Length; i++)
        {
            registers[i] = 0;
        }
        UpdateRAMVisuals();
        SetConsole("VM RESET.");
    }

    private void RuntimeStackError()
    {
        isRunning = false;
        SetConsole("RUNTIME ERROR: Invalid stack operation.");
    }

    private void UpdateRAMVisuals()
    {
        if (registerDisplays == null)
        {
            return;
        }

        for (int i = 0; i < registerDisplays.Length && i < registers.Length; i++)
        {
            if (registerDisplays[i] != null)
            {
                registerDisplays[i].text = registers[i].ToString();
            }
        }
    }

    private void SetConsole(string message)
    {
        if (consoleOutput != null)
        {
            consoleOutput.text = message;
        }
        Debug.Log("[TinyC++] " + message);
    }
}
