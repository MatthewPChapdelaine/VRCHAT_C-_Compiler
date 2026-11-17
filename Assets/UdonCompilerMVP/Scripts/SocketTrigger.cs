using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class SocketTrigger : UdonSharpBehaviour
{
    public CompilerNode ownerNode;
    public int slotIndex;
    public AudioSource snapAudio;

    private void OnTriggerEnter(Collider other)
    {
        if (ownerNode == null)
        {
            return;
        }

        CompilerNode incomingNode = other.GetComponentInParent<CompilerNode>();
        if (incomingNode == null || incomingNode == ownerNode || incomingNode.parentNode != null)
        {
            return;
        }

        VRC_Pickup pickup = incomingNode.GetComponent(typeof(VRC_Pickup)) as VRC_Pickup;
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
