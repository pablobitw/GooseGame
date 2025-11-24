using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace GameClient.ViewModels
{
    public class AvatarViewModel
    {
        public ObservableCollection<string> AvatarPaths { get; set; } = new ObservableCollection<string>();

        public AvatarViewModel()
        {
            LoadAvatars();
        }

        private void LoadAvatars()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string avatarsDir = Path.Combine(baseDir, "Assets", "Avatar");

                if (!Directory.Exists(avatarsDir))
                {
                    string projectPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\Assets\Avatar"));
                    if (Directory.Exists(projectPath))
                    {
                        avatarsDir = projectPath;
                    }
                }

                if (Directory.Exists(avatarsDir))
                {
                    var files = Directory.GetFiles(avatarsDir)
                                         .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg"));

                    foreach (var file in files)
                    {
                        AvatarPaths.Add(file);
                    }
                }
                else
                {
                    if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                    {
                        for (int i = 0; i < 10; i++) AvatarPaths.Add("Placeholder");
                    }
                }
            }
            catch { /* Ignorar errores en diseño */ }
        }
    }
}