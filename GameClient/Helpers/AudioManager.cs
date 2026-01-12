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

        private static readonly MediaPlayer _musicPlayer = InitializeMusicPlayer();

        private static string _currentTrackPath;
        private static string[] _currentPlaylist; 

        private static double _masterVolume = GameClient.Properties.Settings.Default.MusicVolume;
        private static double _sfxVolume = GameClient.Properties.Settings.Default.SfxVolume; 

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

        private static MediaPlayer InitializeMusicPlayer()
        {
            var player = new MediaPlayer();
            player.Volume = _masterVolume;
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
                    _preloadedSfx[relativePath] = player;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando audio: {relativePath} - {ex.Message}");
            }
        }

        public static void PlayRandomMusic(string[] playlist)
        {
            if (playlist == null || playlist.Length == 0) return;

            if (_currentPlaylist == playlist && _musicPlayer.Source != null)
            {
                return;
            }

            _currentPlaylist = playlist;
            PlayNextRandomTrack();
        }

        private static void PlayNextRandomTrack()
        {
            if (_currentPlaylist == null || _currentPlaylist.Length == 0) return;

            int index = _random.Next(_currentPlaylist.Length);
            PlayFile(_currentPlaylist[index]);
        }

        private static void PlayFile(string relativePath)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));

                if (!File.Exists(fullPath)) return;

                _musicPlayer.Open(new Uri(fullPath));
                _musicPlayer.Volume = _masterVolume; 
                _musicPlayer.Play();

                _currentTrackPath = relativePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reproduciendo música: {ex.Message}");
            }
        }

        public static void PlaySfx(string relativePath)
        {
            if (_preloadedSfx.ContainsKey(relativePath))
            {
                var player = _preloadedSfx[relativePath];
                player.Stop();
                player.Volume = _sfxVolume; 
                player.Position = TimeSpan.Zero;
                player.Play();
            }
            else
            {
                PreloadSfx(relativePath);
                if (_preloadedSfx.ContainsKey(relativePath))
                {
                    var player = _preloadedSfx[relativePath];
                    player.Play();
                }
            }
        }

        public static void SetVolume(double volume)
        {
            if (volume >= 0 && volume <= 1)
            {
                _masterVolume = volume;
                _musicPlayer.Volume = _masterVolume;

                GameClient.Properties.Settings.Default.MusicVolume = volume;
                GameClient.Properties.Settings.Default.Save();
            }
        }

        public static double GetVolume() => _masterVolume;

        public static void SetSfxVolume(double volume)
        {
            if (volume >= 0 && volume <= 1)
            {
                _sfxVolume = volume;
                foreach (var p in _preloadedSfx.Values)
                {
                    p.Volume = _sfxVolume;
                }

                // Guardar en configuración persistente
                GameClient.Properties.Settings.Default.SfxVolume = volume;
                GameClient.Properties.Settings.Default.Save();
            }
        }

        public static double GetSfxVolume() => _sfxVolume;

        public static void StopMusic()
        {
            _musicPlayer.Stop();
            _musicPlayer.Close();
            _currentTrackPath = null;
            _currentPlaylist = null; 
        }

        private static void Player_MediaEnded(object sender, EventArgs e)
        {
            PlayNextRandomTrack();
        }
    }
}