using UnityEngine;

namespace WhistlePOC
{
    public sealed class PooledProjectileVisual : MonoBehaviour
    {
        public MeshRenderer[] body;
        public LineRenderer[] contours;
        public LineRenderer soundWave;
    }
}
