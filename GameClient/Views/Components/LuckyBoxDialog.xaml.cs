using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace GameClient.Views.Dialogs
{
    public partial class LuckyBoxDialog : UserControl
    {
        public event EventHandler DialogClosed;

        private int _luckyBoxClicks = 0;
        private string _currentRewardType = "";
        private int _currentRewardAmount = 0;

        public LuckyBoxDialog()
        {
            InitializeComponent();
        }

        public void ShowReward(string type, int amount)
        {
            _currentRewardType = type;
            _currentRewardAmount = amount;
            _luckyBoxClicks = 0;

            ResetState();
            this.Visibility = Visibility.Visible;
        }

        private void ResetState()
        {
            try
            {
                LuckyBoxImage.Source = new BitmapImage(new Uri("/Assets/Images/luckybox_closed.png", UriKind.Relative));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LuckyBoxDialog] Error al cargar imagen cerrada: {ex.Message}");
            }

            if (LuckyBoxImage.RenderTransform is RotateTransform rt)
            {
                rt.Angle = 0;
            }

            LuckyBoxImage.Visibility = Visibility.Visible;
            RewardContainer.Visibility = Visibility.Collapsed;
            OpenBoxButton.IsEnabled = true;
        }

        private async void OpenBoxButton_Click(object sender, RoutedEventArgs e)
        {
            _luckyBoxClicks++;
            var shakeAnim = this.Resources["ShakeAnimation"] as Storyboard;

            if (_luckyBoxClicks < 3)
            {
                shakeAnim?.Begin();
            }
            else
            {
                OpenBoxButton.IsEnabled = false;
                LuckyBoxImage.Visibility = Visibility.Collapsed;

                SetRewardVisuals();
                RewardContainer.Visibility = Visibility.Visible;

                var revealAnim = this.Resources["RevealAnimation"] as Storyboard;
                revealAnim?.Begin();

                await Task.Delay(3000);

                this.Visibility = Visibility.Collapsed;
                DialogClosed?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SetRewardVisuals()
        {
            string imagePath = "";
            string text = "";
            SolidColorBrush color = Brushes.White;

            switch (_currentRewardType)
            {
                case "COINS":
                    imagePath = "coin_pile.png";
                    text = string.Format(GameClient.Resources.Strings.RewardGold, _currentRewardAmount);
                    color = Brushes.Gold;
                    break;
                case "COMMON":
                    imagePath = "ticket_common.png";
                    text = GameClient.Resources.Strings.RewardCommon;
                    break;
                case "EPIC":
                    imagePath = "ticket_epic.png";
                    text = GameClient.Resources.Strings.RewardEpic;
                    color = Brushes.Purple;
                    break;
                case "LEGENDARY":
                    imagePath = "ticket_legendary.png";
                    text = GameClient.Resources.Strings.RewardLegendary;
                    color = Brushes.OrangeRed;
                    break;
            }

            RewardText.Text = text;
            RewardText.Foreground = color;

            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    RewardImage.Source = new BitmapImage(new Uri($"/Assets/Images/{imagePath}", UriKind.Relative));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LuckyBoxDialog] Error al cargar imagen de recompensa: {ex.Message}");
                }
            }
        }

        private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
