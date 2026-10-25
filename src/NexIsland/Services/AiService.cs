using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NexIsland.Services
{
    public class AiMessage
    {
        public string Role { get; set; } = "user"; // "user", "assistant", "system"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);
        public bool IsAssistant => Role.Equals("assistant", StringComparison.OrdinalIgnoreCase);
        public string FormattedTime => Timestamp.ToString("HH:mm");
    }

    public interface IAiProvider
    {
        string Name { get; }
        bool IsConfigured { get; }
        Task<string> GenerateResponseAsync(string prompt, IEnumerable<AiMessage> history, CancellationToken ct);
    }

    public class OpenAiCompatibleProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;
        private string _apiKey = string.Empty;
        private string _endpoint = "https://api.openai.com/v1/chat/completions";
        private string _model = "gpt-4o-mini";

        public string Name => "Cloud AI (OpenAI / Ollama / Groq / Compatible)";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public OpenAiCompatibleProvider(string endpoint = "https://api.openai.com/v1/chat/completions", string model = "gpt-4o-mini")
        {
            _endpoint = endpoint;
            _model = model;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            LoadEncryptedKey();
        }

        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey.Trim();
            SaveEncryptedKey(_apiKey);
        }

        public void SetEndpointAndModel(string endpoint, string model)
        {
            if (!string.IsNullOrWhiteSpace(endpoint)) _endpoint = endpoint.Trim();
            if (!string.IsNullOrWhiteSpace(model)) _model = model.Trim();
        }

        public async Task<string> GenerateResponseAsync(string prompt, IEnumerable<AiMessage> history, CancellationToken ct)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("AI Provider API Key is not configured. Please set your key in AI settings.");
            }

            var messagesList = new List<object>
            {
                new { role = "system", content = "You are NexIsland AI, a helpful, ultra-concise, intelligent desktop copilot for Windows 10/11. Give crisp, direct, beautifully formatted responses." }
            };

            foreach (var h in history)
            {
                messagesList.Add(new { role = h.Role, content = h.Content });
            }
            messagesList.Add(new { role = "user", content = prompt });

            var payload = new
            {
                model = _model,
                messages = messagesList,
                temperature = 0.7,
                max_tokens = 600
            };

            string json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.SendAsync(req, ct);
            string responseBody = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"API request returned {resp.StatusCode}: {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? "No response received.";
                }
            }

            return "Received unexpected response format from AI provider.";
        }

        private void SaveEncryptedKey(string key)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dir = Path.Combine(appData, "NexIsland");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "ai_vault.dat");

                if (string.IsNullOrEmpty(key))
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }

                byte[] plainBytes = Encoding.UTF8.GetBytes(key);
                byte[] cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(path, cipherBytes);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("Failed to encrypt AI API key", ex);
            }
        }

        private void LoadEncryptedKey()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var path = Path.Combine(appData, "NexIsland", "ai_vault.dat");
                if (File.Exists(path))
                {
                    byte[] cipherBytes = File.ReadAllBytes(path);
                    byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
                    _apiKey = Encoding.UTF8.GetString(plainBytes);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Debug($"Note loading AI key: {ex.Message}");
            }
        }
    }

    public class OfflineFallbackAiProvider : IAiProvider
    {
        public string Name => "NexIsland Smart Copilot (Offline / Rule-based Assistant)";
        public bool IsConfigured => true;

        public Task<string> GenerateResponseAsync(string prompt, IEnumerable<AiMessage> history, CancellationToken ct)
        {
            string lower = prompt.ToLowerInvariant().Trim();

            // Check if prompt is asking for summarization
            if (lower.StartsWith("summarize") || lower.Contains("rangkum") || lower.Contains("ringkas"))
            {
                return Task.FromResult("📋 **AI Summary**:\n• Key Concept: Identified main theme in provided context.\n• Action Item: Focused extraction ready for review.\n• Takeaway: Text structured cleanly for fast reference.");
            }

            // Check if prompt is asking for checklist / todo
            if (lower.Contains("checklist") || lower.Contains("to-do") || lower.Contains("daftar tugas"))
            {
                return Task.FromResult("✅ **Task Checklist**:\n[ ] 1. Review objectives and setup workspace\n[ ] 2. Focus for 25 minutes using Pomodoro mode\n[ ] 3. Take 5-minute break and check agenda\n[ ] 4. Finalize outputs and archive notes");
            }

            // Check if prompt is asking for study / work plan
            if (lower.Contains("study") || lower.Contains("belajar") || lower.Contains("work plan") || lower.Contains("jadwal"))
            {
                return Task.FromResult("🎯 **NexIsland Optimized Schedule**:\n• 08:30 - 09:15: Deep Work Session 1 (Distraction-free Focus)\n• 09:15 - 09:30: Short Break & Hydration\n• 09:30 - 10:15: Deep Work Session 2 (Core deliverables)\n• 10:15 - 10:30: Review and Task wrap-up");
            }

            // Check if prompt is asking for Windows tips / shortcuts
            if (lower.Contains("windows") || lower.Contains("shortcut") || lower.Contains("tips"))
            {
                return Task.FromResult("⚡ **Windows Power Shortcuts**:\n• `Alt + Space` : NexIsland Universal Search\n• `Win + V` : Windows Native Clipboard History\n• `Win + Shift + S` : Snipping Tool Screen Capture\n• `Win + .` : Windows Emoji & Symbol Picker\n• `Win + Tab` : Task View & Virtual Workspaces");
            }

            // Check if prompt is asking for routine creation
            if (lower.Contains("routine") || lower.Contains("otomatisasi"))
            {
                return Task.FromResult("🤖 **Smart Routine Suggestion**:\n• Name: Deep Work Study Mode\n• Trigger: Start of Focus Session\n• Actions: Switch to Focus timer, set Balanced power mode, suppress alerts.\n(You can run this anytime from the Routines module!)");
            }

            // General smart response
            return Task.FromResult($"💡 **NexIsland AI Assistant**:\nI have processed your query: *\"{prompt}\"*.\n\n• For deep natural language answers, you can connect an OpenAI/Ollama API key in AI settings.\n• I can also generate checklists, summarize clipboard text, suggest Windows shortcuts, and plan routines!");
        }
    }

    public class AiService
    {
        private static AiService? _instance;
        public static AiService Instance => _instance ??= new AiService();

        private IAiProvider _activeProvider;
        private readonly OpenAiCompatibleProvider _cloudProvider;
        private readonly OfflineFallbackAiProvider _offlineProvider;

        public ObservableCollection<AiMessage> ConversationHistory { get; } = new();

        public bool IsThinking { get; private set; }

        public event Action<bool>? ThinkingStateChanged;
        public event Action<AiMessage>? MessageAdded;
        public event Action? HistoryCleared;
        public event Action<string>? ErrorOccurred;

        public bool HasCloudKeyConfigured => _cloudProvider.IsConfigured;

        public AiService()
        {
            _cloudProvider = new OpenAiCompatibleProvider();
            _offlineProvider = new OfflineFallbackAiProvider();
            _activeProvider = _cloudProvider.IsConfigured ? _cloudProvider : _offlineProvider;

            // Welcome initial greeting
            ConversationHistory.Add(new AiMessage
            {
                Role = "assistant",
                Content = "👋 Hello! I am your NexIsland AI Copilot. Ask me questions, summarize text, create checklists, or plan routines."
            });
        }

        public void SetApiKey(string key)
        {
            _cloudProvider.SetApiKey(key);
            _activeProvider = _cloudProvider.IsConfigured ? _cloudProvider : _offlineProvider;
        }

        public async Task<string> AskAsync(string prompt, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return string.Empty;

            var userMsg = new AiMessage { Role = "user", Content = prompt.Trim() };
            ConversationHistory.Add(userMsg);
            MessageAdded?.Invoke(userMsg);

            IsThinking = true;
            ThinkingStateChanged?.Invoke(true);

            try
            {
                // Select provider based on configuration
                var provider = _cloudProvider.IsConfigured ? (IAiProvider)_cloudProvider : _offlineProvider;
                string replyText = await provider.GenerateResponseAsync(prompt, ConversationHistory, ct);

                var assistantMsg = new AiMessage { Role = "assistant", Content = replyText };
                ConversationHistory.Add(assistantMsg);
                MessageAdded?.Invoke(assistantMsg);
                return replyText;
            }
            catch (Exception ex)
            {
                string err = $"AI Error: {ex.Message}";
                var errMsg = new AiMessage { Role = "assistant", Content = $"⚠️ {err}" };
                ConversationHistory.Add(errMsg);
                MessageAdded?.Invoke(errMsg);
                ErrorOccurred?.Invoke(err);
                return err;
            }
            finally
            {
                IsThinking = false;
                ThinkingStateChanged?.Invoke(false);
            }
        }

        public void ClearHistory()
        {
            ConversationHistory.Clear();
            ConversationHistory.Add(new AiMessage
            {
                Role = "assistant",
                Content = "Conversation cleared. How can I assist you today?"
            });
            HistoryCleared?.Invoke();
        }
    }
}
