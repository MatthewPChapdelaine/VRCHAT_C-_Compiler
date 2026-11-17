using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class CompilerNode : UdonSharpBehaviour
{
    [Header("Node Identity")]
    [UdonSynced] public int nodeType;
    [UdonSynced] public int valueOrVarIndex;

    [Header("AST Connections")]
    public CompilerNode parentNode;
    public CompilerNode leftChild;
    public CompilerNode rightChild;
    public Transform leftSocketTransform;
    public Transform rightSocketTransform;

    [Header("Visuals")]
    public TextMeshProUGUI labelText;

    public override void OnPickup()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);

        if (parentNode != null)
        {
            parentNode.DetachChild(this);
            parentNode = null;
        }
    }

    public override void OnPickupUseDown()
    {
        if (nodeType == 7)
        {
            valueOrVarIndex = (valueOrVarIndex + 1) % 10;
            UpdateVisualLabel();
            RequestSerialization();
        }
        else if (nodeType == 6)
        {
            valueOrVarIndex = (valueOrVarIndex + 1) % 4;
            UpdateVisualLabel();
            RequestSerialization();
        }
    }

    public override void OnDrop()
    {
        if (parentNode != null)
        {
            Transform targetSocket = parentNode.leftChild == this
                ? parentNode.leftSocketTransform
                : parentNode.rightSocketTransform;

            if (targetSocket != null)
            {
                transform.SetPositionAndRotation(targetSocket.position, targetSocket.rotation);
            }
        }
    }

    public void AttachChild(CompilerNode child, int slotIndex)
    {
        if (child == null || child == this || child.parentNode != null)
        {
            return;
        }

        if (slotIndex == 0 && leftChild == null && leftSocketTransform != null)
        {
            leftChild = child;
            child.parentNode = this;
            child.transform.SetPositionAndRotation(leftSocketTransform.position, leftSocketTransform.rotation);
        }
        else if (slotIndex == 1 && rightChild == null && rightSocketTransform != null)
        {
            rightChild = child;
            child.parentNode = this;
            child.transform.SetPositionAndRotation(rightSocketTransform.position, rightSocketTransform.rotation);
        }
    }

    public void DetachChild(CompilerNode child)
    {
        if (child == null)
        {
            return;
        }

        if (leftChild == child)
        {
            leftChild = null;
        }

        if (rightChild == child)
        {
            rightChild = null;
        }
    }

    public void UpdateVisualLabel()
    {
        if (labelText == null)
        {
            return;
        }

        if (nodeType == 7)
        {
            labelText.text = valueOrVarIndex.ToString();
        }
        else if (nodeType == 6)
        {
            string[] varNames = new string[] { "Var_A", "Var_B", "Var_C", "Var_D" };
            if (valueOrVarIndex >= 0 && valueOrVarIndex < varNames.Length)
            {
                labelText.text = varNames[valueOrVarIndex];
            }
        }
    }

    public override void OnDeserialization()
    {
        UpdateVisualLabel();
    }

    public void Interact()
    {
        OnPickupUseDown();
    }
}
