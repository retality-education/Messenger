using Newtonsoft.Json;
using SOE.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Contact = SOE.Models.Contact;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if ANDROID
using AndroidX.Activity.Result.Contract;
using SOE.Platforms.Android;
using Xamarin.Android;
using Android.App;
using AndroidX.Activity.Result;
using AndroidX.Activity;
#endif

namespace SOE.Views
{
    public partial class MainPage : ContentPage
    {
        public ObservableCollection<Contact> Contacts { get; } = new();
        private readonly HttpClient _httpClient = new();
        private string _deviceId;
        private string _currentChatRecipientId;

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;

            _deviceId = Preferences.Get("DeviceId", Guid.NewGuid().ToString());
            Debug.WriteLine($"Device ID: {_deviceId}");
            Preferences.Set("DeviceId", _deviceId);

            LoadContacts();

#if ANDROID
            MessagingCenter.Subscribe<MyVpnService, Message>(this, "NewMessage", (sender, msg) =>
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    ProcessNewMessage(msg);
                    
                    // Если сообщение от текущего собеседника, обновляем чат
                    if (_currentChatRecipientId == msg.From)
                    {
                        MessagingCenter.Send(this, "UpdateChat", msg);
                    }
                });
            });
#endif
        }

        private void StartVpnClicked(object sender, EventArgs e)
        {
#if ANDROID
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var intent = Android.Net.VpnService.Prepare(Platform.CurrentActivity);
                if (intent != null)
                {
                    Platform.CurrentActivity.StartActivityForResult(intent, 0);
                }
                else
                {
                    Platform.CurrentActivity.StartService(new Android.Content.Intent(Platform.CurrentActivity, typeof(MyVpnService)));
                }
            }
#else
            DisplayAlert("Not Supported", "VPN functionality is only available on Android", "OK");
#endif
        }

        private void LoadContacts()
        {
            var savedContacts = Preferences.Get("Contacts", "[]");
            var contacts = JsonConvert.DeserializeObject<List<Contact>>(savedContacts) ?? new List<Contact>();

            Contacts.Clear();
            foreach (var contact in contacts.OrderByDescending(c => c.LastMessageDate))
            {
                Contacts.Add(contact);
            }
        }

        private void ProcessNewMessage(Message message)
        {
            // Находим или создаем контакт
            var contact = Contacts.FirstOrDefault(c => c.Id == message.From);
            if (contact == null)
            {
                contact = new Contact
                {
                    Id = message.From,
                    Nickname = $"User {message.From.Substring(0, Math.Min(4, message.From.Length))}",
                    LastMessage = message.Content,
                    LastMessageDate = message.Date
                };
                Contacts.Add(contact);
            }
            else
            {
                contact.LastMessage = message.Content;
                contact.LastMessageDate = message.Date;
            }

            // Сохраняем сообщение в историю
            SaveMessageToHistory(message);

            // Сохраняем и сортируем контакты
            SaveAndSortContacts();
        }

        private void SaveMessageToHistory(Message message)
        {
            var chatMessage = new ChatMessage
            {
                Content = message.Content,
                Date = message.Date,
                IsIncoming = true
            };

            // Загружаем текущие сообщения
            var messagesKey = $"Messages_{message.From}";
            var savedMessages = Preferences.Get(messagesKey, "[]");
            var messages = JsonConvert.DeserializeObject<List<ChatMessage>>(savedMessages) ?? new List<ChatMessage>();

            // Добавляем новое сообщение
            messages.Add(chatMessage);

            // Сохраняем обратно
            Preferences.Set(messagesKey, JsonConvert.SerializeObject(messages));
        }

        private void SaveAndSortContacts()
        {
            var sorted = Contacts.OrderByDescending(c => c.LastMessageDate).ToList();
            Contacts.Clear();
            foreach (var item in sorted)
            {
                Contacts.Add(item);
            }
            SaveContacts();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadContacts();
            _currentChatRecipientId = null;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }

        private async void OnContactSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is Contact selectedContact)
            {
                _currentChatRecipientId = selectedContact.Id;
                await Navigation.PushAsync(new ChatPage(selectedContact.Id));
                ContactsList.SelectedItem = null;
            }
        }

        private async void OnStartNewChatClicked(object sender, EventArgs e)
        {
            string contactId = NewContactIdEntry.Text?.Trim();
            if (string.IsNullOrEmpty(contactId))
            {
                await DisplayAlert("Ошибка", "Введите ID пользователя", "OK");
                return;
            }

            var existingContact = Contacts.FirstOrDefault(c => c.Id == contactId);
            if (existingContact == null)
            {
                existingContact = new Contact
                {
                    Id = contactId,
                    Nickname = $"User {contactId.Substring(0, Math.Min(4, contactId.Length))}"
                };
                Contacts.Add(existingContact);
                SaveContacts();
            }

            _currentChatRecipientId = contactId;
            await Navigation.PushAsync(new ChatPage(contactId));
            NewContactIdEntry.Text = string.Empty;
        }

        private void SaveContacts()
        {
            var json = JsonConvert.SerializeObject(Contacts.ToList());
            Preferences.Set("Contacts", json);
        }
    }
}