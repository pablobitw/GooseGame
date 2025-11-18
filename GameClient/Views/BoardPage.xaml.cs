using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading; 
using GameClient.GameplayServiceReference;

namespace GameClient.Views
{
    public partial class BoardPage : Page
    {
        private string lobbyCode;
        private int boardId;
        private string currentUsername; 
        private GameplayServiceClient gameplayClient;
        private DispatcherTimer gameLoopTimer;

        public BoardPage(string lobbyCode, int boardId, string username)
        {
            InitializeComponent();
            this.lobbyCode = lobbyCode;
            this.boardId = boardId;
            this.currentUsername = username;

            gameplayClient = new GameplayServiceClient();

            LoadBoardImage();
            StartGameLoop();
        }

        private void LoadBoardImage()
        {
            string imagePath = (boardId == 1)
                ? "pack://application:,,,/Assets/Boards/normal_board.png"
                : "pack://application:,,,/Assets/Boards/special_board.png";

            BoardGrid.Background = new ImageBrush(new BitmapImage(new Uri(imagePath)));
        }

        private void StartGameLoop()
        {
            gameLoopTimer = new DispatcherTimer();
            gameLoopTimer.Interval = TimeSpan.FromSeconds(2);
            gameLoopTimer.Tick += async (s, e) => await UpdateGameState();
            gameLoopTimer.Start();
        }

        private async Task UpdateGameState()
        {
            try
            {
                var state = await gameplayClient.GetGameStateAsync(lobbyCode, currentUsername);

                if (state != null)
                {
                    if (state.IsMyTurn)
                    {
                        RollDiceButton.IsEnabled = true;
                        RollDiceButton.Content = "¡Tirar Dados!";
                    }
                    else
                    {
                        RollDiceButton.IsEnabled = false;
                        RollDiceButton.Content = $"Esperando a {state.CurrentTurnUsername}...";
                    }

                    if (state.LastDiceOne > 0)
                    {
                        DiceOneText.Text = state.LastDiceOne.ToString();
                        DiceTwoText.Text = state.LastDiceTwo.ToString();
                    }

                    if (state.GameLog != null)
                    {
                       
                        GameLogListBox.Items.Clear();
                        foreach (var log in state.GameLog)
                        {
                            GameLogListBox.Items.Add(log);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error actualizando juego: " + ex.Message);
            }
        }

        private async void RollDiceButton_Click(object sender, RoutedEventArgs e)
        {
            RollDiceButton.IsEnabled = false; 

            try
            {
                var result = await gameplayClient.RollDiceAsync(lobbyCode, currentUsername);

                DiceOneText.Text = result.DiceOne.ToString();
                DiceTwoText.Text = result.DiceTwo.ToString();

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al tirar dados: " + ex.Message);
                RollDiceButton.IsEnabled = true; 
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            gameLoopTimer?.Stop();
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}