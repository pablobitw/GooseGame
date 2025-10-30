using GameClient.ChatServiceReference;
using System;
using System.ComponentModel;
using System.ServiceModel;
using System.Windows;
using System.Windows.Input;

namespace GameClient.Views
{
    public class ChatMessage
    {
        public string PlayerName { get; set; }
        public string Message { get; set; }
    }

    public partial class ChatWindow : Window, IChatServiceCallback
    {
        private ChatServiceClient _chatClient;
        private string _playerName;

        public ChatWindow(string username)
        {
            InitializeComponent();
            _playerName = username;
            this.Title = $"Chat - Connected as: {_playerName}";
            ConnectToChat();
            this.Closing += ChatWindow_Closing; // asegurar la desconexión
        }

        private void ConnectToChat()
        {
            try
            {
                InstanceContext context = new InstanceContext(this);
                _chatClient = new ChatServiceClient(context);

                             _chatClient.JoinChat(_playerName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not connect to chat server: " + ex.Message);
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e) 
        { 
            SendMessage(); 
        }
        private void MessageBoxTextBox_KeyDown(object sender, KeyEventArgs e) 
        { 
            if (e.Key == Key.Enter) 
                SendMessage();
        }

        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageBoxTextBox.Text) || _chatClient == null) return;

            try
            {
                _chatClient.SendMessage(_playerName, MessageBoxTextBox.Text);

                AddMessageToUI("Me:", MessageBoxTextBox.Text);
                MessageBoxTextBox.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error sending message: " + ex.Message);
            }
        }

        private void AddMessageToUI(string name, string message)
        {
            var newMessage = new ChatMessage { PlayerName = name, Message = message };
            MessagesListBox.Items.Add(newMessage);
            MessagesListBox.ScrollIntoView(newMessage);
        }

        public void ReceiveMessage(string senderName, string message)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessageToUI(senderName + ":", message);
            });
        }

        private void ChatWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_chatClient != null)
            {
                try
                {
                    _chatClient.Leave(_playerName);
                    _chatClient.Close();
                }
                catch { _chatClient.Abort(); }
            }
        }
    }
}