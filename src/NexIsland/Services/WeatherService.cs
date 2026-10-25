using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace NexIsland.Services
{
    public class WeatherData
    {
        public string City { get; set; } = "Current Location";
        public double TemperatureCelsius { get; set; }
        public double TemperatureFahrenheit => (TemperatureCelsius * 9.0 / 5.0) + 32.0;
        public int WeatherCode { get; set; }
        public string Condition { get; set; } = "Clear";
        public string WeatherEmoji { get; set; } = "☀️";
        public double Humidity { get; set; }
        public double WindSpeedKmh { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public bool IsCached { get; set; }
    }

    public class WeatherService
    {
        private static WeatherService? _instance;
        public static WeatherService Instance => _instance ??= new WeatherService();

        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        private readonly DispatcherTimer _timer;
        private readonly string _cacheFilePath;
        private WeatherData _current = new();

        public WeatherData Current => _current;
        public event Action<WeatherData>? WeatherUpdated;

        public WeatherService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _cacheFilePath = Path.Combine(appData, "NexIsland", "weather_cache.json");
            LoadCache();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(30)
            };
            _timer.Tick += async (s, e) => await RefreshWeatherAsync();
        }

        public void Start()
        {
            _ = RefreshWeatherAsync();
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public async Task RefreshWeatherAsync(double lat = -6.2088, double lon = 106.8456, string cityName = "Jakarta")
        {
            try
            {
                // Open-Meteo free API - no API key required, highly reliable
                var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat:F4}&longitude={lon:F4}&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m";
                var response = await _httpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                if (root.TryGetProperty("current", out var currentElem))
                {
                    double temp = currentElem.GetProperty("temperature_2m").GetDouble();
                    double humidity = currentElem.GetProperty("relative_humidity_2m").GetDouble();
                    int code = currentElem.GetProperty("weather_code").GetInt32();
                    double wind = currentElem.GetProperty("wind_speed_10m").GetDouble();

                    var (condition, emoji) = InterpretWmoCode(code);

                    _current = new WeatherData
                    {
                        City = cityName,
                        TemperatureCelsius = Math.Round(temp, 1),
                        WeatherCode = code,
                        Condition = condition,
                        WeatherEmoji = emoji,
                        Humidity = humidity,
                        WindSpeedKmh = wind,
                        LastUpdated = DateTime.Now,
                        IsCached = false
                    };

                    SaveCache();
                    WeatherUpdated?.Invoke(_current);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Warn($"Could not refresh live weather: {ex.Message}. Falling back to cache.");
                if (_current != null)
                {
                    _current.IsCached = true;
                    WeatherUpdated?.Invoke(_current);
                }
            }
        }

        private static (string Condition, string Emoji) InterpretWmoCode(int code)
        {
            return code switch
            {
                0 => ("Clear Sky", "☀️"),
                1 => ("Mainly Clear", "🌤️"),
                2 => ("Partly Cloudy", "⛅"),
                3 => ("Overcast", "☁️"),
                45 or 48 => ("Foggy", "🌫️"),
                51 or 53 or 55 => ("Drizzle", "🌦️"),
                61 or 63 or 65 => ("Rain", "🌧️"),
                71 or 73 or 75 => ("Snow", "❄️"),
                80 or 81 or 82 => ("Rain Showers", "🌦️"),
                95 or 96 or 99 => ("Thunderstorm", "⛈️"),
                _ => ("Cloudy", "☁️")
            };
        }

        private void LoadCache()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    var json = File.ReadAllText(_cacheFilePath);
                    var cached = JsonSerializer.Deserialize<WeatherData>(json);
                    if (cached != null)
                    {
                        cached.IsCached = true;
                        _current = cached;
                    }
                }
            }
            catch { }
        }

        private void SaveCache()
        {
            try
            {
                var dir = Path.GetDirectoryName(_cacheFilePath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_cacheFilePath, json);
            }
            catch { }
        }
    }
}
