using UnityEngine;

namespace DarkFlare
{
    public class GameplayPauseSystem : AbstractSystem
    {
        float _previousTimeScale = 1f;

        public bool IsPaused { get; private set; }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused)
            {
                return;
            }

            if (paused)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _previousTimeScale;
            }

            IsPaused = paused;
            this.SendEvent(new GameplayPauseChangedEvent(paused));
            Debug.Log($"[GameplayPauseSystem] 游戏暂停状态切换为 {paused}");
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            if (IsPaused)
            {
                Time.timeScale = _previousTimeScale;
                IsPaused = false;
            }
        }
    }
}
