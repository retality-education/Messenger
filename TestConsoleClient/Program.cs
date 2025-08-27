using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

class Program
{
    private static string _token;
    private static string _currentChatId;
    private static HttpClient _httpClient = new HttpClient();

    static async Task Main(string[] args)
    {
        Console.WriteLine("Тестовый клиент мессенджера");
        Console.WriteLine("--------------------------\n");

        // Базовый URL API (должен совпадать с вашим сервером)
        var baseUrl = "http://localhost:5000";

        // 1. Аутентификация пользователя
        Console.WriteLine("1. Регистрация");
        Console.WriteLine("2. Вход");
        Console.Write("Выберите действие: ");
        var authChoice = Console.ReadLine();

        try
        {
            if (authChoice == "1")
            {
                // Регистрация
                Console.Write("Имя пользователя: ");
                var username = Console.ReadLine();
                Console.Write("Email: ");
                var email = Console.ReadLine();
                Console.Write("Пароль: ");
                var password = Console.ReadLine();

                var response = await _httpClient.PostAsJsonAsync(
                    $"{baseUrl}/api/auth/register",
                    new { username, email, password });

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Ошибка регистрации: {await response.Content.ReadAsStringAsync()}");
                    return;
                }

                var user = await response.Content.ReadFromJsonAsync<User>();
                Console.WriteLine($"Зарегистрирован пользователь: {user.Username}");
            }

            // Вход (для всех случаев)
            Console.WriteLine("\nВход в систему:");
            Console.Write("Имя пользователя: ");
            var loginUsername = Console.ReadLine();
            Console.Write("Пароль: ");
            var loginPassword = Console.ReadLine();

            var loginResponse = await _httpClient.PostAsJsonAsync(
                $"{baseUrl}/api/auth/login",
                new { username = loginUsername, password = loginPassword });

            if (!loginResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Ошибка входа: {await loginResponse.Content.ReadAsStringAsync()}");
                return;
            }

            var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            _token = authResult.Token;
            Console.WriteLine($"\nДобро пожаловать, {authResult.User.Username}!");

            // 2. Настройка подключения к SignalR
            var connection = new HubConnectionBuilder()
                .WithUrl($"{baseUrl}/chatHub", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_token);
                })
                .WithAutomaticReconnect()
                .Build();

            // 3. Обработчики событий чата
            connection.On<string>("UserJoined", username =>
            {
                if (username != authResult.User.Username)
                    Console.WriteLine($"[ЧАТ] {username} присоединился");
            });

            connection.On<string>("UserLeft", username =>
            {
                if (username != authResult.User.Username)
                    Console.WriteLine($"[ЧАТ] {username} покинул чат");
            });

            connection.On<ChatMessage>("ReceiveMessage", message =>
            {
                var prefix = message.UserId == authResult.User.Id ? "Вы" : message.Username;
                Console.WriteLine($"[{message.Timestamp:HH:mm}] {prefix}: {message.Text}");
            });

            connection.On<object>("ChatMood", mood =>
            {
                Console.WriteLine($"\n[НАСТРОЕНИЕ ЧАТА] {mood}");
            });

            // 4. Подключение к хабу
            await connection.StartAsync();
            Console.WriteLine("\nСоединение с чатом установлено!");

            // 5. Главный цикл приложения
            while (true)
            {
                Console.WriteLine("\nМЕНЮ:");
                Console.WriteLine("1. Создать чат");
                Console.WriteLine("2. Присоединиться к чату");
                Console.WriteLine("3. Отправить сообщение");
                Console.WriteLine("4. Покинуть чат");
                Console.WriteLine("5. Мои чаты");
                Console.WriteLine("0. Выход");
                Console.Write("Выберите действие: ");

                switch (Console.ReadLine())
                {
                    case "1":
                        await CreateChat(connection);
                        break;

                    case "2":
                        await JoinChat(connection);
                        break;

                    case "3":
                        await SendMessage(connection);
                        break;

                    case "4":
                        await LeaveChat(connection);
                        break;

                    case "5":
                        await ListMyChats(connection);
                        break;

                    case "0":
                        return;

                    default:
                        Console.WriteLine("Неверный ввод");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private static async Task CreateChat(HubConnection connection)
    {
        Console.Write("Название чата: ");
        var name = Console.ReadLine();

        _currentChatId = await connection.InvokeAsync<string>("CreateChat", name);
        Console.WriteLine($"\nЧат создан! ID: {_currentChatId}");
    }

    private static async Task JoinChat(HubConnection connection)
    {
        Console.Write("ID чата: ");
        _currentChatId = Console.ReadLine();

        var isConnected = await connection.InvokeAsync<bool>("JoinChat", _currentChatId);

        if (isConnected)
        {
            // Загрузка истории сообщений
            var messages = await connection.InvokeAsync<List<ChatMessage>>("LoadHistory", _currentChatId, 0, 100);

            Console.WriteLine($"\nИстория чата {_currentChatId}:");
            foreach (var msg in messages.OrderBy(m => m.Timestamp))
            {
                Console.WriteLine($"[{msg.Timestamp:HH:mm}] {msg.Username}: {msg.Text}");
            }

            Console.WriteLine($"\nВы в чате {_currentChatId}\n");
        }
        else
        {
            Console.WriteLine("Не удалось присоединиться к чату");
        }
    }

    private static async Task SendMessage(HubConnection connection)
    {
        if (string.IsNullOrEmpty(_currentChatId))
        {
            Console.WriteLine("Сначала присоединитесь к чату");
            return;
        }

        Console.Write("Сообщение: ");
        var text = Console.ReadLine();

        try
        {
            var result = await connection.InvokeAsync<SendMessageResult>(
                "SendMessage",
                _currentChatId,
                text
            );

            if (result != null)
            {
                Console.WriteLine($"Подсказка: {result.Hint}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка отправки: {ex.Message}");
        }
    }

    private static async Task LeaveChat(HubConnection connection)
    {
        if (string.IsNullOrEmpty(_currentChatId))
        {
            Console.WriteLine("Вы не в чате");
            return;
        }

        await connection.InvokeAsync("LeaveChat", _currentChatId);
        Console.WriteLine("Вы покинули чат");
        _currentChatId = null;
    }

    private static async Task ListMyChats(HubConnection connection)
    {
        var chats = await connection.InvokeAsync<List<ChatSummary>>("GetMyChats");

        Console.WriteLine("\nВАШИ ЧАТЫ:");
        foreach (var chat in chats)
        {
            Console.WriteLine($"[{chat.ChatId}] {chat.ChatName}");
            Console.WriteLine($"Последнее сообщение: {chat.LastMessagePreview ?? "нет"}");
            Console.WriteLine($"Время: {chat.LastMessageTime:g}\n");
        }
    }
}

// Модели данных для клиента
public class User
{
    public string Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuthResponse
{
    public string Token { get; set; }
    public User User { get; set; }
}

public class ChatMessage
{
    public Guid Id { get; set; }
    public string ChatId { get; set; }
    public string UserId { get; set; }
    public string Username { get; set; }
    public string Text { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ChatSummary
{
    public string ChatId { get; set; }
    public string ChatName { get; set; }
    public string LastMessagePreview { get; set; }
    public DateTime LastMessageTime { get; set; }
}

public class SendMessageResult
{
    public bool IsPositive { get; set; }
    public string Hint { get; set; }
}