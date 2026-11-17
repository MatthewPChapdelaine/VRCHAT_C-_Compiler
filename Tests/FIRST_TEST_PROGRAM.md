# First TinyC++ VM Test

Build this physical AST in VRChat:

```text
Var_A = 3;
while (Var_A > 0) {
    Var_A = Var_A - 1;
}
```

## Assembly

1. Snap a `SEQ` block into the workbench root.
2. Snap `ASSIGN` into `SEQ.slot0`.
   - `ASSIGN.slot0`: `VAR` configured to `Var_A`
   - `ASSIGN.slot1`: `CONST` configured to `3`
3. Snap `WHILE` into `SEQ.slot1`.
   - `WHILE.slot0`: `GT`
     - `GT.slot0`: `VAR` configured to `Var_A`
     - `GT.slot1`: `CONST` configured to `0`
   - `WHILE.slot1`: `ASSIGN`
     - `ASSIGN.slot0`: `VAR` configured to `Var_A`
     - `ASSIGN.slot1`: `SUB`
       - `SUB.slot0`: `VAR` configured to `Var_A`
       - `SUB.slot1`: `CONST` configured to `1`

## Verification

1. Pull the compile lever.
2. Confirm the console reports `COMPILED SUCCESS`.
3. Press `Step` repeatedly, or press `Run`.
4. Confirm the VAR_A RAM display changes `3 -> 2 -> 1 -> 0`.
5. Confirm the console reports `PROGRAM HALTED`.
