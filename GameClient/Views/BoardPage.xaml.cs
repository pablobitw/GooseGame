using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class BoardPage : Page
    {
        private string _lobbyCode;
        private int _boardId;

        public BoardPage(string lobbyCode, int boardId)
        {
            InitializeComponent();
            _lobbyCode = lobbyCode;
            _boardId = boardId;

            LoadBoardImage();
        }

        private void LoadBoardImage()
        {
            string imagePath;
            if (_boardId == 1)
            {
                imagePath = "pack://application:,,,/Assets/Boards/normal_board.png";
            }
            else
            {
                imagePath = "pack://application:,,,/Assets/Boards/special_board.png";
            }

            BoardGrid.Background = new ImageBrush(
                new BitmapImage(new Uri(imagePath))
            );
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}