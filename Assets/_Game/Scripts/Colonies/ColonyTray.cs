using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public sealed class ColonyTray : MonoBehaviour
    {
        [SerializeField] private Transform slotsRoot;
        [SerializeField, Min(0.1f)] private float horizontalSpacing = 0.92f;
        [SerializeField] private Vector2 slotSize = new(0.72f, 0.62f);
        private readonly List<ColonySlot> slots = new();
        public event Action<ColonyController> ColonyCompleted;
        public IReadOnlyList<ColonySlot> Slots => slots;
        public bool IsFull => slots.Count > 0 && slots.TrueForAll(slot => !slot.IsEmpty);

        public void Build(int capacity)
        {
            ClearObjects();
            if (slotsRoot == null) slotsRoot = transform;
            for (int i = 0; i < capacity; i++)
            {
                var go = new GameObject($"ColonySlot_{i}");
                go.transform.SetParent(slotsRoot, false);
                go.transform.localPosition = new Vector3((i - (capacity - 1) * 0.5f) * horizontalSpacing, 0.12f, 0f);
                go.transform.localScale = new Vector3(slotSize.x, slotSize.y, 1f);
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                var slot = go.AddComponent<ColonySlot>();
                slot.Configure(renderer);
                slot.Initialize(i);
                slots.Add(slot);
            }
        }

        public bool TryAdd(Func<Vector3, Action<ColonyController>, ColonyController> factory, out ColonyController colony, out ColonySlot slot)
        {
            colony = null;
            slot = slots.Find(item => item.IsEmpty);
            if (slot == null) return false;
            colony = factory(slot.AntSpawnWorldPosition, OnColonyCompleted);
            slot.Occupy(colony);
            return colony != null;
        }

        public void Tick()
        {
            foreach (ColonySlot slot in slots)
                if (!slot.IsEmpty) slot.Colony.Tick();
        }

        public bool HasProgressingColony()
        {
            foreach (ColonySlot slot in slots)
            {
                if (slot.IsEmpty) continue;
                if (slot.Colony.ActiveAntCount > 0 || slot.Colony.HasAvailableTarget()) return true;
            }
            return false;
        }

        private void OnColonyCompleted(ColonyController colony)
        {
            ColonySlot slot = slots.Find(item => item.Colony == colony);
            slot?.Clear();
            ColonyCompleted?.Invoke(colony);
        }

        public void AddExtraSlot(int amount = 1)
        {
            int oldCapacity = slots.Count;
            int newCapacity = oldCapacity + amount;

            for (int i = oldCapacity; i < newCapacity; i++)
            {
                var go = new GameObject($"ColonySlot_{i}");
                go.transform.SetParent(slotsRoot, false);
                go.transform.localScale = new Vector3(slotSize.x, slotSize.y, 1f);
                
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                var slot = go.AddComponent<ColonySlot>();
                slot.Configure(renderer);
                slot.Initialize(i);
                slots.Add(slot);
            }
        }

        private void ClearObjects()
        {
            foreach (ColonySlot slot in slots)
                if (slot != null) Destroy(slot.gameObject);
            slots.Clear();
        }
    }
}
