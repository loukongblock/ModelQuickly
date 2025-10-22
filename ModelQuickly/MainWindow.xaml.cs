using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using Ookii.Dialogs.Wpf;
using System.Net.Http.Json;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json;
using System.Threading;
using File = System.IO.File;
using Path = System.IO.Path;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;
using static System.Net.WebRequestMethods;
using System.Text.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.Net.Security;
using System.Net;
using System.Net.NetworkInformation;
using System.IO.Compression;
using System.Linq.Expressions;
using System.Globalization;
using System.Security.Principal;
using System.Security.Policy;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Media.Media3D;
using HtmlAgilityPack;
using System.Net.Http.Headers;
using UglyToad.PdfPig;
using DOCX = DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig.Tokenization;
using System.Text.RegularExpressions;
using System.Data;
using System.Windows.Media.Animation;
using System.Windows.Controls.Primitives;
using UglyToad.PdfPig.Graphics.Operations.TextObjects;
using DocumentFormat.OpenXml.VariantTypes;
using ModelQuickly;

namespace ModelQuickly
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private HttpClient _httpClient;
        private bool _memoryEnabled = false;
        private readonly List<ChatMessage> _history;
        private readonly List<ChatMessage> _historyLite = new List<ChatMessage>();

        private List<ChatSession> _sessions;
        private ChatSession _current;
        public static bool IsRunningAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        public static void RestartAsAdmin()
        {
            if (!IsRunningAsAdmin())
            {
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    Verb = "runas",
                    FileName = Process.GetCurrentProcess().MainModule.FileName,
                    Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1))
                };
                Process.Start(processInfo);
                Environment.Exit(0);
            }
        }
        public class ModelPath
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Modle { get; set; }
        }
        private readonly string _cfgPath = Path.Combine(
    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
    "config.json");
        private Config _cfg;


        public class Config
        {
            public bool jiyi { get; set; } = true;
            public double wendu { get; set; } = 0.7;
            public int shangxiawen { get; set; } = 1;
            public bool zhedie { get; set; } = false;
            public bool netserach { get; set; } = false;
            public int keepRounds { get; set; } = 60;
            public double chengfa { get; set; } = 1.1;
            public string renshe { get; set; } = "你是一个AI模型，专注于回答用户的问题";
        }

        public class ChatSession
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public List<ChatMessage> Messages { get; set; }

            public ChatSession()
            {
                Messages = new List<ChatMessage>();
            }
        }

        // 会话仓库
        internal static class ChatSessionStorage
        {
            private static readonly string FilePath =
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sessions.json");

            public static List<ChatSession> Load()
            {
                if (!File.Exists(FilePath))
                    return new List<ChatSession>();

                string json = File.ReadAllText(FilePath);
                List<ChatSession> list =
                    JsonConvert.DeserializeObject<List<ChatSession>>(json);
                return list ?? new List<ChatSession>();
            }

            public static void Save(IEnumerable<ChatSession> list)
            {
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
        }

        internal static class ModelStorage
        {
            private static readonly string CentralJson =
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modelpath.json");

            public static List<ModelPath> Load()
            {
                if (!File.Exists(CentralJson)) return new List<ModelPath>();
                return JsonConvert.DeserializeObject<List<ModelPath>>(File.ReadAllText(CentralJson))
                       ?? new List<ModelPath>();
            }

            public static void Save(IEnumerable<ModelPath> list)
            {
                File.WriteAllText(CentralJson,
                    JsonConvert.SerializeObject(list, Formatting.Indented));
            }
        }

        private static readonly HttpClient http = new HttpClient();
        private static readonly string HistoryPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chat_history.json");
        public enum BackendType
        {
            LlamaCpp,   // http://localhost:8080  /v1/chat/completions
        }

        private static readonly HttpClient client = new HttpClient();

        private string CurrentModelName = "AI";
        private string ModelName = "AI";


        private void LoadOrCreateConfig()
        {
            if (!File.Exists(_cfgPath))
            {
                _cfg = new Config();
                SaveConfig();
            }
            else
            {
                var json = File.ReadAllText(_cfgPath);
                _cfg = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
        }
        private void SaveConfig()
        {
            var json = JsonConvert.SerializeObject(_cfg, Formatting.Indented);
            File.WriteAllText(_cfgPath, json);
        }

        private void LoadConfigToControls()
        {
            if (!File.Exists(_cfgPath))
            {
                _cfg = new Config();
                SaveConfig();
            }
            else
            {
                var json = File.ReadAllText(_cfgPath);
                _cfg = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }

            renshebianxie.Text = _cfg.renshe;
            if (_cfg.jiyi == false)
            {
                isen.HorizontalAlignment = HorizontalAlignment.Left;
                _memoryEnabled = false;
                openjiyibaocun.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2f2f2f"));
            }
            else
            {
                openjiyibaocun.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00b96b"));
                isen.HorizontalAlignment = HorizontalAlignment.Right;
                _memoryEnabled = true;
            }
            wendu.Text = _cfg.wendu.ToString(CultureInfo.InvariantCulture);
            chengfa.Text = _cfg.chengfa.ToString(CultureInfo.InvariantCulture);
            lunshu.Text = _cfg.keepRounds.ToString(CultureInfo.InvariantCulture);
            shangxiawen.Text = _cfg.shangxiawen.ToString(CultureInfo.InvariantCulture);
        }
        private const string MutexName = "Global\\ModelQuickly";




        private double originalBottomMargin = 20;
        private double minHeight = 130;
        private readonly CancellationTokenSource _cts2 = new CancellationTokenSource();
        private StringBuilder _deltaSb = new StringBuilder();
        private TextBox _ansBox;          // 正在流式输出的 TextBox
        public MainWindow()
        {
            IsRunningAsAdmin();
            RestartAsAdmin();
            InitializeComponent();
            input.KeyDown += input_KeyDown;
            Loaded += OnWindowLoaded;
            resizeThumb.DragDelta += ResizeThumb_DragDelta;
            LoadOrCreateConfig();
            LoadConfigToControls();
            LoadAvatarFromConfig();
            _sendOriginalBrush = send.Background;
            if(_cfg.netserach == true)
            {
                 netserach.Background = (Brush)new BrushConverter().ConvertFrom("#FF2F2F2F");
            }
            else
            {
                netserach.Background = (Brush)new BrushConverter().ConvertFrom("#FF1F1F1F");
            }
            _sessions = ChatSessionStorage.Load();
            if (_sessions.Count == 0)
            {
                ChatSession def = new ChatSession();
                def.Id = Guid.NewGuid().ToString();
                def.Title = "默认对话";
                _sessions.Add(def);
            }
            string oldFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                          "chat_history.json");
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            if (File.Exists(oldFile))
            {
                List<ChatMessage> old = LoadHistory();
                _sessions[0].Messages.AddRange(old);
                File.Delete(oldFile);
                ChatSessionStorage.Save(_sessions);
            }

            RenderChatList();
            SwitchSession(_sessions[0]);

            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

            _history = LoadHistory();
            foreach (var m in PersistentModelList.Load())
                CreateModelCard(m.Name, m.Path, m.Modle);

            foreach (var m in _history)
            {
                string senderName = m.Role == "user" ? "我" : m.Assistant;
                AddMessageToUI(senderName, m.Text, m.Role == "user", m.Model); // Model 已非空
            }

        }
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double h = inputBorder.Height - e.VerticalChange;
            h = Math.Max(130, Math.Min(300, h));
            inputBorder.Height = h;

            // 1. 提示条跟着走
            tbborder.Margin = new Thickness(0, 0, 15, 20 + h + 10);

            // 2. 同步压缩 ScrollViewer：基础 170 + 相对 130 的增量
            ChatScrollViewer.Margin = new Thickness(280, 0, 0, 170 + (h - 130));
        }
        private void RenderChatList()
        {
            chatlist.Children.Clear();

            foreach (ChatSession s in _sessions)
            {
                Border card = new Border();
                card.Width = 250;
                card.Height = 35;
                card.CornerRadius = new CornerRadius(18);
                card.Background = Brushes.Transparent;
                card.Cursor = Cursors.Hand;
                card.Margin = new Thickness(0, 5, 0, 0);
                card.Tag = s.Id;
                Grid g = new Grid();

                Image icon = new Image();
                icon.Source = new BitmapImage(
                    new Uri("pack://application:,,,/chat.png"));
                icon.Width = 22;
                icon.Height = 22;
                icon.HorizontalAlignment = HorizontalAlignment.Left;
                icon.Margin = new Thickness(10, 0, 0, 0);
                RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.Fant);
                g.Children.Add(icon);

                TextBlock title = new TextBlock();
                title.Text = s.Title;
                title.Foreground = Brushes.White;
                title.FontSize = 13;
                title.VerticalAlignment = VerticalAlignment.Center;
                title.Margin = new Thickness(40, 0, 0, 0);
                g.Children.Add(title);
                Image delImg = new Image();
                delImg.Source = new BitmapImage(
                    new Uri("pack://application:,,,/delet.png"));
                delImg.Width = 16;
                delImg.Height = 16;

                Border delBtn = new Border();
                delBtn.Width = 30;
                delBtn.Height = 30;
                delBtn.HorizontalAlignment = HorizontalAlignment.Right;
                delBtn.VerticalAlignment = VerticalAlignment.Center;
                delBtn.Margin = new Thickness(0, 0, 8, 0);
                delBtn.Background = Brushes.Transparent;
                delBtn.BorderThickness = new Thickness(0);
                delBtn.Cursor = Cursors.Hand;
                delBtn.Child = delImg;
                delBtn.Visibility = Visibility.Collapsed;
                RenderOptions.SetBitmapScalingMode(delBtn, BitmapScalingMode.Fant);
                delBtn.Tag = s.Id;
                g.Children.Add(delBtn);

                /* ---------- 事件 ---------- */

                // 鼠标进入卡片：显示删除图标
                card.MouseEnter += (sender, e) =>
                {
                    delBtn.Visibility = Visibility.Visible;
                };

                // 鼠标离开卡片：隐藏删除图标
                card.MouseLeave += (sender, e) =>
                {
                    // 如果是当前会话，保持显示
                    if (_current != null && _current.Id == (string)card.Tag)
                        delBtn.Visibility = Visibility.Visible;
                    else
                        delBtn.Visibility = Visibility.Collapsed;
                };

                // 单击卡片：切换会话
                card.MouseLeftButtonDown += (sender, e) =>
                {
                    SwitchSession(s);
                };

                // 单击删除图标：删除会话
                delBtn.MouseLeftButtonDown += (sender, e) =>
                {
                    e.Handled = true;   // 阻止卡片点击事件
                    DeleteSession(s);

                };

                card.Child = g;
                chatlist.Children.Add(card);
            }
        }

        private void DeleteSession(ChatSession toDelete)
        {
            _sessions.Remove(toDelete);

            if (_sessions.Count == 0)
            {
                ChatSession def = new ChatSession();
                def.Id = Guid.NewGuid().ToString();
                def.Title = "默认对话";
                _sessions.Add(def);
            }

            ChatSessionStorage.Save(_sessions);
            RenderChatList();
            SwitchSession(_sessions[0]);
        }

        private void SwitchSession(ChatSession session)
        {
            _current = session;

            // 高亮
            foreach (UIElement ele in chatlist.Children)
            {
                Border b = ele as Border;
                if (b != null) b.Background = Brushes.Transparent;
            }
            foreach (UIElement ele in chatlist.Children)
            {
                Border b = ele as Border;
                if (b != null && ((string)b.Tag) == session.Id)
                {
                    b.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#252525"));
                    break;
                }
            }

            // 清空右侧并加载消息
            MessagesPanel.Children.Clear();
            foreach (ChatMessage m in session.Messages)
            {
                AddMessageToUI(
                    m.Role == "user" ? "我" : m.Assistant,
                    m.Text,
                    m.Role == "user",
                    m.Model);
            }
        }
        public bool isopenzanzhu = false;
        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            //var psi = new ProcessStartInfo
            //{
            //    FileName = "powershell",
            //    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"& { Stop-Process -Name 'llama-server' -Force -ErrorAction SilentlyContinue }\"",
            //    CreateNoWindow = true,
            //    UseShellExecute = false
            //};

            //using (var proc = Process.Start(psi))
            //{
            //    proc?.WaitForExit(); // 等待 PowerShell 脚本执行完毕
            //}
            var checker = new VersionChecker();
            _updateUrl = await checker.GetUpdateUrlAsync();
            string remoteVer = await checker.GetVersionAsync() ?? "1.0.2";

            if (remoteVer != "1.0.2")          // 字符串直接比
            {
                update.Visibility = Visibility.Visible;
                return;
            }

        }

        private void OpenWebPage(string url)
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true // 
            });
        }
        private static List<ChatMessage> LoadHistory()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chat_history.json");
            if (!File.Exists(path)) return new List<ChatMessage>();

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                return JsonConvert.DeserializeObject<List<ChatMessage>>(json) ?? new List<ChatMessage>();
            }
            catch
            {
                return new List<ChatMessage>();
            }
        }

        private static void SaveHistory(List<ChatMessage> history)
        {
            var json = JsonConvert.SerializeObject(history, Formatting.Indented);
            File.WriteAllText(HistoryPath, json, Encoding.UTF8);
        }

        public class VersionResponse
        {
            public string version { get; set; }
        }
        public class OpenzanzhuResponse
        {
            public string openzanzhu { get; set; }
        }
        public class VersionChecker
        {
            private readonly HttpClient _httpClient;
            private readonly string _apiUrl = "https://loukongblock.github.io/ModelQuickly/update.json";

            // 新增方法，放到 VersionChecker 类里
            public async Task<bool> IsOpenzanzhuEnabledAsync()
            {
                try
                {
                    var json = await _httpClient.GetStringAsync(_apiUrl);
                    using (var doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("openzanzhu", out var elem))
                        {
                            // 只要字符串不是 "false" 就认为开启
                            return !string.Equals(elem.GetString(), "false",
                                                 StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"获取赞助开关失败：{ex.Message}");
                }
                return false;   // 默认关
            }
            public VersionChecker()
            {
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                _httpClient.DefaultRequestHeaders.Referrer =
                    new Uri("https://loukongblock.github.io/ModelQuickly/");
                var handler = new HttpClientHandler
                {

                };
                _httpClient = new HttpClient(handler);
            }

            // 在 VersionChecker 类里追加
            public async Task<string> GetUpdateUrlAsync()
            {
                try
                {
                    var json = await _httpClient.GetStringAsync(_apiUrl);
                    using (var doc = JsonDocument.Parse(json))
                        if (doc.RootElement.TryGetProperty("updateurl", out var elem))
                            return elem.GetString();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"get updateurl fail: {ex.Message}");
                }
                return null;
            }

            public async Task<string> GetVersionAsync()
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                );
                _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://loukongblock.github.io/ModelQuickly/");
                try
                {
                    // 发送GET请求
                    var response = await _httpClient.GetAsync(_apiUrl);

                    // 确保响应成功
                    response.EnsureSuccessStatusCode();

                    // 读取响应内容
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 解析JSON
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true // 忽略属性名大小写
                    };
                    var versionInfo = JsonSerializer.Deserialize<VersionResponse>(jsonString, options);

                    return versionInfo?.version;
                }
                catch (HttpRequestException ex)
                {
                    // 处理网络请求异常
                    Console.WriteLine($"请求错误: {ex.Message}");
                    return null;
                }
                catch (JsonException ex)
                {
                    // 处理JSON解析异常
                    Console.WriteLine($"JSON解析错误: {ex.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    // 处理其他异常
                    Console.WriteLine($"发生错误: {ex.Message}");
                    return null;
                }
            }
        }


        public static async Task<string> ReceiveCompletionAsync(HttpResponseMessage response)
        {
            while (!Process.GetProcessesByName("llama-server").Any())
            {
                await Task.Delay(1000);
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            using (var doc = JsonDocument.Parse(json))
            {
                if (doc.RootElement.TryGetProperty("content", out var c))
                {
                    return c.GetString()?.Trim() ?? string.Empty;
                }
                return string.Empty;
            }
        }


        public class ChatMessage
        {
            public string Role { get; set; }
            public string Assistant { get; set; }
            public string Model { get; set; }
            public string Text { get; set; }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                grid.Margin = new Thickness(7);
                minwindow.Visibility = Visibility.Visible;
                maxwindow.Visibility = Visibility.Collapsed;
            }
            if (WindowState == WindowState.Normal)
            {
                grid.Margin = new Thickness(0);
                minwindow.Visibility = Visibility.Collapsed;
                maxwindow.Visibility = Visibility.Visible;
            }
        }
        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
            }
        }

        private void Border_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Colors.Transparent);
            }
        }

        private void qution_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            opcity04.Visibility = Visibility.Visible;
            powerby.Visibility = Visibility.Visible;
        }

        private void setting_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            bushu.BorderBrush = new SolidColorBrush(Colors.Transparent);
            bushu.Background = new SolidColorBrush(Colors.Transparent);
            fastbushu.Visibility = Visibility.Collapsed;
            qution.BorderBrush = new SolidColorBrush(Colors.Transparent); ;
            qution.Background = new SolidColorBrush(Colors.Transparent); ;
            setting.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2f2f2f"));
            setting.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
            qution.Background = new SolidColorBrush(Colors.Transparent);
            qution.BorderBrush = new SolidColorBrush(Colors.Transparent);
            chat.BorderBrush = new SolidColorBrush(Colors.Transparent);
            chat.Background = new SolidColorBrush(Colors.Transparent);
        }

        private void chat_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            gerenyemian.Visibility = Visibility.Collapsed;
            bushu.BorderBrush = new SolidColorBrush(Colors.Transparent);
            bushu.Background = new SolidColorBrush(Colors.Transparent);
            fastbushu.Visibility = Visibility.Collapsed;
            qution.BorderBrush = new SolidColorBrush(Colors.Transparent); ;
            qution.Background = new SolidColorBrush(Colors.Transparent); ;
            setting.BorderBrush = new SolidColorBrush(Colors.Transparent);
            setting.Background = new SolidColorBrush(Colors.Transparent);
            qution.Background = new SolidColorBrush(Colors.Transparent);
            qution.BorderBrush = new SolidColorBrush(Colors.Transparent);
            chat.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2f2f2f"));
            chat.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
        }


        private static readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:8080"),
            Timeout = Timeout.InfiniteTimeSpan
        };

        /// <summary>
        /// 把用户本次上传的文件转成一段「AI 看得懂」的纯文本摘要
        /// </summary>
        private async Task<string> BuildAttachmentSummaryAsync(List<object> files)
        {
            if (files == null || files.Count == 0) return string.Empty;

            var sb = new StringBuilder("【用户上传内容摘要】\n");
            foreach (var f in files)
            {
                switch (f)
                {
                    case BitmapImage img:
                        sb.AppendLine($"[图片] 宽度={img.PixelWidth} 高度={img.PixelHeight}");
                        break;

                    case string path when File.Exists(path):
                        string ext = Path.GetExtension(path).ToLower();
                        if (ext == ".txt")
                        {
                            string content = File.ReadAllText(path);
                            sb.AppendLine($"[文本] 文件={Path.GetFileName(path)} 长度={content.Length} 字符");
                        }
                        else
                        {
                            string docText = ExtractText(path);
                            sb.AppendLine($"[文档] 文件={Path.GetFileName(path)}");
                            sb.AppendLine("内容摘录：\n" + docText);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        public static class LlamaHealth
        {
            /// <summary>
            /// 阻塞式检查 llama.cpp server 是否已加载模型。
            /// 兼容 C# 7.3，无任何 async/await 语法糖。
            /// </summary>
            public static bool IsModelLoaded()
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create("http://localhost:8080/health");
                    request.Method = "GET";
                    request.Timeout = 3000;   // 3 秒超时

                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string body = reader.ReadToEnd().Trim();
                        // 最简单、最稳的字符串匹配
                        return body == "{\"status\":\"ok\"}";
                    }
                }
                catch
                {
                    // 任何异常（服务未起、模型未加载、端口未开）都视为未就绪
                    return false;
                }
            }
        }
        public async Task<HttpResponseMessage> SendPromptAsync(string prompt,
                                                   string model = null,
                                                   bool stream = false)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("prompt 不能为空");

            string attachSummary = await BuildAttachmentSummaryAsync(_userFiles);
            if (!string.IsNullOrWhiteSpace(attachSummary))
                prompt = attachSummary + "\n\n" + prompt;

            var payload = new
            {
                system = "系统人设:" + _cfg.renshe,
                prompt = "<｜User｜>" + prompt + "<｜Assistant｜>",
                temperature = _cfg.wendu,
                top_p = 0.95,
                top_k = 40,
                repeat_penalty = _cfg.chengfa,
                min_p = 0.05,
                keep_alive = 0,
                cache_prompt = true,
                n_predict = 1024,
                n_threads = 4,
                n_gpu_layers = 8,
                stop = new[] {
      "<｜begin▁of▁sentence｜>",
      "<｜end▁of▁sentence｜>",
      "<｜User｜>",
      "<｜Assistant｜>"
  },
                stream = stream               // ✅ 只加这一行
            };

            string json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _http.PostAsync("/completion", content);   // ✅ 流式/非流式同一接口
        }

        private Border _loadingCtrl;
        private FrameworkElement _fakeMessageElement;
        private int _fakeMessageIndex = -1;

        private bool _isSending = false;
        private Brush _sendOriginalBrush;
        private async void send_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isSending) return;
            _isSending = true;
            send.Background = Brushes.Gray;
            _cfg.keepRounds = int.Parse(lunshu.Text);
            _cfg.shangxiawen = int.Parse(shangxiawen.Text);
            _cfg.renshe = renshebianxie.Text;
            _cfg.jiyi = isen.HorizontalAlignment == HorizontalAlignment.Right;
            if (double.TryParse(wendu.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            { _cfg.wendu = v; SaveConfig(); }
            if (double.TryParse(chengfa.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cf))
                _cfg.chengfa = cf;
            else
            {
                MessageBox.Show("设置 -> 模型纠正度必须是数字"); send.Background = _sendOriginalBrush;
                _isSending = false; return;
            }
            SaveConfig();

            string tickFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tick.txt");
            string ticks;
            if (!File.Exists(tickFile))
            { ticks = "1"; File.WriteAllText(tickFile, ticks); }
            else
            {
                ticks = File.ReadAllText(tickFile).Trim();
                if (string.IsNullOrWhiteSpace(ticks)) { ticks = "1"; File.WriteAllText(tickFile, ticks); }
                else
                {
                    int tickValue = int.Parse(ticks);
                    if (tickValue % 60 == 0 && isopenzanzhu == true)
                    { zanzhutanchuang.Visibility = Visibility.Visible; cishu.Text = $"{ticks}次"; opcity04.Visibility = Visibility.Visible; }
                    tickValue--; ticks = tickValue.ToString(); File.WriteAllText(tickFile, ticks);
                }
            }

            string message = input.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
            { send.ClearValue(Border.BackgroundProperty); _isSending = false; return; }
            if (!Process.GetProcessesByName("llama-server").Any())
            {
                MessageBox.Show("请先点击左侧模型卡片启动服务"); send.Background = _sendOriginalBrush;
                _isSending = false; ; return;
            }
            else
            {
                bool ok = LlamaHealth.IsModelLoaded();
                if (!ok)
                {
                    MessageBox.Show("服务已启动，请等待片刻，模型正在加载"); send.Background = _sendOriginalBrush;
                    _isSending = false; ; return;
                }
            }

            string attachSummary = await BuildAttachmentSummaryAsync(_userFiles);
            string extraContext = null;
            if (_cfg.netserach == true)
            {
                try
                {
                    string search = await InternetSearch(message);
                    if (!string.IsNullOrWhiteSpace(search))
                        extraContext = $"【实时搜索结果】\n{search}\n【END】";
                }
                catch { /* 联网失败就纯提问 */ }
            }

            //合并摘要+搜索+时间 → 一次性丢给模型
            if (!string.IsNullOrWhiteSpace(attachSummary))
                extraContext = (extraContext ?? "") + "\n" + attachSummary;

            //UI 插入
            _history.Add(new ChatMessage { Role = "user", Text = message, Assistant = null, Model = null });
            AddMessageToUI("我", message, true, null);
            _fakeMessageElement = CreateFakeMessage();
            _fakeMessageIndex = MessagesPanel.Children.Count;
            MessagesPanel.Children.Add(_fakeMessageElement);
            ChatScrollViewer.ScrollToEnd();
            input.Clear();
            _userFiles.Clear();          // 清空内存
            RefreshDropText();           // 刷新界面文字

            /* ---- 请求并等待回复 ---- */
            await StreamAiAnswerAsync(message, extraContext);

            send.Background = _sendOriginalBrush;
            _isSending = false;
        }


        private void AddMessageToUI(string sender, string message, bool isSelf, string aiModel = null)
        {

            StackPanel messageContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            Border avatarBorder = new Border
            {
                CornerRadius = new CornerRadius(17.5),
                Width = 35,
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            if (isSelf)
                avatarBorder.Background = Brushes.Transparent;
            else if (aiModel == "DeepSeek")
                avatarBorder.Background = Brushes.White;
            else if (aiModel == "Qwen")
                avatarBorder.Background = Brushes.Transparent;
            else
                avatarBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00b96b"));

            int imgSize = 35;
            Image avatarImg;

            if (isSelf)
            {
                string localPath = GetCurrentUserAvatarPath();
                string avatarUri = "file:///" + localPath.Replace('\\', '/');
                using (var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = fs;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();

                    avatarImg = new Image
                    {
                        Width = imgSize,
                        Height = imgSize,
                        Source = bmp,
                        Stretch = Stretch.Fill,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        UseLayoutRounding = true,
                        SnapsToDevicePixels = true
                    };
                }

                avatarImg.Clip = new RectangleGeometry(
                    new Rect(0, 0, imgSize, imgSize),
                    imgSize / 2.0, imgSize / 2.0);
            }
            else
            {
                string avatarUri;
                if (aiModel == "DeepSeek")
                    avatarUri = "pack://application:,,,/deepseeklogo.png";
                else if (aiModel == "Qwen")
                    avatarUri = "pack://application:,,,/qwenlogo.png";
                else
                    avatarUri = "pack://application:,,,/user.png";

                using (var fs = new FileStream(avatarUri.Replace("pack://application:,,,", AppDomain.CurrentDomain.BaseDirectory), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = fs;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();

                    avatarImg = new Image
                    {
                        Width = imgSize,
                        Height = imgSize,
                        Source = bmp,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        UseLayoutRounding = true,
                        SnapsToDevicePixels = true
                    };
                }
            }

            RenderOptions.SetBitmapScalingMode(avatarImg, BitmapScalingMode.Fant);
            avatarBorder.Child = avatarImg;

            StackPanel textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Top
            };

            TextBlock nameBlock = new TextBlock
            {
                Text = isSelf ? "我" : sender,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14
            };

            TextBlock timeBlock = new TextBlock
            {
                Text = DateTime.Now.ToString("yyyy/M/d"),
                Foreground = Brushes.White,
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 3)
            };

            TextBox msgBlock = new TextBox
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true
            };

            textPanel.Children.Add(nameBlock);
            textPanel.Children.Add(timeBlock);
            textPanel.Children.Add(msgBlock);

            messageContainer.Children.Add(avatarBorder);
            messageContainer.Children.Add(textPanel);

            MessagesPanel.Children.Add(messageContainer);
            ChatScrollViewer.ScrollToEnd();
        }

        private FrameworkElement CreateFakeMessage()
        {
            StackPanel messageContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            int imgSize = 35;
            Border avatarBorder = new Border
            {
                CornerRadius = new CornerRadius(17.5),
                Width = imgSize,
                Height = imgSize,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Background = ModelName == "Qwen"
                    ? Brushes.Transparent
                    : ModelName == "DeepSeek"
                        ? Brushes.White
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00b96b"))
            };

            Image avatarImg;
            using (var fs = new FileStream(
                ModelName == "DeepSeek" ? "./deepseeklogo.png"
                : ModelName == "Qwen" ? "./qwenlogo.png"
                : "./user.png",
                FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = fs;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                avatarImg = new Image
                {
                    Width = imgSize,
                    Height = imgSize,
                    Source = bmp,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    UseLayoutRounding = true,
                    SnapsToDevicePixels = true
                };
            }

            RenderOptions.SetBitmapScalingMode(avatarImg, BitmapScalingMode.Fant);
            avatarBorder.Child = avatarImg;


            StackPanel textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

            textPanel.Children.Add(new TextBlock
            {
                Text = CurrentModelName,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14
            });

            textPanel.Children.Add(new TextBlock
            {
                Text = DateTime.Now.ToString("yyyy/M/d"),
                Foreground = Brushes.White,
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 3)
            });

            var dot1 = new Ellipse { Width = 6, Height = 6, Fill = Brushes.White, Margin = new Thickness(0, 0, 2, 0) };
            var dot2 = new Ellipse { Width = 6, Height = 6, Fill = Brushes.White, Margin = new Thickness(2, 0, 0, 0) };
            var dot3 = new Ellipse { Width = 6, Height = 6, Fill = Brushes.White, Margin = new Thickness(4, 0, 0, 0) };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(dot1);
            stack.Children.Add(dot2);
            stack.Children.Add(dot3);

            textPanel.Children.Add(new Border
            {
                Child = stack,
                MinWidth = 30,
                MinHeight = 20,
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 5, 0, 0)
            });

            messageContainer.Children.Add(avatarBorder);
            messageContainer.Children.Add(textPanel);

            foreach (Ellipse dot in stack.Children)
            {
                var trans = new TranslateTransform();
                dot.RenderTransform = trans;

                var jump = new DoubleAnimation
                {
                    From = 0,
                    To = -4,
                    Duration = TimeSpan.FromSeconds(0.3),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromMilliseconds(stack.Children.IndexOf(dot) * 100)
                };

                trans.BeginAnimation(TranslateTransform.YProperty, jump);
            }

            return messageContainer;
        }

        private string GetCurrentUserAvatarPath()
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(appDir, "image.txt");

                if (File.Exists(configPath))
                {
                    string fileName = File.ReadAllText(configPath).Trim();
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        return Path.Combine(appDir, fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载头像时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }


            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user.png");
        }
        public sealed class KbRecord
        {
            public string Text;
            public float[] Vector;
            public KbRecord(string t, float[] v) { Text = t; Vector = v; }
        }
        private async Task StreamAiAnswerAsync(string userText, string extraContext = null)
        {
            int keepRounds = _cfg.keepRounds;
            int maxChars = int.TryParse(shangxiawen.Text, out int len) ? len : 3200;

            /* 1. 实时生成 lite 镜像：去 think + 滑动窗口，不碰 _history */
            _historyLite.Clear();
            int start = Math.Max(0, _history.Count - keepRounds * 2);
            for (int i = start; i < _history.Count; i++)
            {
                ChatMessage m = _history[i];
                // 去 think
                string txt = System.Text.RegularExpressions.Regex.Replace(
                                 m.Text ?? "",
                                 @"<think>.*?</think>",
                                 "",
                                 System.Text.RegularExpressions.RegexOptions.Singleline).Trim();
                _historyLite.Add(new ChatMessage
                {
                    Role = m.Role,
                    Text = txt,          // 干净文本
                    Assistant = m.Assistant,
                    Model = m.Model
                });
            }

            /* 2. 用 _historyLite 拼 Llama-3 纯文本模板 */
            var sb = new System.Text.StringBuilder();
            sb.Append("<|begin_of_text|>\n");
            sb.Append("system\n"); sb.Append(_cfg.renshe).Append('\n');
            sb.Append("<|eot_id|>\n");

            foreach (ChatMessage m in _historyLite)
            {
                sb.Append(m.Role.ToLower()).Append('\n');
                sb.Append(m.Text ?? "").Append('\n');
                sb.Append("<|eot_id|>\n");
            }

            string finalPrompt = userText;
            if (!string.IsNullOrWhiteSpace(extraContext) && _cfg.netserach == true)
                finalPrompt = extraContext + "\n\n" + userText;
            sb.Append("user\n"); sb.Append(finalPrompt).Append('\n');
            sb.Append("<|eot_id|>\n");
            sb.Append("assistant\n");

            string promptToModel = sb.ToString();

            /* 3. 取回答、UI 展示完整（含 think）、原 _history 照常入库 */
            string aiAnswer;
            using (var resp = await SendPromptAsync(promptToModel))
            {
                string raw = await resp.Content.ReadAsStringAsync();
                using (var doc = System.Text.Json.JsonDocument.Parse(raw))
                    aiAnswer = doc.RootElement.GetProperty("content").GetString() ?? "";
            }

            if (_fakeMessageElement != null &&
                _fakeMessageIndex >= 0 &&
                _fakeMessageIndex < MessagesPanel.Children.Count)
            {
                MessagesPanel.Children.RemoveAt(_fakeMessageIndex);
                _fakeMessageElement = null;
                _fakeMessageIndex = -1;
            }
            AddMessageToUI(CurrentModelName, aiAnswer, false, ModelName);
            ChatScrollViewer.ScrollToEnd();

            _history.Add(new ChatMessage
            {
                Role = "assistant",
                Text = aiAnswer,          // 原样保留，重启不丢
                Assistant = CurrentModelName,
                Model = ModelName
            });
            SaveHistory(_history);
        }

        public class ChatHistoryRecord
        {
            public string Title { get; set; }
            public List<ChatMessage> Messages { get; set; }
        }


        private async void bushu_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            chat.BorderBrush = new SolidColorBrush(Colors.Transparent);
            chat.Background = new SolidColorBrush(Colors.Transparent);
            qution.BorderBrush = new SolidColorBrush(Colors.Transparent); ;
            qution.Background = new SolidColorBrush(Colors.Transparent); ;
            setting.BorderBrush = new SolidColorBrush(Colors.Transparent);
            setting.Background = new SolidColorBrush(Colors.Transparent);
            qution.Background = new SolidColorBrush(Colors.Transparent);
            qution.BorderBrush = new SolidColorBrush(Colors.Transparent);
            bushu.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2f2f2f"));
            bushu.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
            fastbushu.Visibility = Visibility.Visible;
            await Task.Delay(100);
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            bool hasWiFi = false;
            foreach (NetworkInterface ni in interfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    hasWiFi = true;
                    break;
                }
            }
        }


        private long GetAvailableSpace(string folderPath)
        {
            DriveInfo driveInfo = new DriveInfo(System.IO.Path.GetPathRoot(folderPath));
            return driveInfo.AvailableFreeSpace;
        }


        private void yijianbushu_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //无用，删除可能报错(史山)
        }


        private async void deepseekr1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsWifiConnected())
            {
                MessageBox.Show("当前未连接到 Wi-Fi，请连接 Wi-Fi 后再试");
                SetErrorUI();
                return;
            }

            string folder = PromptUserForFolder();
            if (string.IsNullOrEmpty(folder))
            {

                return;
            }

            this.Dispatcher.BeginInvoke((Action)(() => { tbtext.Text = "已下载：0 KB"; }));

            string sourceServer = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server");
            string targetServer = Path.Combine(folder, "server");
            if (!Directory.Exists(targetServer))
                DirectoryCopy(sourceServer, targetServer);

            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseeklicense.zip");
            if (!File.Exists(zipPath))
            {
                const string resName = "ModelQuickly.resource.deepseeklicense.zip";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
                {
                    if (stream == null)
                        throw new FileNotFoundException("找不到嵌入资源：" + resName);
                    using (FileStream fs = File.Create(zipPath))
                        await stream.CopyToAsync(fs);
                }
            }

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string fullPath = Path.Combine(folder, SanitizeFileName(entry.Name));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    entry.ExtractToFile(fullPath, overwrite: true);
                }
            }

            bushujiemian.Visibility = Visibility.Visible;
            bushuchenggong.Text = "正在部署..";
            quxiao.Visibility = Visibility.Visible;
            queding.Visibility = Visibility.Collapsed;
            tixingdengdai.Visibility = Visibility.Visible;
            opcity04.Visibility = Visibility.Visible;

            _cts = new CancellationTokenSource();


            Task monitor = MonitorDownloadProgress(folder, _cts.Token);

            List<string> urls = new List<string>
        {
                    "https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00001-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00002-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00003-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00004-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00005-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00006-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00007-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00008-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00009-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00010-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00011-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00012-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00013-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00014-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00015-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00016-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00017-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00018-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00019-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00020-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00021-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00022-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00023-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00024-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00025-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00026-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00027-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00028-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00029-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00030-of-00030.gguf "
        };
            bool allOk = await DownloadMultipleFilesAsync(folder, urls);

            _cts.Cancel();
            try { await monitor; } catch { /* 忽略 */ }

            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                quxiao.Visibility = Visibility.Collapsed;
                queding.Visibility = Visibility.Visible;
                tixingdengdai.Visibility = Visibility.Collapsed;
                bushuchenggong.Text = allOk ? "部署成功！" : "部署失败(部分或全部文件下载失败)。";
            }));
        }
        private string PromptUserForFolder()
        {
            var dlg = new VistaFolderBrowserDialog();
            return dlg.ShowDialog() == true ? dlg.SelectedPath : null;
        }

        private static readonly HttpClient hc = new HttpClient();

        private static readonly HttpClient searchClient = new HttpClient();
        private async Task<string> InternetSearch(string keyword)
        {
            searchClient.DefaultRequestHeaders.Clear();
            searchClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
            searchClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
            searchClient.DefaultRequestHeaders.Add("Referer", "https://cn.bing.com/");

            string kw = Uri.EscapeDataString(keyword);
            string url = $"https://cn.bing.com/search?q={kw}&count=10&FORM=BEHPTB";
            string html = await searchClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes("//li[@class='b_algo']");
            if (nodes == null) return string.Empty;

            var sb = new StringBuilder();
            int took = 0;
            foreach (var li in nodes)
            {
                if (took >= 5) break;
                string title = li.SelectSingleNode(".//h2")?.InnerText ?? "";
                string snippet = li.SelectSingleNode(".//p")?.InnerText ?? "";
                if (!string.IsNullOrWhiteSpace(title))
                { sb.AppendLine(title).AppendLine(snippet).AppendLine(); took++; }
            }
            return sb.ToString();
        }

        private async Task<bool> DownloadMultipleFilesAsync(string folder, List<string> downloadUrls)
        {
            const int BUFFER = 45000;

            if (_httpClient == null)
            {
                _httpClient = new HttpClient(new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                }, true);
                _httpClient.Timeout = Timeout.InfiniteTimeSpan;          // ← 永不超时
                _httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                _httpClient.DefaultRequestHeaders.Add("Referer", "https://hf-mirror.com");
            }

            CancellationToken token = _cts.Token;
            Task progressTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    long totalBytes = 0;
                    try
                    {
                        foreach (string f in Directory.EnumerateFiles(folder, "*.gguf"))
                            totalBytes += new FileInfo(f).Length;
                    }
                    catch { /* 忽略 */ }
                    this.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        tbtext.Text = "已下载：" + (totalBytes / 1024).ToString() + " KB";
                    }));
                    await Task.Delay(500, token);
                }
            }, token);

            bool allOk = true;
            try
            {
                foreach (string url in downloadUrls)
                {
                    string rawName = Path.GetFileName(url.Split('?')[0]);
                    string fileName = SanitizeFileName(rawName);
                    string filePath = Path.Combine(folder, fileName);

                    long startOffset = 0;
                    if (File.Exists(filePath))
                        startOffset = new FileInfo(filePath).Length;

                    HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
                    if (startOffset > 0)
                        req.Headers.Range = new RangeHeaderValue(startOffset, null);

                    HttpResponseMessage resp = await _httpClient.SendAsync(req,
                        HttpCompletionOption.ResponseHeadersRead, token);
                    if (!resp.IsSuccessStatusCode)
                    {
                        allOk = false;
                        break;
                    }

                    long contentLen = resp.Content.Headers.ContentLength ?? 0;
                    long needSpace = contentLen + 50 * 1024 * 1024;
                    if (GetAvailableSpace(folder) < needSpace)
                    {
                        MessageBox.Show("磁盘空间不足，无法继续下载。");
                        allOk = false;
                        break;
                    }

                    using (Stream netStream = await resp.Content.ReadAsStreamAsync())
                    using (FileStream fs = new FileStream(filePath, FileMode.Append,
                                                         FileAccess.Write, FileShare.None, BUFFER, useAsync: true))
                    {
                        await netStream.CopyToAsync(fs, BUFFER, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                allOk = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("下载出错：" + ex.Message);
                allOk = false;
            }
            finally
            {
                _cts.Cancel();
                try { await progressTask; } catch { /* 忽略 */ }
            }

            return allOk;
        }
        private long _lastBytes;
        private async Task MonitorDownloadProgress(string folder, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    long bytes = 0;
                    foreach (string f in Directory.EnumerateFiles(folder, "*.gguf"))
                        bytes += new FileInfo(f).Length;
                    this.Dispatcher.BeginInvoke((Action)(() => { tbtext.Text = "已下载：" + (bytes / 1024).ToString() + " KB"; }));
                }
                catch { /* 忽略占用 */ }
                await Task.Delay(1000, token);
            }
        }
        private static bool IsWifiConnected()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                    ni.OperationalStatus == OperationalStatus.Up)
                    return true;
            return false;
        }
        private async void DeepSeekR1DistillQwen15B_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsWifiConnected())
            {
                MessageBox.Show("当前未连接到 Wi-Fi，请连接 Wi-Fi 后再试");
                SetErrorUI();
                return;
            }

            string folder = PromptUserForFolder();
            if (string.IsNullOrEmpty(folder))
            {

                return;
            }

            this.Dispatcher.BeginInvoke((Action)(() => { tbtext.Text = "已下载：0 KB"; }));

            string sourceServer = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server");
            string targetServer = Path.Combine(folder, "server");
            if (!Directory.Exists(targetServer))
                DirectoryCopy(sourceServer, targetServer);

            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseeklicense.zip");
            if (!File.Exists(zipPath))
            {
                const string resName = "ModelQuickly.resource.deepseeklicense.zip";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
                {
                    if (stream == null)
                        throw new FileNotFoundException("找不到嵌入资源：" + resName);
                    using (FileStream fs = File.Create(zipPath))
                        await stream.CopyToAsync(fs);
                }
            }

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string fullPath = Path.Combine(folder, SanitizeFileName(entry.Name));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    entry.ExtractToFile(fullPath, overwrite: true);
                }
            }

            bushujiemian.Visibility = Visibility.Visible;
            bushuchenggong.Text = "正在部署..";
            quxiao.Visibility = Visibility.Visible;
            queding.Visibility = Visibility.Collapsed;
            tixingdengdai.Visibility = Visibility.Visible;
            opcity04.Visibility = Visibility.Visible;

            _cts = new CancellationTokenSource();

            Task monitor = MonitorDownloadProgress(folder, _cts.Token);

            List<string> urls = new List<string>
        {
            "https://hf-mirror.com/unsloth/DeepSeek-R1-Distill-Qwen-1.5B-GGUF/resolve/main/DeepSeek-R1-Distill-Qwen-1.5B-Q8_0.gguf?download=true"
        };
            bool allOk = await DownloadMultipleFilesAsync(folder, urls);

            _cts.Cancel();
            try { await monitor; } catch { /* 忽略 */ }

            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                quxiao.Visibility = Visibility.Collapsed;
                queding.Visibility = Visibility.Visible;
                tixingdengdai.Visibility = Visibility.Collapsed;
                bushuchenggong.Text = allOk ? "部署成功！" : "部署失败(部分或全部文件下载失败)。";
            }));
        }
        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private void SetErrorUI()
        {
            fastbushu.Visibility = Visibility.Collapsed;
            qution.BorderBrush = new SolidColorBrush(Colors.Transparent);
            qution.Background = new SolidColorBrush(Colors.Transparent);
            setting.BorderBrush = new SolidColorBrush(Colors.Transparent);
            setting.Background = new SolidColorBrush(Colors.Transparent);
            bushu.Background = new SolidColorBrush(Colors.Transparent);
            bushu.BorderBrush = new SolidColorBrush(Colors.Transparent);
            chat.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2f2f2f"));
            chat.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
        }



        private async void deepseekr10528_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsWifiConnected())
            {
                MessageBox.Show("当前未连接到 Wi-Fi，请连接 Wi-Fi 后再试");
                SetErrorUI();
                return;
            }

            string folder = PromptUserForFolder();
            if (string.IsNullOrEmpty(folder))
            {

                return;
            }


            this.Dispatcher.BeginInvoke((Action)(() => { tbtext.Text = "已下载：0 KB"; }));


            string sourceServer = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server");
            string targetServer = Path.Combine(folder, "server");
            if (!Directory.Exists(targetServer))
                DirectoryCopy(sourceServer, targetServer);

            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseeklicense.zip");
            if (!File.Exists(zipPath))
            {
                const string resName = "ModelQuickly.resource.deepseeklicense.zip";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
                {
                    if (stream == null)
                        throw new FileNotFoundException("找不到嵌入资源：" + resName);
                    using (FileStream fs = File.Create(zipPath))
                        await stream.CopyToAsync(fs);
                }
            }


            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string fullPath = Path.Combine(folder, SanitizeFileName(entry.Name));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    entry.ExtractToFile(fullPath, overwrite: true);
                }
            }


            bushujiemian.Visibility = Visibility.Visible;
            bushuchenggong.Text = "正在部署..";
            quxiao.Visibility = Visibility.Visible;
            queding.Visibility = Visibility.Collapsed;
            tixingdengdai.Visibility = Visibility.Visible;
            opcity04.Visibility = Visibility.Visible;

            _cts = new CancellationTokenSource();

            Task monitor = MonitorDownloadProgress(folder, _cts.Token);

            List<string> urls = new List<string>
        {
                    "https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00001-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00002-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00003-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00004-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00005-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00006-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00007-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00008-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00009-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00010-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00011-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00012-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00013-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00014-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00015-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00016-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00017-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00018-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00019-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00020-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00021-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00022-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00023-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00024-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00025-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00026-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00027-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00028-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00029-of-00030.gguf ",
"https://hf-mirror.com/unsloth/DeepSeek-R1-0528-GGUF/resolve/main/BF16/DeepSeek-R1-0528-BF16-00030-of-00030.gguf "
        };
            bool allOk = await DownloadMultipleFilesAsync(folder, urls);

            _cts.Cancel();
            try { await monitor; } catch { /* 忽略 */ }

            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                quxiao.Visibility = Visibility.Collapsed;
                queding.Visibility = Visibility.Visible;
                tixingdengdai.Visibility = Visibility.Collapsed;
                bushuchenggong.Text = allOk ? "部署成功！" : "部署失败(部分或全部文件下载失败)。";
            }));
        }

        private async void DeepSeekR1DistillQwen32B_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsWifiConnected())
            {
                MessageBox.Show("当前未连接到 Wi-Fi，请连接 Wi-Fi 后再试");
                SetErrorUI();
                return;
            }

            string folder = PromptUserForFolder();
            if (string.IsNullOrEmpty(folder))
            {

                return;
            }

            this.Dispatcher.BeginInvoke((Action)(() => { tbtext.Text = "已下载：0 KB"; }));


            string sourceServer = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server");
            string targetServer = Path.Combine(folder, "server");
            if (!Directory.Exists(targetServer))
                DirectoryCopy(sourceServer, targetServer);

            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseeklicense.zip");
            if (!File.Exists(zipPath))
            {
                const string resName = "ModelQuickly.resource.deepseeklicense.zip";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
                {
                    if (stream == null)
                        throw new FileNotFoundException("找不到嵌入资源：" + resName);
                    using (FileStream fs = File.Create(zipPath))
                        await stream.CopyToAsync(fs);
                }
            }

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string fullPath = Path.Combine(folder, SanitizeFileName(entry.Name));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    entry.ExtractToFile(fullPath, overwrite: true);
                }
            }

            bushujiemian.Visibility = Visibility.Visible;
            bushuchenggong.Text = "正在部署..";
            quxiao.Visibility = Visibility.Visible;
            queding.Visibility = Visibility.Collapsed;
            tixingdengdai.Visibility = Visibility.Visible;
            opcity04.Visibility = Visibility.Visible;

            _cts = new CancellationTokenSource();

            Task monitor = MonitorDownloadProgress(folder, _cts.Token);

            List<string> urls = new List<string>
        {
                "https://hf-mirror.com/unsloth/DeepSeek-R1-Distill-Qwen-32B-GGUF/resolve/main/DeepSeek-R1-Distill-Qwen-32B-F16/DeepSeek-R1-Distill-Qwen-32B-F16-00001-of-00002.gguf?download=true",
                "https://hf-mirror.com/unsloth/DeepSeek-R1-Distill-Qwen-32B-GGUF/resolve/main/DeepSeek-R1-Distill-Qwen-32B-F16/DeepSeek-R1-Distill-Qwen-32B-F16-00002-of-00002.gguf?download=true"
        };
            bool allOk = await DownloadMultipleFilesAsync(folder, urls);

            _cts.Cancel();
            try { await monitor; } catch { /* 忽略 */ }

            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                quxiao.Visibility = Visibility.Collapsed;
                queding.Visibility = Visibility.Visible;
                tixingdengdai.Visibility = Visibility.Collapsed;
                bushuchenggong.Text = allOk ? "部署成功！" : "部署失败(部分或全部文件下载失败)。";
            }));
        }

        private async void DeepSeekR1DistillQwen7B_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsWifiConnected())
            {
                MessageBox.Show("当前未连接到 Wi-Fi，请连接 Wi-Fi 后再试");
                SetErrorUI();
                return;
            }


            string folder = PromptUserForFolder();
            if (string.IsNullOrEmpty(folder))
            {

                return;
            }


            this.Dispatcher.BeginInvoke((Action)(() => { tbtext.Text = "已下载：0 KB"; }));

            string sourceServer = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server");
            string targetServer = Path.Combine(folder, "server");
            if (!Directory.Exists(targetServer))
                DirectoryCopy(sourceServer, targetServer);


            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseeklicense.zip");
            if (!File.Exists(zipPath))
            {
                const string resName = "ModelQuickly.resource.deepseeklicense.zip";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
                {
                    if (stream == null)
                        throw new FileNotFoundException("找不到嵌入资源：" + resName);
                    using (FileStream fs = File.Create(zipPath))
                        await stream.CopyToAsync(fs);
                }
            }


            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string fullPath = Path.Combine(folder, SanitizeFileName(entry.Name));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    entry.ExtractToFile(fullPath, overwrite: true);
                }
            }

            bushujiemian.Visibility = Visibility.Visible;
            bushuchenggong.Text = "正在部署..";
            quxiao.Visibility = Visibility.Visible;
            queding.Visibility = Visibility.Collapsed;
            tixingdengdai.Visibility = Visibility.Visible;
            opcity04.Visibility = Visibility.Visible;

            _cts = new CancellationTokenSource();

            Task monitor = MonitorDownloadProgress(folder, _cts.Token);

            // 8.2 真正下载
            List<string> urls = new List<string>
        {
            "https://hf-mirror.com/unsloth/DeepSeek-R1-Distill-Qwen-7B-GGUF/resolve/main/DeepSeek-R1-Distill-Qwen-7B-F16.gguf?download=true"
        };
            bool allOk = await DownloadMultipleFilesAsync(folder, urls);

            _cts.Cancel();
            try { await monitor; } catch { /* 忽略 */ }

            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                quxiao.Visibility = Visibility.Collapsed;
                queding.Visibility = Visibility.Visible;
                tixingdengdai.Visibility = Visibility.Collapsed;
                bushuchenggong.Text = allOk ? "部署成功！" : "部署失败(部分或全部文件下载失败)。";
            }));
        }

        private void closeinputmodelname_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            backgroundopcity.Visibility = Visibility.Collapsed;
            modelinputborder.Visibility = Visibility.Collapsed;
        }

        private void openinputmodelname_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            backgroundopcity.Visibility = Visibility.Visible;
            modelinputborder.Visibility = Visibility.Visible;
        }
        private int _modelTop = 5;

        private static void DirectoryCopy(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(source))
                DirectoryCopy(dir, Path.Combine(target, Path.GetFileName(dir)));
        }


        private void addmodel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            #region 1. 选择文件夹
            var dlg = new VistaFolderBrowserDialog();   // 或 CommonOpenFileDialog
            if (dlg.ShowDialog() != true) return;

            string modelDir = dlg.SelectedPath;
            string modelName = modelnameinput.Text.Trim();
            if (string.IsNullOrWhiteSpace(modelName)) return;
            #endregion

            #region 2. 隐藏 UI
            backgroundopcity.Visibility = Visibility.Collapsed;
            modelinputborder.Visibility = Visibility.Collapsed;
            #endregion

            #region 3. 拷贝 server 目录
            string modelNameReal = moxingmingzi.Text;   // 原变量 ModelName 与字段冲突，改名
            string sourceServer = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server");
            string targetServer = Path.Combine(modelDir, "server");

            if (!Directory.Exists(targetServer))
                DirectoryCopy(sourceServer, targetServer);
            #endregion

            #region 4. 释放嵌入的 zip
            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "deepseeklicense.zip");

            if (!File.Exists(zipPath))
            {
                const string resourceName = "ModelQuickly.resource.deepseeklicense.zip"; // 按实际命名空间修改
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                        throw new FileNotFoundException("找不到嵌入资源：" + resourceName);

                    using (FileStream fs = File.Create(zipPath))
                        stream.CopyTo(fs);
                }
            }
            #endregion

            #region 5. 解压到目标目录
            if (!Directory.Exists(modelDir))
                Directory.CreateDirectory(modelDir);

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string fullPath = Path.Combine(modelDir, entry.FullName);

                    // 目录条目
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(fullPath);
                        continue;
                    }

                    // 文件条目
                    string entryDir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(entryDir) && !Directory.Exists(entryDir))
                        Directory.CreateDirectory(entryDir);

                    entry.ExtractToFile(fullPath, overwrite: true);
                }
            }
            #endregion

            #region 6. 写入模型列表
            var modelList = ModelStorage.Load();
            modelList.Add(new ModelPath
            {
                Name = modelName,
                Path = modelDir,
                Modle = modelNameReal   // 原字段 ModelName 已改名
            });
            ModelStorage.Save(modelList);
            CreateModelCard(modelName, modelDir, modelNameReal);
            modelnameinput.Clear();
            #endregion

            #region 7. 写入人设 prompt
            ModelPrompt.Text = "你是一个AI模型，专注于回答用户的问题";

            // 用新变量名避免重复定义
            var promptList = ModelPromptStorage.Load();
            promptList.RemoveAll(x => x.ModelName == CurrentModelName);
            promptList.Add(new ModelPromptItem
            {
                ModelName = CurrentModelName,
                SystemPrompt = ModelPrompt.Text.Trim()
            });
            ModelPromptStorage.Save(promptList);

            _cfg.renshe = ModelPrompt.Text;
            #endregion

            #region 8. 隐藏剩余 UI
            opcity04.Visibility = Visibility.Collapsed;
            promptinputborder.Visibility = Visibility.Collapsed;
            ModelPrompt.Clear();
            #endregion
        }


        public class ModelPromptItem
        {
            public string ModelName { get; set; }   // 用户起的名字
            public string SystemPrompt { get; set; }   // 人设
        }

        public static class ModelPromptStorage
        {
            private static readonly string FilePath =
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(ModelPromptStorage).Assembly.Location),
                    "modelprompt.json");

            public static List<ModelPromptItem> Load()
            {
                if (!File.Exists(FilePath)) return new List<ModelPromptItem>();
                return JsonConvert.DeserializeObject<List<ModelPromptItem>>(
                       File.ReadAllText(FilePath)) ?? new List<ModelPromptItem>();
            }

            public static void Save(List<ModelPromptItem> list)
            {
                File.WriteAllText(FilePath,
                    JsonConvert.SerializeObject(list, Formatting.Indented));
            }
        }

        private string LoadPersona(string modelName)
        {
            if (!File.Exists("modelprompt.json")) return "You are a helpful assistant.";

            var list = JsonConvert.DeserializeObject<List<ModelPromptItem>>(
                         File.ReadAllText("modelprompt.json")) ?? new List<ModelPromptItem>();

            return list.FirstOrDefault(x => x.ModelName == modelName)?.SystemPrompt
                   ?? "You are a helpful assistant.";
        }

        private void CreateModelCard(string modelName, string modelDir, string realModel)
        {
            BitmapImage logoBitmap;

            if (realModel == "DeepSeek")
                logoBitmap = new BitmapImage(new Uri("pack://application:,,,/deepseeklogo.png"));
            else if (realModel == "Qwen")
                logoBitmap = new BitmapImage(new Uri("pack://application:,,,/qwenlogo.png"));
            else
                logoBitmap = new BitmapImage(new Uri("pack://application:,,,/deepseeklogo.png"));

            int w, h;
            Thickness imageMargin;

            if (realModel == "Qwen")
            {
                w = 25;
                h = 25;
                imageMargin = new Thickness(8, 0, 0, 0);
            }
            else if (realModel == "DeepSeek")
            {
                w = 35;
                h = 35;
                imageMargin = new Thickness(5, 0, 0, 0);
            }
            else
            {
                w = 35;
                h = 35;
                imageMargin = new Thickness(5, 0, 0, 0);
            }


            var grid = new Grid();


            var img = new Image
            {
                Source = logoBitmap,
                Width = w,
                Height = h,
                Margin = imageMargin,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.Fant);
            grid.Children.Add(img);


            var border = new Border
            {
                Width = 250,
                Height = 35,
                CornerRadius = new CornerRadius(18),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, _modelTop, 0, 0),
                Tag = modelName
            };


            grid.Children.Add(new TextBlock
            {
                Text = modelName,
                Foreground = Brushes.White,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(40, 0, 0, 0)
            });

            var btnDel = new Border
            {
                Width = 30,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = modelName,
                Visibility = Visibility.Collapsed
            };


            btnDel.Child = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/delet.png")),
                Width = 16,
                Height = 16
            };
            RenderOptions.SetBitmapScalingMode(btnDel, BitmapScalingMode.Fant);
            grid.Children.Add(btnDel);

            border.Child = grid;



            border.MouseLeftButtonDown += async (s, _) =>
            {

                foreach (Border b in modellist.Children)
                    b.Background = Brushes.Transparent;
                ((Border)s).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525"));

                var borderTag = (string)((Border)s).Tag;
                var mp = ModelStorage.Load().FirstOrDefault(m => m.Name == borderTag);
                if (mp == null) return;

                try
                {

                    await Task.Run(() =>
                    {

                        using (var stopProcess = Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell",
                            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Stop-Process -Name 'llama-server' -Force -ErrorAction SilentlyContinue\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }))
                        {
                            stopProcess?.WaitForExit();
                        }


                        var exePath = Path.Combine(mp.Path, "server", "llama-server.exe");
                        if (!File.Exists(exePath))
                        {
                            throw new FileNotFoundException("未找到llama-server.exe，请检查模型路径");
                        }


                        var ggufFiles = Directory.EnumerateFiles(mp.Path, "*.gguf").ToList();
                        if (ggufFiles.Count == 0)
                        {
                            throw new FileNotFoundException("未检测到.gguf格式模型文件，请重新下载。\r\n不支持 safetensors，请使用 .gguf 格式。");
                        }


                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            Arguments = $"-m \"{ggufFiles[0]}\" -c 4096 --port 8080",
                            WorkingDirectory = mp.Path,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                    });


                    Dispatcher.Invoke(() =>
                    {
                        // ① 读人设 → ② 赋值 → ③ 启动服务
                        CurrentModelName = modelName;
                        ModelName = realModel;
                        //promptinputborder.Visibility = Visibility.Visible;
                        //opcity04.Visibility = Visibility.Visible;
                    });
                }
                catch (FileNotFoundException ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show(ex.Message));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"操作失败: {ex.Message}"));
                }
            };

            var btnEdit = new Border
            {
                Width = 30,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 40, 0),   // 靠左一点，给删除按钮留位
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = modelName,
                Visibility = Visibility.Collapsed
            };
            btnEdit.MouseLeftButtonDown += (sender, e) =>
            {
                e.Handled = true;
                // ① 取模型名（绝对安全，因为 Tag 就是 modelName）
                string model = (string)((Border)border).Tag;

                // ② 读人设
                List<ModelPromptItem> list = ModelPromptStorage.Load();
                ModelPromptItem item = list.Find(
                        delegate (ModelPromptItem x) { return x.ModelName == model; });
                ModelPrompt.Text = item != null ? item.SystemPrompt
                                                : "You are a helpful assistant.";

                // ③ 记住当前正在编辑的模型（供保存用）
                CurrentModelName = model;
                //_cfg.renshe = LoadPersona(modelName);   // 这里只是读，不写文件
                // ④ 弹出面板
                promptinputborder.Visibility = Visibility.Visible;
                opcity04.Visibility = Visibility.Visible;
            };
            btnEdit.Child = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/writ.png")),
                Width = 16,
                Height = 16
            };
            RenderOptions.SetBitmapScalingMode(btnEdit.Child, BitmapScalingMode.Fant);
            grid.Children.Add(btnEdit);
            border.MouseEnter += (_, __) =>
            {
                btnEdit.Visibility = Visibility.Visible;
                btnDel.Visibility = Visibility.Visible;
            };
            border.MouseLeave += (_, __) =>
            {
                btnEdit.Visibility = Visibility.Collapsed;
                btnDel.Visibility = Visibility.Collapsed;
            };
            btnDel.MouseLeftButtonDown += async (_, __) =>
            {

                modellist.Children.Remove(border);


                await Task.Run(() =>
                {
                    // 停止进程
                    using (var stopProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Stop-Process -Name 'llama-server' -Force -ErrorAction SilentlyContinue\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }))
                    {
                        stopProcess?.WaitForExit();
                    }

                    // 更新数据存储
                    var list = ModelStorage.Load();
                    list.RemoveAll(m => m.Name == modelName);
                    ModelStorage.Save(list);
                });
            };

            // 添加到容器
            modellist.Children.Add(border);
            _modelTop += 1;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            var promptList = ModelPromptStorage.Load();
            promptList.RemoveAll(x => x.ModelName == CurrentModelName);
            promptList.Add(new ModelPromptItem
            {
                ModelName = CurrentModelName,
                SystemPrompt = ModelPrompt.Text.Trim()
            });
            ModelPromptStorage.Save(promptList);
            _cfg.renshe = ModelPrompt.Text.Trim();

            opcity04.Visibility = Visibility.Collapsed;
            promptinputborder.Visibility = Visibility.Collapsed;
            _cfg.renshe = ModelPrompt.Text;
            _cfg.shangxiawen = int.Parse(shangxiawen.Text);
            _cfg.keepRounds = int.Parse(lunshu.Text);
            _cfg.renshe = renshebianxie.Text;
            if (isen.HorizontalAlignment == HorizontalAlignment.Right)
            {
                _cfg.jiyi = true;
            }
            else
            {
                _cfg.jiyi = false;
            }
            if (double.TryParse(wendu.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                _cfg.wendu = v;
                SaveConfig();
            }
            if (double.TryParse(chengfa.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cf))
                _cfg.chengfa = cf;

            SaveConfig();
            ChatSessionStorage.Save(_sessions);
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"& { Stop-Process -Name 'llama-server' -Force -ErrorAction SilentlyContinue }\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (var proc = Process.Start(psi))
            {
                proc?.WaitForExit(); // 等待 PowerShell 脚本执行完毕
            }
        }
        internal static class PersistentModelList
        {
            private static readonly string FilePath =
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modelpath.json");

            public static List<ModelPath> Load()
            {
                if (!File.Exists(FilePath)) return new List<ModelPath>();
                var list = JsonConvert.DeserializeObject<List<ModelPath>>(File.ReadAllText(FilePath))
             ?? new List<ModelPath>();
                // 启动时一次性过滤掉已失效的文件夹
                list.RemoveAll(m => !Directory.Exists(m.Path));
                Save(list);               // 把清理后的结果写回
                return list;
            }

            public static void Save(IEnumerable<ModelPath> list) =>
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(list, Formatting.Indented));
        }

        private void huati_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _cfg.shangxiawen = int.Parse(shangxiawen.Text);
            _cfg.keepRounds = int.Parse(lunshu.Text);
            _cfg.renshe = renshebianxie.Text;
            if (isen.HorizontalAlignment == HorizontalAlignment.Right)
            {
                _cfg.jiyi = true;
            }
            else
            {
                _cfg.jiyi = false;
            }
            if (double.TryParse(wendu.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                _cfg.wendu = v;
                SaveConfig();
            }
            if (double.TryParse(chengfa.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cf))
                _cfg.chengfa = cf;
            else { MessageBox.Show("设置 -> 模型纠正度必须是数字"); return; }
            SaveConfig();
            huatitext.Foreground = new SolidColorBrush(Colors.White);
            huatiborder.Visibility = Visibility.Visible;
            moxingtext.Foreground = new SolidColorBrush(Colors.Gray);
            moxingborder.Visibility = Visibility.Collapsed;
            shezhitext.Foreground = new SolidColorBrush(Colors.Gray);
            shezhiborder.Visibility = Visibility.Collapsed;
        }

        private void moxing_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _cfg.shangxiawen = int.Parse(shangxiawen.Text);
            _cfg.keepRounds = int.Parse(lunshu.Text);
            _cfg.renshe = renshebianxie.Text;
            if (isen.HorizontalAlignment == HorizontalAlignment.Right)
            {
                _cfg.jiyi = true;
            }
            else
            {
                _cfg.jiyi = false;
            }
            if (double.TryParse(wendu.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                _cfg.wendu = v;
                SaveConfig();
            }
            if (double.TryParse(chengfa.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cf))
                _cfg.chengfa = cf;
            else { MessageBox.Show("设置 -> 模型纠正度必须是数字"); return; }

            SaveConfig();
            huatitext.Foreground = new SolidColorBrush(Colors.Gray);
            huatiborder.Visibility = Visibility.Collapsed;
            moxingtext.Foreground = new SolidColorBrush(Colors.White);
            moxingborder.Visibility = Visibility.Visible;
            shezhitext.Foreground = new SolidColorBrush(Colors.Gray);
            shezhiborder.Visibility = Visibility.Collapsed;
        }

        private void addchat_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string title = chatnameinput.Text.Trim();
            if (string.IsNullOrEmpty(title)) return;

            ChatSession ns = new ChatSession();
            ns.Id = Guid.NewGuid().ToString();
            ns.Title = title;

            _sessions.Add(ns);
            ChatSessionStorage.Save(_sessions);

            RenderChatList();
            SwitchSession(ns);

            backgroundopcity.Visibility = Visibility.Collapsed;
            chatinputborder.Visibility = Visibility.Collapsed;
        }

        private void closeinputchatname_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            backgroundopcity.Visibility = Visibility.Collapsed;
            chatinputborder.Visibility = Visibility.Collapsed;
        }

        private void openaddchat_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            backgroundopcity.Visibility = Visibility.Visible;
            chatinputborder.Visibility = Visibility.Visible;
            chatnameinput.Text = string.Empty;
        }

        private void Border_MouseEnter_1(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2b2b2b"));
            }
        }

        private void Border_MouseLeave_1(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Colors.Transparent);
            }
        }

        private void close_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void maxwindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
            minwindow.Visibility = Visibility.Visible;
            maxwindow.Visibility = Visibility.Collapsed;
        }

        private void hidewindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void minwindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Normal;
            minwindow.Visibility = Visibility.Collapsed;
            maxwindow.Visibility = Visibility.Visible;
        }

        private void shezhi_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _cfg.shangxiawen = int.Parse(shangxiawen.Text);
            _cfg.keepRounds = int.Parse(lunshu.Text);
            renshebianxie.Text = _cfg.renshe;
            if (isen.HorizontalAlignment == HorizontalAlignment.Right)
            {
                _cfg.jiyi = true;
            }
            else
            {
                _cfg.jiyi = false;
            }
            if (double.TryParse(wendu.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                _cfg.wendu = v;
                SaveConfig();
            }
            if (double.TryParse(chengfa.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cf))
                _cfg.chengfa = cf;
            else { MessageBox.Show("设置 -> 模型纠正度必须是数字"); return; }

            SaveConfig();
            LoadOrCreateConfig();
            LoadConfigToControls();
            huatitext.Foreground = new SolidColorBrush(Colors.Gray);
            huatiborder.Visibility = Visibility.Collapsed;
            shezhitext.Foreground = new SolidColorBrush(Colors.White);
            shezhiborder.Visibility = Visibility.Visible;
            moxingtext.Foreground = new SolidColorBrush(Colors.Gray);
            moxingborder.Visibility = Visibility.Collapsed;
        }


        private void openjiyibaocun_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_memoryEnabled == false)
            {
                openjiyibaocun.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00b96b"));
                _memoryEnabled = true;
                _cfg.jiyi = true;
                isen.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                _cfg.jiyi = false;
                openjiyibaocun.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2f2f2f"));
                isen.HorizontalAlignment = HorizontalAlignment.Left;
                _memoryEnabled = false;
            }
        }

        private void meiwenti_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            opcity04.Visibility = Visibility.Collapsed;
            powerby.Visibility = Visibility.Collapsed;
            NewtonsoftJsonborder.Visibility = Visibility.Collapsed;
            mianzeshangming.Visibility = Visibility.Collapsed;
            ookiidialogsborder.Visibility = Visibility.Collapsed;
            zanzhutanchuang.Visibility = Visibility.Collapsed;
            opcity04.Visibility = Visibility.Collapsed;
            string url = "https://afdian.com/a/modelquickly";

            // 兼容 .NET Core / .NET 5+ 的写法
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true   // 必须设为 true 才能用默认浏览器
            });
        }

        private void xiaciyiding_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            zanzhutanchuang.Visibility = Visibility.Collapsed;
            opcity04.Visibility = Visibility.Collapsed;

        }

        private void queding_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            bushujiemian.Visibility = Visibility.Collapsed;
            opcity04.Visibility = Visibility.Collapsed;
        }
        private CancellationTokenSource _cts;
        private string _updateUrl = null;

        private void quxiao_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _cts?.Cancel();
            opcity04.Visibility = Visibility.Collapsed;
            bushujiemian.Visibility = Visibility.Collapsed;
        }

        private void saveconfig_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isen.HorizontalAlignment == HorizontalAlignment.Right)
            {
                _cfg.jiyi = true;
            }
            else
            {
                _cfg.jiyi = false;
            }
            if (double.TryParse(wendu.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                _cfg.wendu = v;
                SaveConfig();
            }
        }


        private void geren_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            chat.BorderBrush = new SolidColorBrush(Colors.Transparent);
            chat.Background = new SolidColorBrush(Colors.Transparent);
            qution.BorderBrush = new SolidColorBrush(Colors.Transparent);
            qution.Background = new SolidColorBrush(Colors.Transparent);
            setting.BorderBrush = new SolidColorBrush(Colors.Transparent);
            setting.Background = new SolidColorBrush(Colors.Transparent);
            bushu.Background = new SolidColorBrush(Colors.Transparent);
            bushu.BorderBrush = new SolidColorBrush(Colors.Transparent);

            var openFileDialog = new VistaOpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "选择头像"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            try
            {

                string appDir = AppDomain.CurrentDomain.BaseDirectory;

                string configPath = Path.Combine(appDir, "image.txt");

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(openFileDialog.FileName);
                string targetPath = Path.Combine(appDir, fileName);
                File.Copy(openFileDialog.FileName, targetPath, true);


                if (File.Exists(configPath))
                {
                    string oldFileName = File.ReadAllText(configPath).Trim();
                    if (!string.IsNullOrEmpty(oldFileName))
                    {
                        string oldFilePath = Path.Combine(appDir, oldFileName);
                        if (File.Exists(oldFilePath) && oldFilePath != targetPath)
                        {
                            File.Delete(oldFilePath);
                        }
                    }
                }


                File.WriteAllText(configPath, fileName);


                LoadAvatar(targetPath);
                LoadAvatarFromConfig();
                foreach (var m in _history)
                {
                    string senderName = m.Role == "user" ? "我" : m.Assistant;
                    AddMessageToUI(senderName, m.Text, m.Role == "user", m.Model);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载头像时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"& { Stop-Process -Name 'llama-server' -Force -ErrorAction SilentlyContinue }\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (var proc = Process.Start(psi))
            {
                proc?.WaitForExit();
            }
            LaunchBatAndSuicide();
        }
        private void LoadAvatar(string avatarPath)
        {
            if (File.Exists(avatarPath))
            {

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(avatarPath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                touxiang.Source = bitmap;
            }
        }
        private void LoadAvatarFromConfig()
        {
            try
            {

                string appDir = AppDomain.CurrentDomain.BaseDirectory;

                string configPath = Path.Combine(appDir, "image.txt");

                if (File.Exists(configPath))
                {
                    string fileName = File.ReadAllText(configPath).Trim();
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        string avatarPath = Path.Combine(appDir, fileName);
                        LoadAvatar(avatarPath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载头像时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void queding1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            opcity04.Visibility = Visibility.Collapsed;
            powerby.Visibility = Visibility.Collapsed;
            NewtonsoftJsonborder.Visibility = Visibility.Collapsed;
            mianzeshangming.Visibility = Visibility.Collapsed;
            ookiidialogsborder.Visibility = Visibility.Collapsed;
        }

        private void NewtonsoftJson_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            NewtonsoftJsonborder.Visibility = Visibility.Visible;
        }


        private void ookiidialogswpf_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ookiidialogsborder.Visibility = Visibility.Visible;
        }

        private async void opcity04_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (bushujiemian.Visibility == Visibility.Collapsed)
            {
                opcity04.Visibility = Visibility.Collapsed;
                zanzhutanchuang.Visibility = Visibility.Collapsed;
                powerby.Visibility = Visibility.Collapsed;
                updateborder.Visibility = Visibility.Collapsed;
                promptinputborder.Visibility = Visibility.Collapsed;
                #region 1. 保存人设（先保存）
                if (ModelPrompt == null || string.IsNullOrWhiteSpace(ModelPrompt.Text))
                {
                    return;
                }

                var promptList = ModelPromptStorage.Load();
                promptList.RemoveAll(x => x.ModelName == CurrentModelName);
                promptList.Add(new ModelPromptItem
                {
                    ModelName = CurrentModelName,
                    SystemPrompt = ModelPrompt.Text.Trim()
                });
                ModelPromptStorage.Save(promptList);
                _cfg.renshe = ModelPrompt.Text.Trim();

                opcity04.Visibility = Visibility.Collapsed;
                promptinputborder.Visibility = Visibility.Collapsed;
                _cfg.renshe = ModelPrompt.Text;
                #endregion

                #region 2. 只有检测到 llama-server 进程才重启
                bool hasLlama = Process.GetProcessesByName("llama-server").Length > 0;
                if (!hasLlama)
                {
                    // 可选：提示用户
                    // Dispatcher.Invoke(() => MessageBox.Show("llama-server 未运行，无需重启。"));
                    return;   // 直接结束
                }
                #endregion

                #region 3. 重启 llama-server
                var mp = ModelStorage.Load().FirstOrDefault(m => m.Name == CurrentModelName);
                if (mp == null)
                {
                    MessageBox.Show("未找到当前模型配置，无法重启服务。");
                    return;
                }

                try
                {
                    await Task.Run(() =>
                    {
                        // 3.1 停旧进程
                        using (var stop = Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell",
                            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Stop-Process -Name 'llama-server' -Force -ErrorAction SilentlyContinue\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }))
                        {
                            stop?.WaitForExit(3000);
                        }

                        // 3.2 检查可执行文件
                        string exePath = Path.Combine(mp.Path, "server", "llama-server.exe");
                        if (!File.Exists(exePath))
                            throw new FileNotFoundException("未找到 llama-server.exe，请检查模型路径。");

                        // 3.3 检查模型文件
                        var ggufFiles = Directory.EnumerateFiles(mp.Path, "*.gguf").ToList();
                        if (ggufFiles.Count == 0)
                            throw new FileNotFoundException("未检测到 .gguf 格式模型文件，请重新下载。\r\n不支持 safetensors，请使用 .gguf 格式。");

                        // 3.4 启动新进程
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            Arguments = $"-m \"{ggufFiles[0]}\" -c 4096 --port 8080",
                            WorkingDirectory = mp.Path,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                    });
                }
                catch (FileNotFoundException ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show(ex.Message));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show($"重启服务失败: {ex.Message}"));
                }
                #endregion
            }
        }

        private void backgroundopcity_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            backgroundopcity.Visibility = Visibility.Collapsed;
            chatinputborder.Visibility = Visibility.Collapsed;
            modelinputborder.Visibility = Visibility.Collapsed;
        }

        private void xialanxuanze_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            if (moxingxuanze.Visibility == Visibility.Visible)
            {
                moxingxuanze.Visibility = Visibility.Collapsed;
                xialanxuanze.CornerRadius = new CornerRadius(10, 10, 10, 10);
            }
            else
            {
                moxingxuanze.Visibility = Visibility.Visible;
                xialanxuanze.CornerRadius = new CornerRadius(10, 10, 0, 0);
            }
        }

        private void xuanzedeepseek_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            moxingxuanze.Visibility = Visibility.Collapsed;
            dangqianxuanzhongtupian.Width = 25;
            dangqianxuanzhongtupian.Height = 25;
            xialanxuanze.CornerRadius = new CornerRadius(10, 10, 10, 10);
            moxingmingzi.Text = "DeepSeek";
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri("pack://application:,,,/deepseeklogo.png", UriKind.Absolute);
            bitmap.EndInit();
            dangqianxuanzhongtupian.Source = bitmap;
        }

        private void xuanzeqianwen_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            moxingxuanze.Visibility = Visibility.Collapsed;
            xialanxuanze.CornerRadius = new CornerRadius(10, 10, 10, 10);
            moxingmingzi.Text = "Qwen";
            dangqianxuanzhongtupian.Width = 20;
            dangqianxuanzhongtupian.Height = 20;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri("pack://application:,,,/qwenlogo.png", UriKind.Absolute);
            bitmap.EndInit();
            dangqianxuanzhongtupian.Source = bitmap;
        }

        private async void Qwen330BA3BInstruct_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsWifiConnected())
            {
                MessageBox.Show("当前未连接到 Wi-Fi，请连接 Wi-Fi 后再试");
                SetErrorUI();
                return;
            }


            string folder = PromptUserForFolder();
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            this.Dispatcher.BeginInvoke((Action)(() => { tbtext.Text = "已下载：0 KB"; }));

            string sourceServer = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server");
            string targetServer = Path.Combine(folder, "server");
            if (!Directory.Exists(targetServer))
                DirectoryCopy(sourceServer, targetServer);

            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "qwenlicense.zip");
            if (!File.Exists(zipPath))
            {
                const string resName = "ModelQuickly.resource.qwenlicense.zip";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
                {
                    if (stream == null)
                        throw new FileNotFoundException("找不到嵌入资源：" + resName);
                    using (FileStream fs = File.Create(zipPath))
                        await stream.CopyToAsync(fs);
                }
            }

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string fullPath = Path.Combine(folder, SanitizeFileName(entry.Name));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    entry.ExtractToFile(fullPath, overwrite: true);
                }
            }

            bushujiemian.Visibility = Visibility.Visible;
            bushuchenggong.Text = "正在部署..";
            quxiao.Visibility = Visibility.Visible;
            queding.Visibility = Visibility.Collapsed;
            tixingdengdai.Visibility = Visibility.Visible;
            opcity04.Visibility = Visibility.Visible;

            _cts = new CancellationTokenSource();


            Task monitor = MonitorDownloadProgress(folder, _cts.Token);


            List<string> urls = new List<string>
        {
            "https://hf-mirror.com/unsloth/Qwen3-30B-A3B-Instruct-2507-GGUF/resolve/main/Qwen3-30B-A3B-Instruct-2507-UD-Q8_K_XL.gguf?download=true"
        };
            bool allOk = await DownloadMultipleFilesAsync(folder, urls);

            // 9. 结束
            _cts.Cancel();
            try { await monitor; } catch { /* 忽略 */ }

            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                quxiao.Visibility = Visibility.Collapsed;
                queding.Visibility = Visibility.Visible;
                tixingdengdai.Visibility = Visibility.Collapsed;
                bushuchenggong.Text = allOk ? "部署成功！" : "部署失败(部分或全部文件下载失败)。";
            }));
        }

        private async void Qwen3Coder30BA3BInstruct_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsWifiConnected())
            {
                MessageBox.Show("当前未连接到 Wi-Fi，请连接 Wi-Fi 后再试");
                SetErrorUI();
                return;
            }

            string folder = PromptUserForFolder();
            if (string.IsNullOrEmpty(folder))
            {

                return;
            }


            this.Dispatcher.BeginInvoke((Action)(() => { tbtext.Text = "已下载：0 KB"; }));


            string sourceServer = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server");
            string targetServer = Path.Combine(folder, "server");
            if (!Directory.Exists(targetServer))
                DirectoryCopy(sourceServer, targetServer);


            string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "qwenlicense.zip");
            if (!File.Exists(zipPath))
            {
                const string resName = "ModelQuickly.resource.qwenlicense.zip";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
                {
                    if (stream == null)
                        throw new FileNotFoundException("找不到嵌入资源：" + resName);
                    using (FileStream fs = File.Create(zipPath))
                        await stream.CopyToAsync(fs);
                }
            }


            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string fullPath = Path.Combine(folder, SanitizeFileName(entry.Name));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    entry.ExtractToFile(fullPath, overwrite: true);
                }
            }

            bushujiemian.Visibility = Visibility.Visible;
            bushuchenggong.Text = "正在部署..";
            quxiao.Visibility = Visibility.Visible;
            queding.Visibility = Visibility.Collapsed;
            tixingdengdai.Visibility = Visibility.Visible;
            opcity04.Visibility = Visibility.Visible;

            _cts = new CancellationTokenSource();


            Task monitor = MonitorDownloadProgress(folder, _cts.Token);


            List<string> urls = new List<string>
        {
            "https://hf-mirror.com/unsloth/Qwen3-Coder-30B-A3B-Instruct-GGUF/resolve/main/Qwen3-Coder-30B-A3B-Instruct-UD-Q8_K_XL.gguf?download=true"
        };
            bool allOk = await DownloadMultipleFilesAsync(folder, urls);


            _cts.Cancel();
            try { await monitor; } catch { /* 忽略 */ }

            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                quxiao.Visibility = Visibility.Collapsed;
                queding.Visibility = Visibility.Visible;
                tixingdengdai.Visibility = Visibility.Collapsed;
                bushuchenggong.Text = allOk ? "部署成功！" : "部署失败(部分或全部文件下载失败)。";
            }));

        }

        private void netserach_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var bd = (Border)sender;

            if (bd.Background.ToString() == "#FF1F1F1F")
            {
                bd.Background = (Brush)new BrushConverter().ConvertFrom("#FF2F2F2F");
                _cfg.netserach = true;
            }

            else
            {
                bd.Background = (Brush)new BrushConverter().ConvertFrom("#FF1F1F1F");
                _cfg.netserach = false;
            }
        }

        private void uploadfiles_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog()
            {
                Multiselect = true,
                Filter = "所有支持文件|*.png;*.jpg;*.jpeg;*.bmp;*.pdf;*.txt;*.docx|图片|*.png;*.jpg;*.jpeg;*.bmp|文档|*.pdf;*.txt;*.docx"
            };
            if (dlg.ShowDialog() == true)
            {
                _userFiles.Clear();
                foreach (string f in dlg.FileNames) AddFile(f);
                RefreshDropText();
            }

        }
        private readonly List<object> _userFiles = new List<object>();
        private void AddFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            if (new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(ext))

                _userFiles.Add(new BitmapImage(new Uri(path, UriKind.Absolute)));
            else if (new[] { ".pdf", ".docx", ".txt" }.Contains(ext))

                _userFiles.Add(path);
            else

                _userFiles.Add(File.ReadAllText(path));
        }
        private string ExtractText(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            string raw = "";
            try
            {
                if (ext == ".pdf")
                {
                    using (var doc = PdfDocument.Open(path))
                        foreach (var page in doc.GetPages())
                            raw += page.Text;
                }
                else if (ext == ".docx")
                {
                    using (var doc = DOCX.WordprocessingDocument.Open(path, false))
                        raw = string.Join("\n", doc.MainDocumentPart.RootElement
                                            .Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));
                }
            }
            catch { raw = "（文档解析失败）"; }

            return raw.Length > 4000 ? raw.Substring(0, 4000) + "\n...(已截断)" : raw;
        }
        static void LaunchBatAndSuicide()
        {
            string batPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rest.bat");
            File.WriteAllText(batPath,
                @"@echo off" + "\r\n" +
                @"start """" /b ""%~dp0ModelQuickly.exe""");

            Process.Start(new ProcessStartInfo
            {
                FileName = batPath,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            });

            Environment.Exit(0);   // 自杀
        }
        private void RefreshDropText()
        {
            tbDrop.Text = $"已上传 {_userFiles.Count} 个文件";
        }

        private void update_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            opcity04.Visibility = Visibility.Visible;
            updateborder.Visibility = Visibility.Visible;
        }

        private void input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(input.Text))
            {
                e.Handled = true;
                send_MouseLeftButtonDown(this, null);
            }
        }

        private void closeupdate_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            opcity04.Visibility = Visibility.Collapsed;
            updateborder.Visibility = Visibility.Collapsed;
        }

        private async void updatebutton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            updatebutton.Visibility = Visibility.Collapsed;
            closeupdateborder.Visibility = Visibility.Collapsed;
            updatetext.Text = "正在更新，完成后会自动重启";
            text1.Text = $"当前进度:0%";
            string fileUrl = _updateUrl;
            string fileName = Path.GetFileName(fileUrl);
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string savePath = Path.Combine(exeDir, fileName);

            using (WebClient client = new WebClient())
            {
                // 实时进度 → text1
                client.DownloadProgressChanged += async (s, args) =>
                {
                    text1.Text = $"当前进度:{args.ProgressPercentage}%";
                };

                try
                {
                    await client.DownloadFileTaskAsync(new Uri(fileUrl), savePath);
                }
                catch
                {
                    return; // 下载失败就不继续
                }
            }

            // 下载完成后执行脚本
            string batPath1 = Path.Combine(exeDir, "update.bat");
            Process.Start(new ProcessStartInfo
            {
                FileName = batPath1,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            });

            this.Close();   // 关闭当前程序
        }

        private async void addprompt_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            #region 1. 保存人设（先保存）
            if (ModelPrompt == null || string.IsNullOrWhiteSpace(ModelPrompt.Text))
            {
                MessageBox.Show("人设内容不能为空！");
                return;
            }

            var promptList = ModelPromptStorage.Load();
            promptList.RemoveAll(x => x.ModelName == CurrentModelName);
            promptList.Add(new ModelPromptItem
            {
                ModelName = CurrentModelName,
                SystemPrompt = ModelPrompt.Text.Trim()
            });
            ModelPromptStorage.Save(promptList);
            _cfg.renshe = ModelPrompt.Text.Trim();

            opcity04.Visibility = Visibility.Collapsed;
            promptinputborder.Visibility = Visibility.Collapsed;
            _cfg.renshe = ModelPrompt.Text;
            #endregion

            #region 2. 只有检测到 llama-server 进程才重启
            bool hasLlama = Process.GetProcessesByName("llama-server").Length > 0;
            if (!hasLlama)
            {
                // 可选：提示用户
                // Dispatcher.Invoke(() => MessageBox.Show("llama-server 未运行，无需重启。"));
                return;   // 直接结束
            }
            #endregion

            #region 3. 重启 llama-server
            var mp = ModelStorage.Load().FirstOrDefault(m => m.Name == CurrentModelName);
            if (mp == null)
            {
                MessageBox.Show("未找到当前模型配置，无法重启服务。");
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    // 3.1 停旧进程
                    using (var stop = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Stop-Process -Name 'llama-server' -Force -ErrorAction SilentlyContinue\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }))
                    {
                        stop?.WaitForExit(3000);
                    }

                    // 3.2 检查可执行文件
                    string exePath = Path.Combine(mp.Path, "server", "llama-server.exe");
                    if (!File.Exists(exePath))
                        throw new FileNotFoundException("未找到 llama-server.exe，请检查模型路径。");

                    // 3.3 检查模型文件
                    var ggufFiles = Directory.EnumerateFiles(mp.Path, "*.gguf").ToList();
                    if (ggufFiles.Count == 0)
                        throw new FileNotFoundException("未检测到 .gguf 格式模型文件，请重新下载。\r\n不支持 safetensors，请使用 .gguf 格式。");

                    // 3.4 启动新进程
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"-m \"{ggufFiles[0]}\" -c 4096 --port 8080",
                        WorkingDirectory = mp.Path,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                });
            }
            catch (FileNotFoundException ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show(ex.Message));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"重启服务失败: {ex.Message}"));
            }
            #endregion
        }

        private async void closeinputprompt_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            opcity04.Visibility = Visibility.Collapsed;
            promptinputborder.Visibility = Visibility.Collapsed;
            #region 1. 保存人设（先保存）
            if (ModelPrompt == null || string.IsNullOrWhiteSpace(ModelPrompt.Text))
            {
                MessageBox.Show("人设内容不能为空！");
                return;
            }

            var promptList = ModelPromptStorage.Load();
            promptList.RemoveAll(x => x.ModelName == CurrentModelName);
            promptList.Add(new ModelPromptItem
            {
                ModelName = CurrentModelName,
                SystemPrompt = ModelPrompt.Text.Trim()
            });
            ModelPromptStorage.Save(promptList);
            _cfg.renshe = ModelPrompt.Text.Trim();

            opcity04.Visibility = Visibility.Collapsed;
            promptinputborder.Visibility = Visibility.Collapsed;
            _cfg.renshe = ModelPrompt.Text;
            #endregion

            #region 2. 只有检测到 llama-server 进程才重启
            bool hasLlama = Process.GetProcessesByName("llama-server").Length > 0;
            if (!hasLlama)
            {
                // 可选：提示用户
                // Dispatcher.Invoke(() => MessageBox.Show("llama-server 未运行，无需重启。"));
                return;   // 直接结束
            }
            #endregion

            #region 3. 重启 llama-server
            var mp = ModelStorage.Load().FirstOrDefault(m => m.Name == CurrentModelName);
            if (mp == null)
            {
                MessageBox.Show("未找到当前模型配置，无法重启服务。");
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    // 3.1 停旧进程
                    using (var stop = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Stop-Process -Name 'llama-server' -Force -ErrorAction SilentlyContinue\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }))
                    {
                        stop?.WaitForExit(3000);
                    }

                    // 3.2 检查可执行文件
                    string exePath = Path.Combine(mp.Path, "server", "llama-server.exe");
                    if (!File.Exists(exePath))
                        throw new FileNotFoundException("未找到 llama-server.exe，请检查模型路径。");

                    // 3.3 检查模型文件
                    var ggufFiles = Directory.EnumerateFiles(mp.Path, "*.gguf").ToList();
                    if (ggufFiles.Count == 0)
                        throw new FileNotFoundException("未检测到 .gguf 格式模型文件，请重新下载。\r\n不支持 safetensors，请使用 .gguf 格式。");

                    // 3.4 启动新进程
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"-m \"{ggufFiles[0]}\" -c 4096 --port 8080",
                        WorkingDirectory = mp.Path,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                });
            }
            catch (FileNotFoundException ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show(ex.Message));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"重启服务失败: {ex.Message}"));
            }
            #endregion
        }
    }
}