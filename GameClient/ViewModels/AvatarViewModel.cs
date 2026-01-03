using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace GameClient.ViewModels
{
    public sealed class AvatarViewModel
    {
        public ObservableCollection<string> AvatarPaths { get; }

        public AvatarViewModel()
        {
            AvatarPaths = new ObservableCollection<string>();
            LoadAvatars();
        }

        private void LoadAvatars()
        {
            try
            {
                string avatarsDir = ResolveAvatarsDirectory();

                if (avatarsDir == null)
                {
                    LoadDesignTimePlaceholders();
                    return;
                }

                var files = Directory.EnumerateFiles(avatarsDir)
                                     .Where(IsImageFile);

                foreach (string file in files)
                {
                    AvatarPaths.Add(file);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AvatarViewModel] Error: {ex.Message}");
            }
        }

        private static string ResolveAvatarsDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string runtimePath = Path.Combine(baseDir, "Assets", "Avatar");
            if (Directory.Exists(runtimePath))
            {
                return runtimePath;
            }

            string designPath = Path.GetFullPath(
                Path.Combine(baseDir, @"..\..\Assets\Avatar"));

            return Directory.Exists(designPath) ? designPath : null;
        }

        private static bool IsImageFile(string path)
        {
            string ext = Path.GetExtension(path);
            return ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase);
        }

        private void LoadDesignTimePlaceholders()
        {
            if (!System.ComponentModel.DesignerProperties
                    .GetIsInDesignMode(new DependencyObject()))
            {
                return;
            }

            for (int i = 0; i < 10; i++)
            {
                AvatarPaths.Add("Placeholder");
            }
        }
    }
}
