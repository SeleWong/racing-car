using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlaneWar
{
    /// <summary>简单组件对象池，避免频繁 Instantiate/Destroy（小游戏平台 GC 很敏感）。</summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly Stack<T> _free = new Stack<T>();
        private readonly Func<T> _factory;
        private readonly Transform _root;

        public ObjectPool(Func<T> factory, Transform root, int prewarm = 0)
        {
            _factory = factory;
            _root = root;
            for (int i = 0; i < prewarm; i++)
            {
                var item = _factory();
                item.gameObject.SetActive(false);
                item.transform.SetParent(_root, false);
                _free.Push(item);
            }
        }

        public T Get()
        {
            T item = _free.Count > 0 ? _free.Pop() : null;
            if (item == null)
            {
                item = _factory();
                item.transform.SetParent(_root, false);
            }
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            _free.Push(item);
        }
    }
}
