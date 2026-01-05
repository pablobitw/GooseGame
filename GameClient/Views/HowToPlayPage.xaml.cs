using GameClient.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

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
                new TileInfo { Name = "La Oca", Description = "¡De oca a oca y tiro porque me toca! Salta a la siguiente oca y vuelve a lanzar los dados.", ImagePath = "/Assets/Tiles/goose_tile.png" },
                new TileInfo { Name = "El Puente", Description = "De puente a puente y tiro porque me lleva la corriente. Salta al siguiente puente y espera tu turno.", ImagePath = "/Assets/Tiles/bridge_tile.png" },
                new TileInfo { Name = "La Posada", Description = "Un descanso necesario pero costoso. Pierdes un turno mientras te relajas.", ImagePath = "/Assets/Tiles/inn_tile.png" },
                new TileInfo { Name = "El Pozo", Description = "Has caído en la oscuridad. No podrás moverte hasta que otro jugador caiga aquí o pase a rescatarte.", ImagePath = "/Assets/Tiles/well_tile.png" },
                new TileInfo { Name = "El Laberinto", Description = "¡Te has desorientado! Retrocedes inmediatamente a la casilla 30.", ImagePath = "/Assets/Tiles/maze_tile.png" },
                new TileInfo { Name = "La Cárcel", Description = "Has cometido una infracción. Quedas arrestado y pierdes dos turnos.", ImagePath = "/Assets/Tiles/prision_tile.png" },
                new TileInfo { Name = "Casilla de Suerte", Description = "¡El destino te sonríe! Recibes una recompensa aleatoria en monedas.", ImagePath = "/Assets/Tiles/lucky_tile.png" },
                new TileInfo { Name = "La Calavera", Description = "El fin del camino... o el inicio. Regresas inmediatamente a la casilla 1 para empezar de nuevo.", ImagePath = "/Assets/Tiles/skull_tile.png" }
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