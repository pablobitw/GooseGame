using GameClient.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using GameClient.Resources;

namespace GameClient.Views
{
    public partial class HowToPlayPage : Page
    {
        public List<TileInfo> TileList { get; set; }

        public HowToPlayPage()
        {
            InitializeComponent();
            LoadTileData();
            DataContext = this;

            if (TileList.Count > 0)
            {
                TilesListBox.SelectedIndex = 0;
            }
        }

        private void LoadTileData()
        {
            TileList = new List<TileInfo>
            {
                new TileInfo {
                    Name = Strings.TileGooseName,
                    Description = Strings.TileGooseDesc,
                    ImagePath = "/Assets/Tiles/goose_tile.png"
                },
                new TileInfo {
                    Name = Strings.TileBridgeName,
                    Description = Strings.TileBridgeDesc,
                    ImagePath = "/Assets/Tiles/bridge_tile.png"
                },
                new TileInfo {
                    Name = Strings.TileInnName,
                    Description = Strings.TileInnDesc,
                    ImagePath = "/Assets/Tiles/inn_tile.png"
                },
                new TileInfo {
                    Name = Strings.TileWellName,
                    Description = Strings.TileWellDesc,
                    ImagePath = "/Assets/Tiles/well_tile.png"
                },
                new TileInfo {
                    Name = Strings.TileMazeName,
                    Description = Strings.TileMazeDesc,
                    ImagePath = "/Assets/Tiles/maze_tile.png"
                },
                new TileInfo {
                    Name = Strings.TilePrisonName,
                    Description = Strings.TilePrisonDesc,
                    ImagePath = "/Assets/Tiles/prision_tile.png"
                },
                new TileInfo {
                    Name = Strings.TileLuckyName,
                    Description = Strings.TileLuckyDesc,
                    ImagePath = "/Assets/Tiles/lucky_tile.png"
                },
                new TileInfo {
                    Name = Strings.TileSkullName,
                    Description = Strings.TileSkullDesc,
                    ImagePath = "/Assets/Tiles/skull_tile.png"
                }
            };
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is GameMainWindow mw)
            {
                _ = mw.ShowMainMenu();
            }
            else
            {
                NavigationService?.GoBack();
            }
        }
    }
}