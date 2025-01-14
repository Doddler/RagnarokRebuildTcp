using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Utility
{
    [Serializable]
    public class SerializableDictionary<K, V> : Dictionary<K, V>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<K> m_Keys = new List<K>();

        [SerializeField]
        private List<V> m_Values = new List<V>();

        public void OnBeforeSerialize()
        {
            m_Keys.Clear();
            m_Values.Clear();
            using Enumerator enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<K, V> current = enumerator.Current;
                m_Keys.Add(current.Key);
                m_Values.Add(current.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();
            for (int i = 0; i < m_Keys.Count; i++)
            {
                Add(m_Keys[i], m_Values[i]);
            }

            m_Keys.Clear();
            m_Values.Clear();
        }
    }
}