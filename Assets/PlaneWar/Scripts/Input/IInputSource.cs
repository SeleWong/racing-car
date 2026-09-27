using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 输入源抽象。每端实现一个即可接入（触屏、鼠标、键盘、手柄、小游戏 SDK 触摸、遥控器……），
    /// 由 InputRouter 合并，玩法层只关心“这一帧飞机要移动多少、是否放炸弹、是否暂停”。
    /// </summary>
    public interface IInputSource
    {
        /// <summary>每帧调用一次（在读取属性前）。</summary>
        void Tick(Camera cam, GameConfig cfg);

        /// <summary>本帧飞机位移（世界单位）。</summary>
        Vector2 MoveDelta { get; }

        bool BombPressed { get; }
        bool PausePressed { get; }

        /// <summary>重置状态（切场景、暂停恢复时防止跳变）。</summary>
        void Reset();
    }
}
