// OpenClaw 控制中心 + DeepSeek 三项数据悬浮球 (合并版, 特效按钮)
// 编译: csc /nologo /target:winexe /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:ds_control.exe ds_float.cs
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

class OpenClawApp {
    // ===== 多语言（自动识别系统语言: 中文→中文界面, 其他→英文界面） =====
    public static bool IsZh = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh");
    public static string T(string zh, string en) { return IsZh ? zh : en; }
    public static string NO_KEY = "NoKey"; // 内部标记，显示时翻译

    // ===== 全局状态 =====
    public static string GatewayUrl = "http://127.0.0.1:18789";
    public static int GatewayPort = 18789;
    public static string CurrentModel = T("检测中...", "Detecting...");
    public static string DynamicSiteUrl = "https://github.com/openclaw/openclaw";
    public static string DynamicSiteName = T("官方文档 ↗", "Docs ↗");
    public static bool IsGatewayRunning = false;
    public static string ModelKey = "deepseek"; // 悬浮球图标: deepseek/gpt/gemini/kimi
    static string _lastModelKey = "";

    static string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".openclaw\\openclaw.json");

    public static BallForm ballForm;
    public static MainForm mainForm;
    public static CardForm cardForm;
    public static bool cardVisible = false;

    // DeepSeek 用量
    static string KEY = "";
    static string BALANCE_URL = "https://api.deepseek.com/user/balance";
    static string OFFICIAL_URL = "https://platform.deepseek.com";
    static string SESSIONS_DIR = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".openclaw\\agents\\main\\sessions");

    // ===== Win32 API: 悬浮球 Alpha =====
    public static class NativeMethods {
        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x; public int y; public POINT(int x, int y) { this.x = x; this.y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx; public int cy; public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp; public byte BlendFlags; public byte SourceConstantAlpha; public byte AlphaFormat; }
        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;
        public const int ULW_ALPHA = 0x02;
    }

    public static GraphicsPath RoundRect(Rectangle r, int rad) {
        GraphicsPath p = new GraphicsPath();
        if (rad <= 0) { p.AddRectangle(r); return p; }
        int d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ===== 1. 自动识别 OpenClaw =====
    public static void DetectOpenClaw() {
        try {
            if (File.Exists(ConfigPath)) {
                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                var mPort = Regex.Match(json, "\"port\"\\s*:\\s*(\\d+)");
                if (mPort.Success) { GatewayPort = int.Parse(mPort.Groups[1].Value); GatewayUrl = "http://127.0.0.1:" + GatewayPort; }
                var mModel = Regex.Match(json, "\"primary\"\\s*:\\s*\"([^\"]+)\"");
                if (!mModel.Success) mModel = Regex.Match(json, "\"model\"\\s*:\\s*\"([^\"]+)\"");
                CurrentModel = mModel.Success ? mModel.Groups[1].Value : "deepseek-chat";
            } else { GatewayUrl = T("未找到配置", "Config not found"); CurrentModel = T("未知", "Unknown"); }
        } catch { GatewayUrl = T("读取错误", "Read error"); CurrentModel = T("未知", "Unknown"); }

        // TCP 端口探针
        IsGatewayRunning = false;
        try {
            using (var client = new TcpClient()) {
                var task = client.ConnectAsync("127.0.0.1", GatewayPort);
                if (task.Wait(800)) IsGatewayRunning = client.Connected;
            }
        } catch {}

        // 模型 → 官网
        string lm = CurrentModel.ToLower();
        if (lm.Contains("deepseek")) { DynamicSiteUrl = "https://platform.deepseek.com"; DynamicSiteName = T("DeepSeek 官网 ↗", "DeepSeek ↗"); }
        else if (lm.Contains("gpt") || lm.Contains("openai")) { DynamicSiteUrl = "https://platform.openai.com"; DynamicSiteName = T("OpenAI 官网 ↗", "OpenAI ↗"); }
        else if (lm.Contains("claude") || lm.Contains("anthropic")) { DynamicSiteUrl = "https://console.anthropic.com"; DynamicSiteName = T("Claude 官网 ↗", "Claude ↗"); }
        else if (lm.Contains("qwen") || lm.Contains("tongyi")) { DynamicSiteUrl = "https://dashscope.console.aliyun.com"; DynamicSiteName = T("通义千问 ↗", "Qwen ↗"); }
        else if (lm.Contains("gemini") || lm.Contains("google")) { DynamicSiteUrl = "https://aistudio.google.com"; DynamicSiteName = T("Gemini 官网 ↗", "Gemini ↗"); }
        else { DynamicSiteUrl = "https://github.com/openclaw/openclaw"; DynamicSiteName = T("OpenClaw 官网 ↗", "OpenClaw ↗"); }

        // 模型 → 悬浮球图标
        string key = "deepseek";
        if (lm.Contains("gpt") || lm.Contains("openai")) key = "gpt";
        else if (lm.Contains("gemini") || lm.Contains("google")) key = "gemini";
        else if (lm.Contains("kimi") || lm.Contains("moonshot")) key = "kimi";
        if (key != _lastModelKey) {
            _lastModelKey = key;
            ModelKey = key;
            if (ballForm != null && !ballForm.IsDisposed) {
                try { ballForm.Invoke((Action)(() => ballForm.DrawBall(ballForm.IsHover(), ballForm.IsPress()))); } catch {}
            }
        }
    }

    // ===== 2. 开机自启 =====
    public static bool IsAutoStartEnabled() {
        try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false)) return k.GetValue("OpenClawControl") != null; } catch { return false; }
    }
    public static void SetAutoStart(bool enable) {
        try {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)) {
                if (enable) k.SetValue("OpenClawControl", "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue("OpenClawControl", false);
            }
        } catch (Exception ex) { MessageBox.Show(T("设置开机自启失败: ", "Failed to set auto-start: ") + ex.Message); }
    }

    // ===== 3. 网关开关 =====
    public static void ToggleGateway() {
        if (IsGatewayRunning) {
            foreach (var p in Process.GetProcessesByName("openclaw")) { try { p.Kill(); } catch {} }
            foreach (var p in Process.GetProcessesByName("node")) {
                try { if (p.MainWindowTitle == "") p.Kill(); } catch {}
            }
            IsGatewayRunning = false;
        } else {
            try {
                // openclaw 是 npm 的 .cmd shim, 需经 cmd.exe 启动
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c openclaw gateway start");
                psi.CreateNoWindow = true; psi.UseShellExecute = false;
                Process.Start(psi);
                IsGatewayRunning = true;
            } catch (Exception ex) { MessageBox.Show(T("网关启动失败: ", "Failed to start gateway: ") + ex.Message); }
        }
    }

    // ===== 4. DeepSeek 用量 =====
    static string FetchBalance() {
        if (string.IsNullOrEmpty(KEY)) return NO_KEY;
        try {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(BALANCE_URL);
            req.Method = "GET";
            req.Headers["Authorization"] = "Bearer " + KEY;
            req.Accept = "application/json";
            req.Timeout = 10000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) {
                string body = sr.ReadToEnd();
                var m = Regex.Match(body, "\"total_balance\"\\s*:\\s*\"([^\"]+)\"");
                if (!m.Success) m = Regex.Match(body, "\"total_balance\"\\s*:\\s*([0-9.]+)");
                return m.Success ? m.Groups[1].Value : "?";
            }
        } catch { return "ERR"; }
    }

    static void GetTodayUsage(out long tin, out long tout, out double cost) {
        tin = 0; tout = 0; cost = 0;
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        try {
            if (!Directory.Exists(SESSIONS_DIR)) return;
            foreach (var f in Directory.GetFiles(SESSIONS_DIR, "*.jsonl")) {
                if (Path.GetFileName(f).Contains("trajectory")) continue;
                try {
                    var fi = new FileInfo(f);
                    if (fi.LastWriteTime.ToString("yyyy-MM-dd") != today) continue;
                    string c = File.ReadAllText(f, Encoding.UTF8);
                    foreach (Match m in Regex.Matches(c, "\"input\"\\s*:\\s*(\\d+)")) { long v; if (long.TryParse(m.Groups[1].Value, out v)) tin += v; }
                    foreach (Match m in Regex.Matches(c, "\"output\"\\s*:\\s*(\\d+)")) { long v; if (long.TryParse(m.Groups[1].Value, out v)) tout += v; }
                    foreach (Match m in Regex.Matches(c, "\"cost\"\\s*:\\s*\\{[^}]*?\"total\"\\s*:\\s*([0-9.eE+-]+)")) { double v; if (double.TryParse(m.Groups[1].Value, out v)) cost += v; }
                } catch {}
            }
        } catch {}
    }

    public static string bal = "…", tokStr = "…", costStr = "…", timeStr = "";

    // ===== 自动获取 DeepSeek API Key =====
    // 优先级: 1.openclaw models.json  2.openclaw.json  3.环境变量  4.同目录 ds_key.conf
    public static void LoadKey() {
        // 按当前模型找对应 provider 的 key
        string prov = "deepseek";
        if (ModelKey == "gpt") prov = "openai";
        else if (ModelKey == "gemini") prov = "google";
        else if (ModelKey == "kimi") prov = "moonshot";

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] jsonFiles = new string[] {
            Path.Combine(home, ".openclaw", "main", "agent", "models.json"),
            Path.Combine(home, ".openclaw", "agents", "main", "agent", "models.json"),
            ConfigPath
        };
        foreach (string f in jsonFiles) {
            try {
                if (!File.Exists(f)) continue;
                string json = File.ReadAllText(f, Encoding.UTF8);
                string k = ExtractKey(json, prov);
                if (k == null && prov != "deepseek") k = ExtractKey(json, "deepseek");
                if (k != null) { KEY = k; SaveConf(); return; }
            } catch {}
        }
        // 环境变量
        try {
            string envVar = prov == "gpt" ? "OPENAI_API_KEY" : (prov == "gemini" ? "GEMINI_API_KEY" : (prov == "kimi" ? "MOONSHOT_API_KEY" : "DEEPSEEK_API_KEY"));
            string envKey = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrEmpty(envKey) && prov != "deepseek") envKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            if (!string.IsNullOrEmpty(envKey)) { KEY = envKey; SaveConf(); return; }
        } catch {}
        // 兜底: 同目录 ds_key.conf
        try {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string conf = Path.Combine(dir, "ds_key.conf");
            if (File.Exists(conf)) { KEY = File.ReadAllText(conf, Encoding.UTF8).Trim(); SaveConf(); }
        } catch {}
        // 全部来源都拿不到 key → 弹窗让用户手动输入（首次使用）
        if (string.IsNullOrEmpty(KEY)) {
            try {
                using (var dlg = new KeyInputForm()) {
                    if (dlg.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(KEY)) {
                        SaveConf();
                    }
                }
            } catch {}
        }
        SaveConf();
    }

    // 识别到 key 后自动生成 conf（便携包模式: 首次运行自动落盘）
    static void SaveConf() {
        if (string.IsNullOrEmpty(KEY)) return;
        try {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string conf = Path.Combine(dir, "ds_key.conf");
            if (!File.Exists(conf)) File.WriteAllText(conf, KEY, new UTF8Encoding(false));
        } catch {}
    }

    // 从 JSON 找指定 provider 块内的 sk- apiKey（IndexOf 方式）
    static string ExtractKey(string json, string provider) {
        int p = json.IndexOf("\"" + provider + "\"");
        if (p >= 0) {
            // 从 provider 位置向后找最近的 apiKey
            int i = json.IndexOf("\"apiKey\"", p);
            if (i >= 0 && i - p < 5000) {
                int colon = json.IndexOf(":", i);
                if (colon >= 0) {
                    int q1 = json.IndexOf("\"", colon);
                    if (q1 >= 0) {
                        int q2 = json.IndexOf("\"", q1 + 1);
                        if (q2 > q1) {
                            string k = json.Substring(q1 + 1, q2 - q1 - 1);
                            if (k.StartsWith("sk-") || k.StartsWith("cpk-") || k.Length > 15) return k;
                        }
                    }
                }
            }
        }
        // 兜底: 任意 sk- apiKey
        int j = json.IndexOf("\"apiKey\"");
        while (j >= 0) {
            int colon = json.IndexOf(":", j);
            if (colon >= 0) {
                int q1 = json.IndexOf("\"", colon);
                if (q1 >= 0) {
                    int q2 = json.IndexOf("\"", q1 + 1);
                    if (q2 > q1) {
                        string k = json.Substring(q1 + 1, q2 - q1 - 1);
                        if (k.StartsWith("sk-")) return k;
                    }
                }
            }
            j = json.IndexOf("\"apiKey\"", j + 1);
        }
        return null;
    }

public static void RefreshData() {
        Thread t = new Thread(() => {
            try {
                Log("RefreshData start");
                string b = FetchBalance();
                Log("balance=" + b);
                long tin, tout; double cost;
                GetTodayUsage(out tin, out tout, out cost);
                Log("usage tin=" + tin + " tout=" + tout + " cost=" + cost);
                long tok = tin + tout;
                string ts;
                if (tok >= 100000000) ts = IsZh ? (tok / 100000000.0).ToString("0.0000") + " 亿" : (tok / 1000000.0).ToString("0.00") + "M";
                else if (tok >= 10000) ts = IsZh ? (tok / 10000.0).ToString("0.00") + " 万" : (tok / 1000.0).ToString("0.00") + "K";
                else ts = tok.ToString();
                bal = b; tokStr = ts;
                costStr = "¥ " + cost.ToString("0.0000");
                timeStr = DateTime.Now.ToString("HH:mm:ss");
                Log("values set: bal=" + bal + " tok=" + tokStr + " cost=" + costStr);
                if (cardForm != null && !cardForm.IsDisposed) {
                    try { if (cardForm.IsHandleCreated) cardForm.Invoke((Action)(() => cardForm.UpdateUsage())); } catch (Exception ex) { Log("card invoke err: " + ex.Message); }
                }
                if (mainForm != null && !mainForm.IsDisposed) {
                    try { if (mainForm.IsHandleCreated) mainForm.Invoke((Action)(() => mainForm.UpdateUsage())); } catch (Exception ex) { Log("main invoke err: " + ex.Message); }
                }
                Log("invoke done");
            } catch (Exception ex) {
                Log("RefreshData exception: " + ex.ToString());
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    public static void Log(string msg) {
        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "ds_float_debug.log"), DateTime.Now.ToString("HH:mm:ss") + " " + msg + "\r\n"); } catch {}
    }

    // ===== 特效圆角按钮 =====
    public class RoundedBtn : Control {
        public Color BaseColor, HoverColor, PressColor;
        public int Radius;
        bool hover = false, press = false;
        Action _onClick;
        public RoundedBtn(string text, Rectangle bounds, Color baseC, Color hoverC, Color pressC, int radius, Action onClick) {
            Text = text; Bounds = bounds;
            BaseColor = baseC; HoverColor = hoverC; PressColor = pressC; Radius = radius;
            _onClick = onClick;
            DoubleBuffered = true; Cursor = Cursors.Hand; ForeColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
            MouseEnter += (s, e) => { hover = true; Invalidate(); };
            MouseLeave += (s, e) => { hover = false; press = false; Invalidate(); };
            MouseDown += (s, e) => { press = true; Invalidate(); };
            MouseUp += (s, e) => { press = false; Invalidate(); };
            Click += (s, e) => _onClick();
        }
        public void PerformClick() { _onClick(); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color c = press ? PressColor : (hover ? HoverColor : BaseColor);
            using (GraphicsPath p = RoundRect(r, Radius)) {
                using (SolidBrush b = new SolidBrush(c)) e.Graphics.FillPath(b, p);
                using (GraphicsPath top = RoundRect(new Rectangle(1, 1, Width - 3, Height / 2), Radius)) {
                    using (SolidBrush hb = new SolidBrush(Color.FromArgb(35, 255, 255, 255))) e.Graphics.FillPath(hb, top);
                }
                TextRenderer.DrawText(e.Graphics, Text, Font, r, ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    // ===== 控制中心主窗口 =====
    public class MainForm : Form {
        Label lblStatus, lblUrl, lblModel;
        Label lblBal, lblTok, lblCost, lblTime;
        RoundedBtn btnGateway, btnBall, btnRefresh, btnSite;
        CheckBox chkAutoStart;
        Panel containerPanel;
        Color bg = Color.FromArgb(20, 22, 30);
        Color cardBg = Color.FromArgb(30, 33, 46);
        Color fg = Color.FromArgb(232, 234, 242);
        Color accent = Color.FromArgb(59, 130, 246);
        Color green = Color.FromArgb(163, 230, 53);

        public MainForm() {
            Text = T("OpenClaw 控制中心", "OpenClaw Control Center");
            Size = new Size(390, 520);
            MinimumSize = new Size(380, 300);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = bg;

            containerPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = bg };
            Controls.Add(containerPanel);

            Label lblTitle = new Label {
                Text = T("OpenClaw 控制面板", "OpenClaw Control Panel"),
                Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold),
                ForeColor = fg, Location = new Point(20, 15), AutoSize = true
            };
            containerPanel.Controls.Add(lblTitle);

            // 卡片1: 网关
            Panel pnlCard = new Panel { Location = new Point(20, 50), Size = new Size(330, 110), BackColor = cardBg };
            containerPanel.Controls.Add(pnlCard);
            lblStatus = MakeInfoLabel(pnlCard, T("网关状态: 检测中...", "Gateway: Detecting..."), 12, 12, true);
            lblUrl = MakeInfoLabel(pnlCard, T("网关地址: 检测中...", "Gateway URL: Detecting..."), 12, 40, false);
            lblModel = MakeInfoLabel(pnlCard, T("当前模型: 检测中...", "Model: Detecting..."), 12, 68, false);

            // 卡片2: 用量
            Panel pnlUsage = new Panel { Location = new Point(20, 175), Size = new Size(330, 130), BackColor = cardBg };
            containerPanel.Controls.Add(pnlUsage);
            MakeInfoLabel(pnlUsage, T("账户余额", "Balance"), 12, 12, false);
            lblBal = MakeInfoLabel(pnlUsage, "…", 130, 10, true);
            lblBal.ForeColor = green;
            lblBal.Font = new Font("Consolas", 12, FontStyle.Bold);
            MakeInfoLabel(pnlUsage, T("今日 Token", "Today Tokens"), 12, 42, false);
            lblTok = MakeInfoLabel(pnlUsage, "…", 130, 40, true);
            lblTok.Font = new Font("Consolas", 10, FontStyle.Bold);
            MakeInfoLabel(pnlUsage, T("大约花费", "Est. Cost"), 12, 72, false);
            lblCost = MakeInfoLabel(pnlUsage, "…", 130, 70, true);
            lblCost.Font = new Font("Consolas", 10, FontStyle.Bold);
            lblTime = MakeInfoLabel(pnlUsage, "", 130, 100, false);
            lblTime.Font = new Font("Consolas", 8);

            // 开机自启
            chkAutoStart = new CheckBox {
                Text = T("开机自动启动控制中心", "Start with Windows"), ForeColor = fg,
                Font = new Font("Microsoft YaHei UI", 9.5f),
                Location = new Point(20, 320), AutoSize = true, Checked = IsAutoStartEnabled()
            };
            chkAutoStart.CheckedChanged += (s, e) => SetAutoStart(chkAutoStart.Checked);
            containerPanel.Controls.Add(chkAutoStart);

            // 特效按钮
            btnGateway = CreateBtn(T("开关网关", "Toggle Gateway"), new Rectangle(20, 355, 158, 40), accent,
                Color.FromArgb(79, 150, 255), Color.FromArgb(40, 100, 210), () => { ToggleGateway(); UpdateUI(); });
            containerPanel.Controls.Add(btnGateway);

            btnBall = CreateBtn(T("切换悬浮球", "Toggle Ball"), new Rectangle(192, 355, 158, 40),
                Color.FromArgb(45, 48, 64), Color.FromArgb(65, 70, 90), Color.FromArgb(35, 38, 50), () => {
                    if (ballForm.Visible) ballForm.Hide();
                    else ballForm.Show();
                });
            containerPanel.Controls.Add(btnBall);

            btnRefresh = CreateBtn(T("⟳ 刷新用量", "⟳ Refresh Usage"), new Rectangle(20, 405, 158, 38),
                Color.FromArgb(45, 48, 64), Color.FromArgb(65, 70, 90), Color.FromArgb(35, 38, 50), () => RefreshData());
            containerPanel.Controls.Add(btnRefresh);

            btnSite = CreateBtn(DynamicSiteName, new Rectangle(192, 405, 158, 38),
                Color.FromArgb(45, 48, 64), Color.FromArgb(65, 70, 90), Color.FromArgb(35, 38, 50), () => {
                    try { Process.Start(new ProcessStartInfo(DynamicSiteUrl) { UseShellExecute = true }); } catch {}
                });
            containerPanel.Controls.Add(btnSite);

            // 定时
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 3000; t.Tick += (s, e) => UpdateUI(); t.Start();
            System.Windows.Forms.Timer t2 = new System.Windows.Forms.Timer();
            t2.Interval = 60000; t2.Tick += (s, e) => RefreshData(); t2.Start();

            UpdateUI();
            RefreshData();
        }

        Label MakeInfoLabel(Panel parent, string text, int x, int y, bool bold) {
            Label l = new Label {
                Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = fg,
                Font = new Font("Microsoft YaHei UI", 9.5f, bold ? FontStyle.Bold : FontStyle.Regular),
                BackColor = Color.Transparent
            };
            parent.Controls.Add(l);
            return l;
        }
        RoundedBtn CreateBtn(string text, Rectangle r, Color c1, Color c2, Color c3, Action onClick) {
            return new RoundedBtn(text, r, c1, c2, c3, 8, onClick);
        }

        public void UpdateUI() {
            DetectOpenClaw();
            lblStatus.Text = T("网关状态: ", "Gateway: ") + (IsGatewayRunning ? T("● 运行中", "● Running") : T("○ 已停止", "○ Stopped"));
            lblStatus.ForeColor = IsGatewayRunning ? Color.FromArgb(163, 230, 53) : Color.FromArgb(239, 68, 68);
            lblUrl.Text = T("网关地址: ", "Gateway URL: ") + GatewayUrl;
            lblModel.Text = T("当前模型: ", "Model: ") + CurrentModel;
            btnGateway.Text = IsGatewayRunning ? T("关闭网关", "Stop Gateway") : T("启动网关", "Start Gateway");
            btnSite.Text = DynamicSiteName;
        }
        public void UpdateUsage() {
            try {
                lblBal.Text = (bal == "ERR" || bal == NO_KEY) ? (bal == NO_KEY ? T("无Key", "No Key") : bal) : "¥ " + bal;
                lblTok.Text = tokStr;
                lblCost.Text = costStr;
                lblTime.Text = T("更新于 ", "Updated ") + timeStr;
            } catch {}
        }
    }

    // ===== ¥ 悬浮球 (特效) =====
    public class BallForm : Form {
        bool hover = false, press = false;
        public BallForm() {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Width = 64; Height = 64;
            TopMost = true; ShowInTaskbar = false;
            Left = Screen.PrimaryScreen.Bounds.Right - 85; Top = 200;
            DrawBall(false, false);
            MouseEnter += (s, e) => { hover = true; DrawBall(hover, press); };
            MouseLeave += (s, e) => { hover = false; press = false; DrawBall(hover, press); };
            MouseDown += (s, e) => { press = true; DrawBall(hover, press); };
            MouseUp += (s, e) => { press = false; DrawBall(hover, press); };
        }
        protected override CreateParams CreateParams {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x00080000; return cp; }
        }
        bool dragging = false; int sx, sy, wx, wy, moved;
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e); dragging = true; moved = 0;
            sx = Cursor.Position.X; sy = Cursor.Position.Y; wx = Left; wy = Top;
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (dragging) {
                int dx = Cursor.Position.X - sx, dy = Cursor.Position.Y - sy;
                if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3) moved = 1;
                Left = wx + dx; Top = wy + dy;
            }
        }
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e); dragging = false; press = false; DrawBall(hover, press);
            if (moved == 0) OpenClawApp.ToggleCard();
        }
        public void DrawBall(bool hov, bool prs) {
            using (Bitmap bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                int pad = prs ? 7 : (hov ? 3 : 4);
                Rectangle r = new Rectangle(pad, pad, 64 - pad * 2, 64 - pad * 2);
                using (GraphicsPath path = new GraphicsPath()) {
                    path.AddEllipse(r);
                    // 白色底圈
                    using (SolidBrush wb = new SolidBrush(Color.FromArgb(255, 255, 255, 255))) {
                        g.FillPath(wb, path);
                    }
                    // 模型图标（圆形裁剪）- stream 需存活到绘制完成
                    string iconName = OpenClawApp.ModelKey + ".png";
                    bool drewIcon = false;
                    try {
                        using (System.IO.Stream st = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(iconName)) {
                            if (st != null) {
                                using (Image iconImg = Image.FromStream(st)) {
                                    Rectangle ic = new Rectangle(r.Left + 3, r.Top + 3, r.Width - 6, r.Height - 6);
                                    using (GraphicsPath clip = new GraphicsPath()) {
                                        clip.AddEllipse(ic);
                                        g.SetClip(clip);
                                        g.DrawImage(iconImg, ic);
                                        g.ResetClip();
                                    }
                                    drewIcon = true;
                                }
                            }
                        }
                    } catch {}
                    if (!drewIcon) {
                        // 无图标文件时画 ¥ 兜底
                        Color c1 = hov ? Color.FromArgb(255, 130, 165, 255) : Color.FromArgb(255, 107, 149, 255);
                        Color c2 = hov ? Color.FromArgb(255, 60, 100, 220) : Color.FromArgb(255, 45, 85, 196);
                        using (PathGradientBrush brush = new PathGradientBrush(path)) {
                            brush.CenterColor = c1;
                            brush.SurroundColors = new Color[] { c2 };
                            brush.CenterPoint = new PointF(r.Left + 18, r.Top + 16);
                            g.FillPath(brush, path);
                        }
                        using (Font f = new Font("Microsoft YaHei UI", 18, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (SolidBrush fb = new SolidBrush(Color.White)) {
                            var sz = g.MeasureString("\u00A5", f);
                            g.DrawString("\u00A5", f, fb, r.Left + (r.Width - sz.Width) / 2, r.Top + (r.Height - sz.Height) / 2);
                        }
                    }
                    // 外发光（hover）
                    if (hov) {
                        using (Pen glow = new Pen(Color.FromArgb(120, 120, 170, 255), 2)) {
                            g.DrawEllipse(glow, r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
                        }
                    }
                }
                SetBitmap(bmp);
            }
        }

        public bool IsHover() { return hover; }
        public bool IsPress() { return press; }

        private void SetBitmap(Bitmap bitmap) {
            IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr memDc = NativeMethods.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            IntPtr oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);
            try {
                NativeMethods.POINT loc = new NativeMethods.POINT(Left, Top);
                NativeMethods.SIZE sz = new NativeMethods.SIZE(Width, Height);
                NativeMethods.BLENDFUNCTION blend = new NativeMethods.BLENDFUNCTION {
                    BlendOp = NativeMethods.AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = NativeMethods.AC_SRC_ALPHA
                };
                NativeMethods.POINT src = new NativeMethods.POINT(0, 0);
                NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref loc, ref sz, memDc, ref src, 0, ref blend, NativeMethods.ULW_ALPHA);
            } finally {
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero) { NativeMethods.SelectObject(memDc, oldBitmap); NativeMethods.DeleteObject(hBitmap); }
                NativeMethods.DeleteDC(memDc);
            }
        }
    }

    // ===== 三项数据卡片 =====
    public class CardForm : Form {
        Label lblBal, lblTok, lblCost, lblTime;
        Color bg = Color.FromArgb(24, 26, 36);
        Color headBg = Color.FromArgb(33, 35, 47);
        Color fg = Color.FromArgb(232, 234, 242);
        Color sub = Color.FromArgb(154, 160, 176);
        Color green = Color.FromArgb(163, 230, 53);

        public CardForm() {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Width = 280; Height = 250;
            TopMost = true; ShowInTaskbar = false;
            BackColor = bg; DoubleBuffered = true;
            using (GraphicsPath p = RoundRect(new Rectangle(0, 0, Width, Height), 16)) Region = new Region(p);

            Label head = new Label {
                Text = T("    DeepSeek 用量", "    DeepSeek Usage"),
                Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
                ForeColor = fg, BackColor = headBg, Bounds = new Rectangle(0, 0, Width, 36)
            };
            Controls.Add(head);
            head.MouseDown += (s, e) => { _drag = true; _px = Cursor.Position.X; _py = Cursor.Position.Y; _wx = Left; _wy = Top; };
            head.MouseMove += (s, e) => { if (_drag) { Left = _wx + Cursor.Position.X - _px; Top = _wy + Cursor.Position.Y - _py; } };
            head.MouseUp += (s, e) => { _drag = false; };

            Label close = new Label {
                Text = "✕", Font = new Font("Segoe UI", 11), ForeColor = sub,
                BackColor = headBg, Bounds = new Rectangle(Width - 34, 4, 30, 28),
                TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand
            };
            close.MouseEnter += (s, e) => close.ForeColor = Color.Red;
            close.MouseLeave += (s, e) => close.ForeColor = sub;
            close.Click += (s, e) => OpenClawApp.ClosePanel();
            Controls.Add(close);

            MakeLabel(T("账户余额", "Balance"), new Rectangle(16, 50, 90, 22), sub, 10);
            lblBal = MakeVal(new Rectangle(120, 46, 144, 26), "…", 14, green, true);
            MakeLabel(T("今日 Token", "Today Tokens"), new Rectangle(16, 80, 90, 22), sub, 10);
            lblTok = MakeVal(new Rectangle(120, 78, 144, 22), "…", 11, fg, false);
            MakeLabel(T("大约花费", "Est. Cost"), new Rectangle(16, 108, 90, 22), sub, 10);
            lblCost = MakeVal(new Rectangle(120, 106, 144, 22), "…", 11, fg, false);
            lblTime = MakeVal(new Rectangle(120, 132, 144, 16), "", 8, Color.FromArgb(107, 114, 128), false);

            Controls.Add(new RoundedBtn(T("⟳ 刷新", "⟳ Refresh"), new Rectangle(16, 156, 118, 34),
                Color.FromArgb(59, 130, 246), Color.FromArgb(79, 150, 255), Color.FromArgb(40, 100, 210), 8,
                () => OpenClawApp.RefreshData()));
            Controls.Add(new RoundedBtn(T("官网 ↗", "Site ↗"), new Rectangle(146, 156, 118, 34),
                Color.FromArgb(45, 48, 64), Color.FromArgb(65, 70, 90), Color.FromArgb(35, 38, 50), 8,
                () => { try { Process.Start(new ProcessStartInfo(OFFICIAL_URL) { UseShellExecute = true }); } catch {} }));
            Controls.Add(new RoundedBtn(T("控制中心", "Control Center"), new Rectangle(16, 198, 118, 30),
                Color.FromArgb(45, 48, 64), Color.FromArgb(65, 70, 90), Color.FromArgb(35, 38, 50), 8,
                () => { OpenClawApp.ClosePanel(); OpenClawApp.mainForm.Show(); OpenClawApp.mainForm.Activate(); }));
            Controls.Add(new RoundedBtn(T("退出", "Exit"), new Rectangle(146, 198, 118, 30),
                Color.FromArgb(239, 68, 68), Color.FromArgb(248, 113, 113), Color.FromArgb(185, 28, 28), 8,
                () => {
                    // 退出 = 关闭悬浮球（隐藏球+卡片），控制中心不受影响
                    OpenClawApp.ClosePanel();
                    OpenClawApp.ballForm.Hide();
                    if (!OpenClawApp.mainForm.Visible) {
                        OpenClawApp.mainForm.Show();
                        OpenClawApp.mainForm.Activate();
                    }
                }));
        }
        bool _drag = false; int _px, _py, _wx, _wy;
        Label MakeLabel(string text, Rectangle r, Color c, float size) {
            Label l = new Label {
                Text = text, Bounds = r, ForeColor = c, BackColor = bg,
                Font = new Font("Microsoft YaHei UI", size), TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(l); return l;
        }
        Label MakeVal(Rectangle r, string text, float size, Color c, bool bold) {
            Label l = new Label {
                Text = text, Bounds = r, ForeColor = c, BackColor = bg,
                Font = new Font("Consolas", size, bold ? FontStyle.Bold : FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleRight
            };
            Controls.Add(l); return l;
        }
        public void UpdateUsage() {
            try {
                lblBal.Text = (bal == "ERR" || bal == NO_KEY) ? (bal == NO_KEY ? T("无Key", "No Key") : bal) : "¥ " + bal;
                lblTok.Text = tokStr;
                lblCost.Text = costStr;
                lblTime.Text = T("更新于 ", "Updated ") + timeStr;
            } catch {}
        }
    }

    // ===== 控制 =====
    public static void ToggleCard() {
        if (cardVisible) ClosePanel();
        else ShowCard();
    }
    public static void ShowCard() {
        cardVisible = true;
        cardForm.Left = ballForm.Left - 100;
        cardForm.Top = ballForm.Top + 64 + 6;
        var sw = Screen.PrimaryScreen.Bounds;
        if (cardForm.Left + 280 > sw.Right) cardForm.Left = sw.Right - 288;
        if (cardForm.Top + 250 > sw.Bottom) cardForm.Top = sw.Bottom - 258;
        if (cardForm.Left < 0) cardForm.Left = 0;
        if (cardForm.Top < 0) cardForm.Top = 0;
        cardForm.Show();
        cardForm.Activate();
        RefreshData();
    }
    public static void ClosePanel() {
        cardVisible = false;
        cardForm.Hide();
    }

    // ===== Key 输入框（首次运行未检测到 API Key 时弹出） =====
    public class KeyInputForm : Form {
        TextBox txtKey;
        Color bg = Color.FromArgb(20, 22, 30);
        Color cardBg = Color.FromArgb(30, 33, 46);
        Color fg = Color.FromArgb(232, 234, 242);
        Color accent = Color.FromArgb(59, 130, 246);
        Color green = Color.FromArgb(163, 230, 53);

        public KeyInputForm() {
            Text = T("OpenClaw 控制中心 - API Key", "OpenClaw Control Center - API Key");
            Size = new Size(430, 240);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = bg;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Label lblTitle = new Label {
                Text = T("未检测到 API Key", "No API Key Detected"),
                Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold),
                ForeColor = fg, Location = new Point(24, 18), AutoSize = true
            };
            Controls.Add(lblTitle);

            Label lblHint = new Label {
                Text = T("首次使用请粘贴 DeepSeek API Key（sk- 开头）\n将自动保存到 exe 同目录的 ds_key.conf", "First run: paste your DeepSeek API Key (starts with sk-)\nIt will be saved to ds_key.conf next to the EXE"),
                Font = new Font("Microsoft YaHei UI", 9.5f),
                ForeColor = Color.FromArgb(156, 160, 176),
                Location = new Point(24, 52), AutoSize = true
            };
            Controls.Add(lblHint);

            txtKey = new TextBox {
                Location = new Point(24, 100),
                Size = new Size(376, 28),
                BackColor = cardBg, ForeColor = fg,
                Font = new Font("Consolas", 11),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(txtKey);
            txtKey.Focus();

            RoundedBtn btnOk = new RoundedBtn(T("保存", "Save"), new Rectangle(196, 152, 96, 34), accent,
                Color.FromArgb(96, 165, 250), Color.FromArgb(37, 99, 235), 8, () => {
                    string k = txtKey.Text.Trim();
                    if (string.IsNullOrEmpty(k)) { txtKey.Focus(); return; }
                    OpenClawApp.KEY = k;
                    DialogResult = DialogResult.OK;
                    Close();
                });
            Controls.Add(btnOk);

            RoundedBtn btnCancel = new RoundedBtn(T("跳过", "Skip"), new Rectangle(304, 152, 96, 34),
                Color.FromArgb(55, 58, 72), Color.FromArgb(70, 74, 90), Color.FromArgb(40, 43, 55), 8, () => {
                    DialogResult = DialogResult.Cancel;
                    Close();
                });
            Controls.Add(btnCancel);

            AcceptButton = null;
            KeyPreview = true;
            KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter) { btnOk.PerformClick(); }
                else if (e.KeyCode == Keys.Escape) { btnCancel.PerformClick(); }
            };
        }
    }

    [STAThread]
    static void Main() {
        LoadKey();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        ballForm = new BallForm();
        cardForm = new CardForm();
        mainForm = new MainForm();

        // 关闭控制中心时: 若悬浮球开着则隐藏主窗(进程驻留, 球继续运行)
        mainForm.FormClosing += (sender, e) => {
            if (ballForm.Visible) {
                e.Cancel = true;
                mainForm.Hide();
            }
        };

        // 默认只显示控制中心（悬浮球可通过按钮开启）
        RefreshData();
        Application.Run(mainForm);
    }
}
