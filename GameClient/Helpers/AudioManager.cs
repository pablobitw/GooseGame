using System;
using System.Windows.Media;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace GameClient.Helpers
{
    public static class AudioManager
    {
        private static readonly Dictionary<string, MediaPlayer> _preloadedSfx = new Dictionary<string, MediaPlayer>();
        private static readonly Random _random = new Random();

        private static readonly MediaPlayer _musicPlayer = CreateMusicPlayer();

        private static string _currentTrackPath;
        private static bool _isMusicEnabled = true;
        private static double _sfxVolume = 0.8;

        public static readonly string[] MenuTracks =
        {
            "Assets/Audio/Music/Menu/Menu1.mp3",
            "Assets/Audio/Music/Menu/Menu22.mp3"
        };

        public static readonly string[] LobbyTracks =
        {
            "Assets/Audio/Music/Lobby/Lobby1.mp3"
        };

        public static readonly string[] GameplayTracks =
        {
            "Assets/Audio/Music/Gameplay/Gameplay1.mp3",
            "Assets/Audio/Music/Gameplay/Gameplay2.mp3",
            "Assets/Audio/Music/Gameplay/Gameplay3.mp3"
        };

        public const string SfxDice = "Assets/Audio/Sfx/dice_roll.mp3";

        private static MediaPlayer CreateMusicPlayer()
        {
            var player = new MediaPlayer
            {
                Volume = 0.5
            };
            player.MediaEnded += Player_MediaEnded;

            PreloadSfx(SfxDice);
            return player;
        }

        private static void PreloadSfx(string relativePath)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                if (File.Exists(fullPath))
                {
                    MediaPlayer player = new MediaPlayer();
                    player.Open(new Uri(fullPath));
                    player.Volume = _sfxVolume;

                    player.Play();
                    player.Stop();

                    _preloadedSfx[relativePath] = player; 
                }
            }
            catch
            {
             
                Console.WriteLine($"Error cargando audio: {relativePath}");
            }
        }

        public static void PlayRandomMusic(string[] playlist)
        {
            if (!_isMusicEnabled || playlist == null || playlist.Length == 0) return;
            if (_currentTrackPath != null && playlist.Contains(_currentTrackPath) && _musicPlayer.Position > TimeSpan.Zero) return;
            int index = _random.Next(playlist.Length);
            PlayFile(playlist[index]);
        }

        private static void PlayFile(string relativePath)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                if (!File.Exists(fullPath)) return;

                _musicPlayer.Stop();
                _musicPlayer.Close();
                _musicPlayer.Open(new Uri(fullPath));
                _musicPlayer.Play();
                _currentTrackPath = relativePath;
                _musicPlayer.Position = TimeSpan.FromMilliseconds(1);
            }
            catch
            {
            }
        }

        public static void PlaySfx(string relativePath)
        {
            if (_preloadedSfx.ContainsKey(relativePath))
            {
                var player = _preloadedSfx[relativePath];
                player.Stop();
                player.Position = TimeSpan.Zero;
                player.Play();
            }
            else
            {
                PreloadSfx(relativePath);
                if (_preloadedSfx.ContainsKey(relativePath))
                {
                    var player = _preloadedSfx[relativePath];
                    player.Position = TimeSpan.Zero;
                    player.Play();
                }
            }
        }

        public static void SetVolume(double volume)
        {
            if (volume >= 0 && volume <= 1) _musicPlayer.Volume = volume;
        }

        public static double GetVolume() => _musicPlayer.Volume;

        public static void SetSfxVolume(double volume)
        {
            if (volume >= 0 && volume <= 1)
            {
                _sfxVolume = volume;
                foreach (var p in _preloadedSfx.Values) p.Volume = _sfxVolume;
            }
        }

        public static double GetSfxVolume() => _sfxVolume;

        public static void StopMusic()
        {
            _musicPlayer.Stop();
            _musicPlayer.Close();
            _currentTrackPath = null;
        }

        private static void Player_MediaEnded(object sender, EventArgs e)
        {
            _musicPlayer.Position = TimeSpan.Zero;
            _musicPlayer.Play();
        }
    }
}