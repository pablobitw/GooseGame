using System;
using System.Windows.Media;
using System.IO;
using System.Linq;

namespace GameClient.Helpers
{
    public static class AudioManager
    {
        private static MediaPlayer _musicPlayer;
        private static string _currentTrackPath;
        private static bool _isMusicEnabled = true;
        private static readonly Random _random = new Random();

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

        static AudioManager()
        {
            _musicPlayer = new MediaPlayer();
            _musicPlayer.Volume = 0.5;
            _musicPlayer.MediaEnded += Player_MediaEnded;
        }

        public static void PlayRandomMusic(string[] playlist)
        {
            if (!_isMusicEnabled || playlist == null || playlist.Length == 0) return;

            if (_currentTrackPath != null && playlist.Contains(_currentTrackPath) && _musicPlayer.Position > TimeSpan.Zero)
            {
                return;
            }

            int index = _random.Next(playlist.Length);
            string nextTrack = playlist[index];

            PlayFile(nextTrack);
        }

        private static void PlayFile(string relativePath)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));

                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"[AudioManager] Archivo no encontrado: {fullPath}");
                    return;
                }

                _musicPlayer.Stop();
                _musicPlayer.Open(new Uri(fullPath));
                _musicPlayer.Play();

                _currentTrackPath = relativePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioManager] Error reproduciendo audio: {ex.Message}");
            }
        }

        public static void SetVolume(double volume)
        {
            if (volume >= 0 && volume <= 1)
            {
                _musicPlayer.Volume = volume;
            }
        }

        public static double GetVolume()
        {
            return _musicPlayer.Volume;
        }

        public static void StopMusic()
        {
            _musicPlayer.Stop();
            _currentTrackPath = null;
        }

        private static void Player_MediaEnded(object sender, EventArgs e)
        {
            _musicPlayer.Position = TimeSpan.Zero;
            _musicPlayer.Play();
        }
    }
}