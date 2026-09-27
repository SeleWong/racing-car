using UnityEngine;

namespace PlaneWar
{
    /// <summary>
    /// 音效管理：订阅 GameEvents 播放对应音效。配置中指定的 AudioClip 优先，
    /// 否则使用运行时合成的简易音效（保证零资源可运行）。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private AudioSource _bgm;
        private AudioSource _sfx;
        private AudioSource _shoot;
        private GameConfig _cfg;
        private bool _muted;

        private AudioClip _clipShoot, _clipSmall, _clipMedium, _clipLarge, _clipLargeAppear,
            _clipDouble, _clipBomb, _clipUseBomb, _clipGameOver, _clipButton;

        public bool Muted
        {
            get { return _muted; }
            set
            {
                _muted = value;
                if (_bgm != null) _bgm.mute = value;
                if (_sfx != null) _sfx.mute = value;
                if (_shoot != null) _shoot.mute = value;
            }
        }

        public void Init(GameConfig cfg)
        {
            _cfg = cfg;
            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.loop = true;
            _bgm.playOnAwake = false;
            _bgm.volume = 0.5f;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _shoot = gameObject.AddComponent<AudioSource>();
            _shoot.playOnAwake = false;
            _shoot.volume = 0.18f;

            _clipShoot = cfg.sfxShoot != null ? cfg.sfxShoot : SfxSynth.Shoot();
            _clipSmall = cfg.sfxEnemyDownSmall != null ? cfg.sfxEnemyDownSmall : SfxSynth.Explosion(0.25f, 0.5f, 1);
            _clipMedium = cfg.sfxEnemyDownMedium != null ? cfg.sfxEnemyDownMedium : SfxSynth.Explosion(0.4f, 0.35f, 2);
            _clipLarge = cfg.sfxEnemyDownLarge != null ? cfg.sfxEnemyDownLarge : SfxSynth.Explosion(0.8f, 0.2f, 3);
            _clipLargeAppear = cfg.sfxLargeAppear != null ? cfg.sfxLargeAppear : SfxSynth.Siren();
            _clipDouble = cfg.sfxGetDoubleBullet != null ? cfg.sfxGetDoubleBullet : SfxSynth.Arpeggio(new[] { 523f, 659f, 784f, 1047f });
            _clipBomb = cfg.sfxGetBomb != null ? cfg.sfxGetBomb : SfxSynth.Arpeggio(new[] { 392f, 523f, 659f });
            _clipUseBomb = cfg.sfxUseBomb != null ? cfg.sfxUseBomb : SfxSynth.Explosion(1.0f, 0.12f, 4);
            _clipGameOver = cfg.sfxGameOver != null ? cfg.sfxGameOver : SfxSynth.Arpeggio(new[] { 523f, 392f, 330f, 262f }, 0.16f);
            _clipButton = cfg.sfxButton != null ? cfg.sfxButton : SfxSynth.Click();

            if (cfg.bgm != null) _bgm.clip = cfg.bgm;

            GameEvents.StateChanged += OnStateChanged;
            GameEvents.PlayerFired += OnFired;
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.EnemySpawned += OnEnemySpawned;
            GameEvents.SupplyCollected += OnSupply;
            GameEvents.BombUsed += OnBombUsed;
        }

        private void OnDestroy()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.PlayerFired -= OnFired;
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.EnemySpawned -= OnEnemySpawned;
            GameEvents.SupplyCollected -= OnSupply;
            GameEvents.BombUsed -= OnBombUsed;
        }

        public void PlayButton() { Play(_clipButton, 0.6f); }

        private void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _muted) return;
            _sfx.PlayOneShot(clip, volume);
        }

        private void OnStateChanged(GameState from, GameState to)
        {
            if (to == GameState.Playing && _bgm.clip != null && !_bgm.isPlaying) _bgm.Play();
            if (to == GameState.Paused || to == GameState.Home) _bgm.Pause();
            if (to == GameState.Playing && from == GameState.Paused) _bgm.UnPause();
            if (to == GameState.PlayerDying || to == GameState.GameOver)
            {
                if (to == GameState.GameOver) { _bgm.Stop(); Play(_clipGameOver, 0.8f); }
                else Play(_clipLarge, 0.9f);
            }
        }

        private void OnFired()
        {
            if (_muted || _clipShoot == null) return;
            _shoot.PlayOneShot(_clipShoot);
        }

        private void OnEnemyKilled(EnemyKind kind, bool byBomb)
        {
            if (byBomb) return; // 炸弹统一播放一次
            switch (kind)
            {
                case EnemyKind.Small: Play(_clipSmall, 0.7f); break;
                case EnemyKind.Medium: Play(_clipMedium, 0.8f); break;
                default: Play(_clipLarge, 1f); break;
            }
        }

        private void OnEnemySpawned(EnemyKind kind)
        {
            if (kind == EnemyKind.Large) Play(_clipLargeAppear, 0.5f);
        }

        private void OnSupply(SupplyKind kind)
        {
            Play(kind == SupplyKind.Bomb ? _clipBomb : _clipDouble, 0.8f);
        }

        private void OnBombUsed() { Play(_clipUseBomb, 1f); }
    }

    /// <summary>运行时合成简单 8-bit 风格音效。</summary>
    public static class SfxSynth
    {
        private const int Rate = 22050;

        private static AudioClip Make(string name, float[] data)
        {
            try
            {
                var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
                clip.SetData(data, 0);
                return clip;
            }
            catch (System.Exception e)
            {
                // 个别平台（部分小游戏环境）不支持 AudioClip.Create，静默降级
                Debug.LogWarning("[PlaneWar] 无法合成音效 " + name + ": " + e.Message);
                return null;
            }
        }

        public static AudioClip Shoot()
        {
            int n = (int)(Rate * 0.06f);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(1400f, 500f, t);
                phase += f / Rate;
                d[i] = (Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f) * (1f - t) * 0.5f;
            }
            return Make("sfx_shoot", d);
        }

        public static AudioClip Explosion(float duration, float brightness, int seed)
        {
            int n = (int)(Rate * duration);
            var d = new float[n];
            var rnd = new System.Random(seed);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float k = Mathf.Lerp(brightness, brightness * 0.2f, t);
                lp += (noise - lp) * k; // 低通让声音更闷
                float env = Mathf.Pow(1f - t, 2f);
                d[i] = Mathf.Clamp(lp * env * 2.2f, -1f, 1f);
            }
            return Make("sfx_explosion_" + seed, d);
        }

        public static AudioClip Arpeggio(float[] notes, float noteLen = 0.07f)
        {
            int per = (int)(Rate * noteLen);
            var d = new float[per * notes.Length];
            for (int k = 0; k < notes.Length; k++)
            {
                for (int i = 0; i < per; i++)
                {
                    float t = (float)i / per;
                    float s = Mathf.Sin(2f * Mathf.PI * notes[k] * i / Rate);
                    d[k * per + i] = (s > 0 ? 0.35f : -0.35f) * (1f - t * 0.7f);
                }
            }
            return Make("sfx_arp", d);
        }

        public static AudioClip Siren()
        {
            int n = (int)(Rate * 1.2f);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = 300f + 120f * Mathf.Sin(t * Mathf.PI * 6f);
                phase += f / Rate;
                d[i] = Mathf.Sin(phase * 2f * Mathf.PI) * 0.4f * Mathf.Sin(t * Mathf.PI);
            }
            return Make("sfx_siren", d);
        }

        public static AudioClip Click()
        {
            int n = (int)(Rate * 0.04f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                d[i] = Mathf.Sin(2f * Mathf.PI * 900f * i / Rate) * (1f - t) * 0.5f;
            }
            return Make("sfx_click", d);
        }
    }
}
