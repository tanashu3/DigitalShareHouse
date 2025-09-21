using UnityEngine;

namespace MicroWorldNS
{
    public class LinkToMicroWorld : MonoBehaviour, ILinkToMicroWorld
    {
        [Tooltip("Link to MicroWorld. It is used to find MicroWorld by this link.")]
        public MicroWorld MicroWorld;

        MicroWorld ILinkToMicroWorld.MicroWorld => MicroWorld;
    }
}