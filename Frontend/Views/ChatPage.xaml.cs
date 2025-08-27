using Newtonsoft.Json;
using SOE.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Contact = SOE.Models.Contact;

namespace SOE.Views
{
    public partial class ChatPage : ContentPage
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new();
        public string RecipientName { get; }

        private readonly string _recipientId;
        private readonly HttpClient _httpClient = new();

        private readonly string _deviceId;

        public ChatPage(string recipientId)
        {
            InitializeComponent();
            
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            _recipientId = recipientId;
            _deviceId = Preferences.Get("DeviceId", Guid.NewGuid().ToString());

            var contactsJson = Preferences.Get("Contacts", "[]");
            var contacts = JsonConvert.DeserializeObject<List<Contact>>(contactsJson);
            RecipientName = contacts?.FirstOrDefault(c => c.Id == recipientId)?.Nickname ?? $"User {recipientId.Substring(0, 4)}";

            BindingContext = this;
            LoadMessages();

            // Подписка на обновления чата
            MessagingCenter.Subscribe<MainPage, Message>(this, "UpdateChat", (sender, message) =>
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    if (message.From == _recipientId)
                    {
                        Messages.Add(new ChatMessage
                        {
                            Content = message.Content,
                            Date = message.Date,
                            IsIncoming = true
                        });
                        SaveMessages();
                        MessagesCollectionView.ScrollTo(Messages.Last(), animate: true);
                    }
                });
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            MessagingCenter.Unsubscribe<MainPage, Message>(this, "UpdateChat");
        }

        private void OnMessageEntryCompleted(object sender, EventArgs e)
        {
            SendClicked(sender, e);
        }

        private void LoadMessages()
        {
            var savedMessages = Preferences.Get($"Messages_{_recipientId}", "[]");
            var messages = JsonConvert.DeserializeObject<List<ChatMessage>>(savedMessages) ?? new List<ChatMessage>();

            Messages.Clear();
            foreach (var msg in messages.OrderBy(m => m.Date))
            {
                Messages.Add(msg);
            }
        }

        private async void SendClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageEntry.Text))
                return;

            var message = new Message
            {
                To = _recipientId,
                From = _deviceId,
                Content = MessageEntry.Text,
                Date = DateTime.UtcNow
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "https://retality-education-retality-messenger-48ce.twc1.net/api/messages",
                    message);

                if (response.IsSuccessStatusCode)
                {
                    var chatMessage = new ChatMessage
                    {
                        Content = message.Content,
                        Date = message.Date,
                        IsIncoming = false
                    };

                    Messages.Add(chatMessage);
                    SaveMessages();
                    MessageEntry.Text = string.Empty;
                    MessagesCollectionView.ScrollTo(chatMessage, animate: true);
                }
                else
                {
                    await DisplayAlert("Ошибка", $"Не удалось отправить сообщение: {response.StatusCode}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка", $"Ошибка при отправке сообщения: {ex.Message}", "OK");
                Debug.WriteLine($"Ошибка отправки: {ex}");
            }
        }

        private void SaveMessages()
        {
            var json = JsonConvert.SerializeObject(Messages.ToList());
            Preferences.Set($"Messages_{_recipientId}", json);
        }
    }
}