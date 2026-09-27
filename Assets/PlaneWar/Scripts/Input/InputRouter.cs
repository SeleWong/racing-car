using System.Collections.Generic;
using UnityEngine;

namespace PlaneWar
{
    /// <summary>合并所有已注册输入源。外部可调用 Register 接入自定义输入（如体感、遥控器、SDK 触摸）。</summary>
    public class InputRouter
    {
        private readonly List<IInputSource> _sources = new List<IInputSource>();

        public Vector2 MoveDelta { get; private set; }
        public bool BombPressed { get; private set; }
        public bool PausePressed { get; private set; }

        public static InputRouter CreateDefault()
        {
            var r = new InputRouter();
            r.Register(new PointerDragInput());
            r.Register(new KeyboardInput());
            return r;
        }

        public void Register(IInputSource source)
        {
            if (source != null && !_sources.Contains(source)) _sources.Add(source);
        }

        public void Unregister(IInputSource source) { _sources.Remove(source); }

        public void Tick(Camera cam, GameConfig cfg)
        {
            Vector2 move = Vector2.zero;
            bool bomb = false, pause = false;
            for (int i = 0; i < _sources.Count; i++)
            {
                var s = _sources[i];
                s.Tick(cam, cfg);
                move += s.MoveDelta;
                bomb |= s.BombPressed;
                pause |= s.PausePressed;
            }
            MoveDelta = move;
            BombPressed = bomb;
            PausePressed = pause;
        }

        public void Reset()
        {
            for (int i = 0; i < _sources.Count; i++) _sources[i].Reset();
            MoveDelta = Vector2.zero;
            BombPressed = false;
            PausePressed = false;
        }
    }
}
