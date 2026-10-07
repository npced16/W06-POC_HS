using UnityEngine;

namespace WhistlePOC
{
    public sealed class PoolMember : MonoBehaviour
    {
        struct Pose { public Transform node; public Vector3 position, scale; public Quaternion rotation; }
        Pose[] poses;
        Renderer[] renderers;
        void Awake() { Cache(); }
        void Cache()
        {
            if (poses != null) return;
            var nodes = GetComponentsInChildren<Transform>(true); poses = new Pose[nodes.Length];
            for (int i = 0; i < nodes.Length; i++) poses[i] = new Pose { node = nodes[i], position = nodes[i].localPosition, rotation = nodes[i].localRotation, scale = nodes[i].localScale };
            renderers = GetComponentsInChildren<Renderer>(true);
        }
        public void ResetForSpawn()
        {
            Cache();
            foreach (var pose in poses) { pose.node.localPosition = pose.position; pose.node.localRotation = pose.rotation; pose.node.localScale = pose.scale; }
            foreach (var renderer in renderers) { renderer.SetPropertyBlock(null); renderer.enabled = false; }
            var graphic = GetComponent<PooledSwordGraphic>(); if (graphic != null) graphic.ClearBinding();
        }
        public void ResetForReturn()
        {
            foreach (var renderer in renderers) { renderer.SetPropertyBlock(null); renderer.enabled = false; }
            var graphic = GetComponent<PooledSwordGraphic>(); if (graphic != null) graphic.ClearBinding();
        }
    }
}
