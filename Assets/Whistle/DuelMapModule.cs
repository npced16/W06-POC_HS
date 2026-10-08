using UnityEngine;

namespace WhistlePOC
{
    // List only ground-level blockers: floors and overhead pipes must not enter navigation.
    public sealed class DuelMapModule : MonoBehaviour
    {
        public BoxCollider[] navigationObstacles = new BoxCollider[0];
    }
}
