using UnityEngine;

namespace WhistlePOC
{
    public sealed class PooledEnemyVisual : MonoBehaviour
    {
        public MeshRenderer[] body;
        public LineRenderer[] contours, footsteps, falseFootsteps;
        public Transform weapon;
        public void HideBody() { foreach (var mesh in body) mesh.enabled = false; }
    }
}
