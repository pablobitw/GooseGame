using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameClient.Helpers
{
    public class ChatUiManager
    {
        private readonly TabControl _tabControl;
        private readonly ListBox _generalChatList;
        private readonly Style _tabStyle;

        public ChatUiManager(TabControl tabControl, ListBox generalChatList, Style tabStyle)
        {
            _tabControl = tabControl;
            _generalChatList = generalChatList;
            _tabStyle = tabStyle;
        }

        public string GetCurrentTarget()
        {
            var selectedTab = _tabControl.SelectedItem as TabItem;
            return selectedTab?.Tag?.ToString() ?? "General";
        }

        public void AddMessage(string sender, string message, bool isPrivate, string targetUser, string currentUser)
        {
            string tabName = "General";
            if (isPrivate)
            {
                tabName = (sender == currentUser) ? targetUser : sender;
            }

            ListBox targetList = GetOrCreateTab(tabName);
            targetList.Items.Add($"{sender}: {message}");
            targetList.ScrollIntoView(targetList.Items[targetList.Items.Count - 1]);
        }

        public void EnsureTabExists(string targetUser)
        {
            GetOrCreateTab(targetUser);
        }

        public void SelectTab(string targetUser)
        {
            foreach (TabItem item in _tabControl.Items)
            {
                if (item.Tag?.ToString() == targetUser)
                {
                    _tabControl.SelectedItem = item;
                    break;
                }
            }
        }

        private ListBox GetOrCreateTab(string tabName)
        {
            foreach (TabItem item in _tabControl.Items)
            {
                if (item.Tag?.ToString() == tabName) return (ListBox)item.Content;
            }

            var newListBox = new ListBox
            {
                Background = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 5, 0, 0)
            };

            if (_generalChatList.ItemTemplate != null)
                newListBox.ItemTemplate = _generalChatList.ItemTemplate;

            var newTab = new TabItem
            {
                Header = tabName,
                Tag = tabName,
                Content = newListBox,
                Style = _tabStyle
            };

            _tabControl.Items.Add(newTab);
            return newListBox;
        }
    }
}