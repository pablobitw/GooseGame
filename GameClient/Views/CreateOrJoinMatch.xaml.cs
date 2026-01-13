using GameClient.Helpers;
using System;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GameClient.Views
{
    public partial class CreateOrJoinMatchPage : Page
    {
        private readonly string _username;

        public CreateOrJoinMatchPage(string username)
        {
            InitializeComponent();
            _username = username;
        }

        private void CreateMatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserSession.GetInstance().IsGuest)
            {
                HandleGuestAccess();
                return;
            }

            try
            {
                NavigationService.Navigate(new LobbyPage(_username));
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show(
                    string.Format(GameClient.Resources.Strings.MatchMenu_LobbyCreateTimeout, ex.Message),
                    GameClient.Resources.Strings.MatchMenu_TimeoutTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    string.Format(GameClient.Resources.Strings.MatchMenu_LobbyCreateError, ex.Message),
                    GameClient.Resources.Strings.MatchMenu_ConnectionTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void HandleGuestAccess()
        {
            var result = MessageBox.Show(
                GameClient.Resources.Strings.MatchMenu_GuestRestriction,
                GameClient.Resources.Strings.MatchMenu_GuestTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                AuthWindow authWindow = new AuthWindow();
                authWindow.Show();
                authWindow.NavigateToRegister();

                Window.GetWindow(this)?.Close();
            }
        }

        private void JoinMatchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new JoinMatchCodePage(_username));
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show(
                    string.Format(GameClient.Resources.Strings.MatchMenu_ServerNoResponse, ex.Message),
                    GameClient.Resources.Strings.MatchMenu_ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    string.Format(GameClient.Resources.Strings.MatchMenu_ConnectionError, ex.Message),
                    GameClient.Resources.Strings.MatchMenu_ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ViewMatchesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new ListMatchesPage(_username));
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show(
                    string.Format(GameClient.Resources.Strings.MatchMenu_ServerNoResponse, ex.Message),
                    GameClient.Resources.Strings.MatchMenu_ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    string.Format(GameClient.Resources.Strings.MatchMenu_ConnectionError, ex.Message),
                    GameClient.Resources.Strings.MatchMenu_ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is GameMainWindow mainWindow)
            {
                await mainWindow.ShowMainMenu();
            }
        }
    }
}