using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhistlePOC
{
    public sealed class ObjectPoolManager : MonoBehaviour
    {
        [Serializable] public sealed class Entry { public GameObject prefab; [Min(0)] public int prewarm = 8; }
        public List<Entry> catalog = new List<Entry>();
        public int CreatedCount { get; private set; }
        public int ReusedCount { get; private set; }
        public int ActiveCount => active.Count;
        readonly Dictionary<GameObject, Stack<GameObject>> available = new Dictionary<GameObject, Stack<GameObject>>();
        readonly Dictionary<GameObject, GameObject> sources = new Dictionary<GameObject, GameObject>();
        readonly HashSet<GameObject> active = new HashSet<GameObject>();
        bool initialized;
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            foreach (var entry in catalog)
            {
                if (entry.prefab == null || available.ContainsKey(entry.prefab)) continue;
                var stack = new Stack<GameObject>(); available.Add(entry.prefab, stack);
                for (int i = 0; i < entry.prewarm; i++) stack.Push(Create(entry.prefab));
            }
        }
        GameObject Create(GameObject prefab)
        {
            var item = Instantiate(prefab, transform); item.name = prefab.name;
            if (item.GetComponent<PoolMember>() == null) throw new InvalidOperationException("Pool prefab requires PoolMember: " + prefab.name);
            sources.Add(item, prefab); CreatedCount++; item.SetActive(false); return item;
        }
        public GameObject Spawn(GameObject prefab, Transform parent)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab), "Assign a prefab in the Inspector.");
            Initialize();
            if (!available.TryGetValue(prefab, out var stack)) { stack = new Stack<GameObject>(); available.Add(prefab, stack); }
            GameObject item;
            if (stack.Count > 0) { item = stack.Pop(); ReusedCount++; } else item = Create(prefab);
            item.transform.SetParent(parent, false); item.GetComponent<PoolMember>().ResetForSpawn();
            active.Add(item); item.SetActive(true); return item;
        }
        public bool Return(GameObject item)
        {
            if (item == null || !active.Remove(item)) return false;
            item.SetActive(false); item.GetComponent<PoolMember>().ResetForReturn();
            item.transform.SetParent(transform, false); available[sources[item]].Push(item); return true;
        }
        public void ReturnAll()
        {
            foreach (var item in new List<GameObject>(active)) Return(item);
        }
        public bool IsActive(GameObject item) { return item != null && active.Contains(item); }
    }
}
