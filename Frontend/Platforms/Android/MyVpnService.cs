

using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using Android.Util;
using Java.IO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SOE.Platforms.Android
{
    [Service(Permission = "android.permission.BIND_VPN_SERVICE")]
    public class MyVpnService : VpnService
    {
        private const string TAG = "MyVpnService";
        private ParcelFileDescriptor _vpnInterface;
        private Thread _vpnThread;
        private Thread _messagePollingThread;
        private bool _isRunning;
        private string _deviceId;
        private HttpClient _httpClient;

        public override void OnCreate()
        {
            base.OnCreate();
            _deviceId = Preferences.Get("DeviceId", Guid.NewGuid().ToString());
            _httpClient = new HttpClient();
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (!_isRunning)
            {
                _isRunning = true;
                StartVpn();

                // Поток для обработки VPN трафика
                _vpnThread = new Thread(VpnWorker) { IsBackground = true };
                _vpnThread.Start();

                // Поток для опроса сообщений
                _messagePollingThread = new Thread(MessagePollingWorker) { IsBackground = true };
                _messagePollingThread.Start();
            }
            return StartCommandResult.Sticky;
        }

        private void StartVpn()
        {
            var builder = new Builder(this)
                .SetSession("SecureChatVPN")
                .AddAddress("10.8.0.2", 24)
                .AddRoute("89.223.68.177", 32) // IP вашего API
                .AddDnsServer("8.8.8.8")
                .SetMtu(1500)
                .SetBlocking(true);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                builder.SetUnderlyingNetworks(null);
            }

            _vpnInterface = builder.Establish();
        }

        private void VpnWorker()
        {
            using var input = new FileInputStream(_vpnInterface.FileDescriptor);
            using var output = new FileOutputStream(_vpnInterface.FileDescriptor);

            byte[] buffer = new byte[32768];
            while (_isRunning)
            {
                try
                {
                    int length = input.Read(buffer);
                    if (length > 0)
                    {
                        var packet = ParsePacket(buffer, length);
                        if (IsAllowedHost(packet.DestinationAddress))
                        {
                            output.Write(buffer, 0, length);
                            Log.Debug(TAG, $"Разрешен трафик к: {packet.DestinationAddress}");
                        }
                        else
                        {
                            Log.Debug("VPN", $"Блокировка трафика к {packet.DestinationAddress}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(TAG, $"Ошибка в VpnWorker: {ex}");
                }
            }
        }

        private async void MessagePollingWorker()
        {
            while (true)
            {
                try
                {
                    var response = await _httpClient.GetStringAsync(
                        $"https://retality-education-retality-messenger-48ce.twc1.net/api/messages/{_deviceId}");

                    if (!string.IsNullOrEmpty(response))
                    {
                         var messages = JsonConvert.DeserializeObject<SOE.Models.Message>(response);
                        MessagingCenter.Send(this, "NewMessage", messages);

                    }
                }
                catch (Exception ex)
                {
                    Log.Error(TAG, $"Ошибка опроса сообщений: {ex}");
                }

                Thread.Sleep(2000); // Интервал опроса - 2 секунды
            }
        }

        private (string SourceAddress, string DestinationAddress, int SourcePort, int DestinationPort, byte[] Payload) ParsePacket(byte[] buffer, int length)
        {
            try
            {
                // Анализ IP-заголовка (первые 20 байт)
                byte protocol = buffer[9]; // Протокол (TCP=6, UDP=17)
                string sourceIP = new IPAddress(BitConverter.ToUInt32(buffer, 12)).ToString();
                string destIP = new IPAddress(BitConverter.ToUInt32(buffer, 16)).ToString();

                // Анализ TCP/UDP заголовков
                int headerLength = (buffer[0] & 0x0F) * 4;
                int sourcePort = 0;
                int destPort = 0;

                if (protocol == 6 && length >= headerLength + 20) // TCP
                {
                    sourcePort = (buffer[headerLength] << 8) | buffer[headerLength + 1];
                    destPort = (buffer[headerLength + 2] << 8) | buffer[headerLength + 3];
                }
                else if (protocol == 17 && length >= headerLength + 8) // UDP
                {
                    sourcePort = (buffer[headerLength] << 8) | buffer[headerLength + 1];
                    destPort = (buffer[headerLength + 2] << 8) | buffer[headerLength + 3];
                }

                // Выделяем payload (данные)
                byte[] payload = new byte[length - headerLength];
                Buffer.BlockCopy(buffer, headerLength, payload, 0, payload.Length);

                return (sourceIP, destIP, sourcePort, destPort, payload);
            }
            catch
            {
                return (null, null, 0, 0, null);
            }
        }

        private bool IsAllowedHost(string ipOrHost)
        {
            var allowedHosts = new[]
            {
                "retality-education-retality-messenger-48ce.twc1.net",
                "89.223.68.177",
                "2.9.162.213",
                "116.35.237.66"
            };

            return allowedHosts.Any(host =>
                host.Equals(ipOrHost, StringComparison.OrdinalIgnoreCase) ||
                (IPAddress.TryParse(ipOrHost, out var ip) &&
                Dns.GetHostAddresses(allowedHosts[0]).Any(addr => addr.Equals(ip))));
        }

        public override void OnDestroy()
        {
            _isRunning = false;

            try
            {
                _vpnThread?.Join(1000);
                _messagePollingThread?.Join(1000);
                _vpnInterface?.Close();
                _httpClient?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Ошибка при остановке: {ex}");
            }

            base.OnDestroy();
        }
    }
}