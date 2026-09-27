using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 组件对象池：局内零 Instantiate / Destroy，避免 GC 与原生内存抖动（小游戏平台尤其敏感）。
    /// maxSize 限制空闲对象上限，超出时直接销毁，防止异常峰值后池子长期占用内存。
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly Stack<T> _free;
        private readonly Func<T> _factory;
        private readonly Transform _root;
        private readonly int _maxSize;

        public int CountInactive { get { return _free.Count; } }
        public int CountAll { get; private set; }

        public ObjectPool(Func<T> factory, Transform root, int prewarm, int maxSize)
        {
            _factory = factory;
            _root = root;
            _maxSize = Mathf.Max(1, maxSize);
            _free = new Stack<T>(Mathf.Max(prewarm, 4));
            for (int i = 0; i < prewarm && i < _maxSize; i++)
            {
                var item = Create();
                item.gameObject.SetActive(false);
                _free.Push(item);
            }
        }

        private T Create()
        {
            var item = _factory();
            item.transform.SetParent(_root, false);
            CountAll++;
            return item;
        }

        public T Get()
        {
            T item = null;
            // 跳过被外部意外销毁的对象
            while (_free.Count > 0 && item == null) item = _free.Pop();
            if (item == null) item = Create();
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            if (_free.Count >= _maxSize)
            {
                CountAll--;
                UnityEngine.Object.Destroy(item.gameObject);
                return;
            }
            item.gameObject.SetActive(false);
            _free.Push(item);
        }

        /// <summary>销毁所有空闲对象（切场景 / 内存告警时调用）。</summary>
        public void Clear()
        {
            while (_free.Count > 0)
            {
                var item = _free.Pop();
                if (item != null) UnityEngine.Object.Destroy(item.gameObject);
                CountAll--;
            }
        }
    }
}
