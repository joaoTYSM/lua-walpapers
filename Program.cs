using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using NAudio.Wave;

static class Native
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string t);
    [DllImport("user32.dll")] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string t);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr c, IntPtr p);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref Point p);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern bool SystemParametersInfo(uint a, uint b, string c, uint d);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}

static class J
{
    static readonly JavaScriptSerializer S = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    public static string To(object o) { return S.Serialize(o); }
    public static Dictionary<string, object> Obj(string s) { return S.DeserializeObject(s) as Dictionary<string, object>; }
}

static class Store
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "lua_walpapers");
    public static readonly string Lib = Path.Combine(Root, "library");
    public static readonly string Defaults = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "defaults");
    static readonly string CfgPath = Path.Combine(Root, "config.json");
    static readonly string[] Vid = { ".mp4", ".webm", ".mkv", ".mov", ".m4v", ".ogv" };
    static readonly string[] Img = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".avif", ".svg" };
    static readonly string[] Bad = { "luaobfuscator", "luasrcdiet", "loadstring(string.char", "loadstring(\"\\", "load(string.char", "string.dump", "ironbrew", "moonsec", "prometheus obf" };
    static readonly Regex ExecDecode = new Regex(@"(exec|eval)\s*\(\s*(compile|__import__|base64|zlib|marshal|codecs|bytes|bytearray|bz2|lzma|\w+\.(decompress|b64decode|loads|decode|fromhex))", RegexOptions.IgnoreCase);

    static bool IsMedia(string e) { return Vid.Contains(e) || Img.Contains(e); }

    public static Dictionary<string, object> Read(string p)
    {
        try { if (File.Exists(p)) return J.Obj(File.ReadAllText(p)) ?? new Dictionary<string, object>(); } catch { }
        return new Dictionary<string, object>();
    }

    public static Dictionary<string, object> Cfg()
    {
        Directory.CreateDirectory(Root);
        return Read(CfgPath);
    }

    public static void SaveCfg(Dictionary<string, object> c) { File.WriteAllText(CfgPath, J.To(c)); }

    public static List<string> Strs(Dictionary<string, object> c, string k)
    {
        List<string> r = new List<string>();
        if (c.ContainsKey(k) && c[k] is IEnumerable && !(c[k] is string)) foreach (object o in (IEnumerable)c[k]) r.Add(Convert.ToString(o));
        return r;
    }

    public static string Str(Dictionary<string, object> d, string k)
    {
        return d.ContainsKey(k) && d[k] != null ? Convert.ToString(d[k]) : null;
    }

    static string PropsPath(string d)
    {
        string a = Path.Combine(d, "LivelyProperties.json");
        return File.Exists(a) ? a : Path.Combine(d, "properties.json");
    }

    public static List<object> List()
    {
        List<object> r = new List<object>();
        if (!Directory.Exists(Lib)) return r;
        foreach (string d in Directory.GetDirectories(Lib))
        {
            string id = Path.GetFileName(d);
            Dictionary<string, object> m = Read(Path.Combine(d, "wallpaper.json"));
            if (m.Count == 0) continue;
            string th = Str(m, "thumb");
            Dictionary<string, object> it = new Dictionary<string, object>();
            it["id"] = id;
            it["name"] = Str(m, "name") ?? id;
            it["kind"] = Str(m, "kind") ?? "web";
            it["lua"] = Str(m, "lua") != null;
            it["thumb"] = th == null ? null : "https://lib.local/" + id + "/" + Uri.EscapeUriString(th);
            it["thumbVideo"] = Str(m, "thumbVideo") == "True";
            string en = Str(m, "entry"); it["entry"] = en == null ? null : "https://lib.local/" + id + "/" + Uri.EscapeUriString(en);
            it["props"] = Read(PropsPath(d));
            it["values"] = Read(Path.Combine(d, "settings.json"));
            r.Add(it);
        }
        return r;
    }

    public static string Slug(string s)
    {
        string r = Regex.Replace((s ?? "").ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return r.Length == 0 ? "wp" : r;
    }

    public static bool Obfuscated(string t)
    {
        string l = t.ToLowerInvariant();
        foreach (string b in Bad) if (l.Contains(b)) return true;
        if (ExecDecode.IsMatch(t)) return true;
        if (Regex.Matches(t, @"\\x[0-9a-fA-F]{2}").Count > 40) return true;
        if (Regex.Matches(t, @"chr\(").Count > 30) return true;
        if (Regex.IsMatch(t, @"\b[Il1O0]{10,}\b")) return true;
        foreach (string line in t.Split('\n')) if (line.Length > 500 && !line.TrimStart().StartsWith("#")) return true;
        return false;
    }

    static void CopyDir(string s, string d)
    {
        Directory.CreateDirectory(d);
        foreach (string f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)), true);
        foreach (string x in Directory.GetDirectories(s)) CopyDir(x, Path.Combine(d, Path.GetFileName(x)));
    }

    static string Finish(string tmp, string hint, string forced)
    {
        string root = tmp;
        for (; ; )
        {
            string[] ds = Directory.GetDirectories(root);
            if (Directory.GetFiles(root).Length == 0 && ds.Length == 1) root = ds[0]; else break;
        }
        string[] all = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        foreach (string f in all)
        {
            string e = Path.GetExtension(f).ToLowerInvariant();
            if (e == ".luac" || e == ".dll") throw new Exception("Compiled Lua is not accepted");
            if (e == ".lua" && Obfuscated(File.ReadAllText(f))) throw new Exception("Obfuscated Lua rejected: " + Path.GetFileName(f));
        }
        Dictionary<string, object> info = Read(Path.Combine(root, "LivelyInfo.json"));
        string entry = null, kind = "web", thumb = null;
        bool tv = false;
        if (File.Exists(Path.Combine(root, "index.html"))) entry = "index.html";
        else
        {
            string[] h = Directory.GetFiles(root, "*.htm*");
            if (h.Length > 0) entry = Path.GetFileName(h[0]);
        }
        if (entry == null)
        {
            string mf = all.FirstOrDefault(f => IsMedia(Path.GetExtension(f).ToLowerInvariant()));
            if (mf == null) throw new Exception("No html or media file found");
            string rel = mf.Substring(root.Length + 1).Replace('\\', '/');
            bool v = Vid.Contains(Path.GetExtension(mf).ToLowerInvariant());
            string tag = v ? "<video src=\"" + Uri.EscapeUriString(rel) + "\" autoplay loop muted playsinline></video>" : "<img src=\"" + Uri.EscapeUriString(rel) + "\">";
            File.WriteAllText(Path.Combine(root, "index.html"), "<!DOCTYPE html><meta charset=utf-8><style>html,body{margin:0;height:100%;background:#000;overflow:hidden}img,video{width:100%;height:100%;object-fit:cover}</style>" + tag);
            entry = "index.html";
            kind = "media";
            thumb = rel;
            tv = v;
        }
        else
        {
            string it = Str(info, "Thumbnail");
            if (it != null && File.Exists(Path.Combine(root, it))) thumb = it;
            else foreach (string n in new[] { "preview.png", "preview.jpg", "preview.gif", "thumbnail.png", "thumbnail.jpg" }) if (File.Exists(Path.Combine(root, n))) { thumb = n; break; }
        }
        string py = null;
        if (File.Exists(Path.Combine(root, "main.lua"))) py = "main.lua";
        else { string[] ps = Directory.GetFiles(root, "*.lua"); if (ps.Length > 0) py = Path.GetFileName(ps[0]); }
        string name = Str(info, "Title") ?? hint;
        string id = forced ?? Slug(name) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        string dest = Path.Combine(Lib, id);
        if (Directory.Exists(dest)) Directory.Delete(dest, true);
        CopyDir(root, dest);
        Dictionary<string, object> m = new Dictionary<string, object>();
        m["name"] = name; m["kind"] = kind; m["entry"] = entry; m["lua"] = py; m["thumb"] = thumb; m["thumbVideo"] = tv.ToString();
        File.WriteAllText(Path.Combine(dest, "wallpaper.json"), J.To(m));
        return id;
    }

    static string NewTmp()
    {
        string t = Path.Combine(Path.GetTempPath(), "aw_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(t);
        return t;
    }

    static void Del(string t) { try { Directory.Delete(t, true); } catch { } }

    public static string ImportPath(string src, string forced)
    {
        string tmp = NewTmp();
        try
        {
            if (Directory.Exists(src)) CopyDir(src, tmp);
            else if (src.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ZipFile.ExtractToDirectory(src, tmp);
            else File.Copy(src, Path.Combine(tmp, Path.GetFileName(src)));
            return Finish(tmp, Path.GetFileNameWithoutExtension(src.TrimEnd('\\', '/')), forced);
        }
        finally { Del(tmp); }
    }

    public static string ImportFiles(string[] files)
    {
        string tmp = NewTmp();
        try
        {
            foreach (string f in files) File.Copy(f, Path.Combine(tmp, Path.GetFileName(f)), true);
            return Finish(tmp, Path.GetFileNameWithoutExtension(files[0]), null);
        }
        finally { Del(tmp); }
    }

    public static string ImportUrl(string url)
    {
        url = url.Trim();
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        Match gm = Regex.Match(url, @"^https?://github\.com/([^/]+)/([^/#?]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase);
        string dl = gm.Success ? "https://github.com/" + gm.Groups[1].Value + "/" + gm.Groups[2].Value + "/archive/HEAD.zip" : url;
        Uri u = new Uri(dl);
        string ext = Path.GetExtension(u.AbsolutePath).ToLowerInvariant();
        if (gm.Success || ext == ".zip" || IsMedia(ext))
        {
            string tmp = NewTmp();
            try
            {
                string fn = gm.Success ? gm.Groups[2].Value + ".zip" : Path.GetFileName(u.AbsolutePath);
                string file = Path.Combine(tmp, fn);
                using (WebClient wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "lua_walpapers";
                    wc.DownloadFile(dl, file);
                }
                return ImportPath(file, null);
            }
            finally { Del(tmp); }
        }
        string id = Slug(u.Host) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        string d = Path.Combine(Lib, id);
        Directory.CreateDirectory(d);
        Dictionary<string, object> m = new Dictionary<string, object>();
        m["name"] = u.Host; m["kind"] = "url"; m["url"] = url;
        File.WriteAllText(Path.Combine(d, "wallpaper.json"), J.To(m));
        return id;
    }

    public static void Remove(string id)
    {
        string d = Path.Combine(Lib, id);
        if (Directory.Exists(d) && Path.GetFullPath(d).StartsWith(Lib)) Directory.Delete(d, true);
    }

    public static void SyncDefaults()
    {
        Directory.CreateDirectory(Lib);
        if (!Directory.Exists(Defaults)) return;
        Dictionary<string, object> c = Cfg();
        List<string> done = Strs(c, "imported");
        foreach (string p in Directory.GetFileSystemEntries(Defaults))
        {
            string n = Path.GetFileName(p);
            if (done.Contains(n)) continue;
            try { ImportPath(p, "default-" + Slug(Path.GetFileNameWithoutExtension(n))); } catch { }
            done.Add(n);
        }
        c["imported"] = done;
        if (Str(c, "active") == null && Directory.Exists(Path.Combine(Lib, "default-fluid")) && !c.ContainsKey("everApplied")) c["active"] = "default-fluid";
        SaveCfg(c);
    }
}

static class Wall
{
    static WallpaperForm form;
    public static string Active;

    public static void Apply(string id)
    {
        Active = id;
        if (form == null || form.IsDisposed)
        {
            form = new WallpaperForm(id);
            form.FormClosed += (s, e) => { form = null; };
            form.Show();
        }
        else form.LoadWp(id);
    }

    public static void Stop()
    {
        Active = null;
        if (form != null) { WallpaperForm f = form; form = null; f.Close(); }
        Native.SystemParametersInfo(0x14, 0, null, 3);
    }

    public static void Live(string id, string n, object v)
    {
        if (form != null && Active == id) form.Push(n, v);
    }
}

static class App
{
    static Task<CoreWebView2Environment> env;
    public static Task<CoreWebView2Environment> Env()
    {
        if (env == null)
            env = CoreWebView2Environment.CreateAsync(null, Path.Combine(Path.GetTempPath(), "lua_walpapersWV2"), new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
        return env;
    }

    public static void Link(string path, string args)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        dynamic sh = Activator.CreateInstance(t);
        dynamic l = sh.CreateShortcut(path);
        l.TargetPath = Application.ExecutablePath;
        l.Arguments = args;
        l.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
        l.IconLocation = Application.ExecutablePath;
        l.Description = "lua_walpapers";
        l.Save();
    }

    public static string StartupLink()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "lua_walpapers.lnk");
    }
}

class WallpaperForm : Form
{
    const int N = 1024;
    const int BINS = 128;
    static readonly float[] Cos = new float[BINS * N];
    static readonly float[] Sin = new float[BINS * N];
    readonly float[] ring = new float[N];
    readonly WebView2 web = new WebView2();
    readonly string pending;
    WasapiLoopbackCapture capture;
    Process py;
    string scriptId, cur;
    int fill;
    int channels = 2;
    volatile bool ready;

    static WallpaperForm()
    {
        for (int k = 0; k < BINS; k++)
            for (int n = 0; n < N; n++)
            {
                double a = 2 * Math.PI * (k + 1) * n / N;
                Cos[k * N + n] = (float)Math.Cos(a);
                Sin[k * N + n] = (float)Math.Sin(a);
            }
    }

    public WallpaperForm(string id)
    {
        pending = id;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        web.Dock = DockStyle.Fill;
        Controls.Add(web);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Attach();
        Init();
    }

    void Attach()
    {
        IntPtr progman = Native.FindWindow("Progman", null);
        IntPtr r;
        Native.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out r);
        IntPtr workerw = IntPtr.Zero;
        Native.EnumWindows((h, l) =>
        {
            if (Native.FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                workerw = Native.FindWindowEx(IntPtr.Zero, h, "WorkerW", null);
            return true;
        }, IntPtr.Zero);
        if (workerw == IntPtr.Zero) workerw = Native.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        if (workerw == IntPtr.Zero) workerw = progman;
        Rectangle b = Screen.PrimaryScreen.Bounds;
        Native.SetParent(Handle, workerw);
        Point pt = new Point(b.X, b.Y);
        Native.ScreenToClient(workerw, ref pt);
        Bounds = new Rectangle(pt.X, pt.Y, b.Width, b.Height);
    }

    async void Init()
    {
        await web.EnsureCoreWebView2Async(await App.Env());
        CoreWebView2 c = web.CoreWebView2;
        c.Settings.AreDefaultContextMenusEnabled = false;
        c.Settings.AreDevToolsEnabled = false;
        c.SetVirtualHostNameToFolderMapping("lib.local", Store.Lib, CoreWebView2HostResourceAccessKind.Allow);
        c.WebMessageReceived += (s, e) =>
        {
            try { if (py != null && !py.HasExited) py.StandardInput.WriteLine(e.TryGetWebMessageAsString()); } catch { }
        };
        web.NavigationCompleted += (s, e) => { ready = true; PushAll(); StartAudio(); };
        LoadWp(pending);
    }

    public async void LoadWp(string id)
    {
        StopLua();
        if (web.CoreWebView2 == null) return;
        ready = false;
        cur = id;
        string d = Path.Combine(Store.Lib, id);
        Dictionary<string, object> m = Store.Read(Path.Combine(d, "wallpaper.json"));
        Dictionary<string, object> vals = Store.Read(Path.Combine(d, "settings.json"));
        if (scriptId != null) web.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(scriptId);
        scriptId = await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.luaProps=" + J.To(vals) + ";");
        if (Store.Str(m, "kind") == "url") web.CoreWebView2.Navigate(Store.Str(m, "url"));
        else web.CoreWebView2.Navigate("https://lib.local/" + id + "/" + Uri.EscapeUriString(Store.Str(m, "entry") ?? "index.html"));
        StartLua(d, m);
    }

    void PushAll()
    {
        if (cur == null) return;
        string d = Path.Combine(Store.Lib, cur);
        Dictionary<string, object> props = Store.Read(File.Exists(Path.Combine(d, "LivelyProperties.json")) ? Path.Combine(d, "LivelyProperties.json") : Path.Combine(d, "properties.json"));
        Dictionary<string, object> vals = Store.Read(Path.Combine(d, "settings.json"));
        foreach (KeyValuePair<string, object> kv in props)
        {
            Dictionary<string, object> def = kv.Value as Dictionary<string, object>;
            if (def == null) continue;
            string t = Store.Str(def, "type");
            if (t == "label" || t == "button") continue;
            Push(kv.Key, vals.ContainsKey(kv.Key) ? vals[kv.Key] : (def.ContainsKey("value") ? def["value"] : null));
        }
    }

    public void Push(string name, object val)
    {
        if (!ready || web.CoreWebView2 == null) return;
        web.CoreWebView2.ExecuteScriptAsync("if(window.livelyPropertyListener)livelyPropertyListener(" + J.To(name) + "," + J.To(val) + ");");
    }

    void StartLua(string d, Dictionary<string, object> m)
    {
        string script = Store.Str(m, "lua");
        if (script == null) return;
        foreach (string exe in new[] { "python", "lua" })
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, "\"" + Path.Combine(d, script) + "\"");
                psi.WorkingDirectory = d;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardInput = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.EnvironmentVariables["LUA_WALLPAPER_SETTINGS"] = Path.Combine(d, "settings.json");
                py = Process.Start(psi);
                py.OutputDataReceived += (s, e) => { if (e.Data != null) LuaLine(e.Data); };
                py.BeginOutputReadLine();
                return;
            }
            catch { py = null; }
        }
    }

    void LuaLine(string line)
    {
        string js = "(function(l){var d=l;try{d=JSON.parse(l)}catch(e){}window.dispatchEvent(new CustomEvent('lua-msg',{detail:d}));if(window.luaMsg)luaMsg(d);})(" + J.To(line) + ")";
        try { BeginInvoke(new Action(() => { if (web.CoreWebView2 != null) web.CoreWebView2.ExecuteScriptAsync(js); })); } catch { }
    }

    void StopLua()
    {
        try { if (py != null && !py.HasExited) py.Kill(); } catch { }
        py = null;
    }

    void StartAudio()
    {
        if (capture != null) return;
        try
        {
            capture = new WasapiLoopbackCapture();
            channels = capture.WaveFormat.Channels;
            capture.DataAvailable += OnData;
            capture.StartRecording();
        }
        catch { capture = null; }
    }

    void OnData(object sender, WaveInEventArgs e)
    {
        if (!ready || capture == null || capture.WaveFormat.BitsPerSample != 32) return;
        int frames = e.BytesRecorded / (4 * channels);
        for (int i = 0; i < frames; i++)
        {
            float m = 0;
            for (int c = 0; c < channels; c++) m += BitConverter.ToSingle(e.Buffer, (i * channels + c) * 4);
            ring[fill++] = m / channels;
            if (fill == N) { fill = 0; Analyze(); }
        }
    }

    void Analyze()
    {
        double energy = 0;
        for (int n = 0; n < N; n++) energy += ring[n] * ring[n];
        bool silent = Math.Sqrt(energy / N) < 0.0004;
        StringBuilder sb = new StringBuilder("(function(a){if(window.hostAudio)hostAudio(a);else if(window.livelyAudioListener)livelyAudioListener(a);})([");
        for (int k = 0; k < BINS; k++)
        {
            double v = 0;
            if (!silent)
            {
                double re = 0, im = 0;
                int o = k * N;
                for (int n = 0; n < N; n++) { re += ring[n] * Cos[o + n]; im -= ring[n] * Sin[o + n]; }
                v = Math.Max(0.0001, Math.Min(1.0, Math.Sqrt(re * re + im * im) / N * 8));
            }
            if (k > 0) sb.Append(',');
            sb.Append(v.ToString("0.####", CultureInfo.InvariantCulture));
        }
        sb.Append("])");
        string js = sb.ToString();
        try { BeginInvoke(new Action(() => { if (web.CoreWebView2 != null) web.CoreWebView2.ExecuteScriptAsync(js); })); } catch { }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        ready = false;
        StopLua();
        try { if (capture != null) { capture.StopRecording(); capture.Dispose(); } } catch { }
        base.OnFormClosed(e);
    }
}

class PanelForm : Form
{
    readonly WebView2 web = new WebView2();
    public bool Exiting;

    public PanelForm()
    {
        Text = "lua_walpapers";
        Size = new Size(1180, 760);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(11, 11, 22);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        web.Dock = DockStyle.Fill;
        Controls.Add(web);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Init();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!Exiting) { e.Cancel = true; Hide(); }
        base.OnFormClosing(e);
    }

    async void Init()
    {
        await web.EnsureCoreWebView2Async(await App.Env());
        CoreWebView2 c = web.CoreWebView2;
        c.SetVirtualHostNameToFolderMapping("lib.local", Store.Lib, CoreWebView2HostResourceAccessKind.Allow);
        c.WebMessageReceived += OnMsg;
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("panel.html"))
        using (StreamReader r = new StreamReader(s, Encoding.UTF8))
            c.NavigateToString(r.ReadToEnd());
    }

    void Send(object o)
    {
        if (web.CoreWebView2 != null) web.CoreWebView2.PostWebMessageAsJson(J.To(o));
    }

    public void SendList()
    {
        Send(new { evt = "list", items = Store.List(), active = Wall.Active, startup = File.Exists(App.StartupLink()) });
    }

    async void RunImport(Func<string> f)
    {
        Send(new { evt = "busy", on = true });
        try { await Task.Run(f); }
        catch (Exception ex) { Send(new { evt = "error", msg = ex.Message }); }
        Send(new { evt = "busy", on = false });
        SendList();
    }

    public void Apply(string id)
    {
        Dictionary<string, object> m = Store.Read(Path.Combine(Store.Lib, id, "wallpaper.json"));
        Dictionary<string, object> c = Store.Cfg();
        List<string> trusted = Store.Strs(c, "trusted");
        if (Store.Str(m, "lua") != null && !trusted.Contains(id))
        {
            DialogResult r = MessageBox.Show(this, "This wallpaper runs a Lua script on your PC.\nOnly allow it if you trust where it came from.\n\nAllow?", "lua_walpapers", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            trusted.Add(id);
            c["trusted"] = trusted;
        }
        Wall.Apply(id);
        c["active"] = id;
        c["everApplied"] = true;
        Store.SaveCfg(c);
        SendList();
    }

    void OnMsg(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        Dictionary<string, object> m = J.Obj(e.WebMessageAsJson);
        string cmd = Store.Str(m, "cmd"), id = Store.Str(m, "id");
        if (cmd == "list") SendList();
        else if (cmd == "apply") Apply(id);
        else if (cmd == "stop")
        {
            Wall.Stop();
            Dictionary<string, object> c = Store.Cfg();
            c["active"] = null;
            c["everApplied"] = true;
            Store.SaveCfg(c);
            SendList();
        }
        else if (cmd == "remove")
        {
            if (Wall.Active == id) Wall.Stop();
            Store.Remove(id);
            SendList();
        }
        else if (cmd == "save")
        {
            File.WriteAllText(Path.Combine(Store.Lib, id, "settings.json"), J.To(m["values"]));
            if (m.ContainsKey("apply") && Convert.ToBoolean(m["apply"])) Apply(id); else SendList();
        }
        else if (cmd == "live") Wall.Live(id, Store.Str(m, "name"), m["value"]);
        else if (cmd == "addUrl") { string u = Store.Str(m, "url"); RunImport(() => Store.ImportUrl(u)); }
        else if (cmd == "addFiles")
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Multiselect = true;
                d.Filter = "Wallpaper files|*.html;*.htm;*.js;*.css;*.lua;*.json;*.zip;*.png;*.jpg;*.jpeg;*.webp;*.gif;*.bmp;*.mp4;*.webm;*.mkv;*.mov|All files|*.*";
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    string[] f = d.FileNames;
                    if (f.Length == 1 && f[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) RunImport(() => Store.ImportPath(f[0], null));
                    else RunImport(() => Store.ImportFiles(f));
                }
            }
        }
        else if (cmd == "addFolder")
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
                if (d.ShowDialog(this) == DialogResult.OK) { string p = d.SelectedPath; RunImport(() => Store.ImportPath(p, null)); }
        }
        else if (cmd == "openDefaults")
        {
            Directory.CreateDirectory(Store.Defaults);
            Process.Start("explorer.exe", "\"" + Store.Defaults + "\"");
        }
        else if (cmd == "startup")
        {
            try
            {
                if (Convert.ToBoolean(m["on"])) App.Link(App.StartupLink(), "--silent");
                else if (File.Exists(App.StartupLink())) File.Delete(App.StartupLink());
            }
            catch (Exception ex) { Send(new { evt = "error", msg = ex.Message }); }
            SendList();
        }
    }
}

class TrayContext : ApplicationContext
{
    readonly NotifyIcon tray = new NotifyIcon();
    readonly Control sync = new Control();
    readonly EventWaitHandle showEvt;
    PanelForm panel;

    public TrayContext(EventWaitHandle show, bool silent)
    {
        showEvt = show;
        IntPtr h = sync.Handle;
        Store.SyncDefaults();
        Dictionary<string, object> c = Store.Cfg();
        if (!c.ContainsKey("linked"))
        {
            try { App.Link(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "lua_walpapers.lnk"), ""); } catch { }
            c["linked"] = true;
            Store.SaveCfg(c);
        }
        tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        tray.Text = "lua_walpapers";
        tray.Visible = true;
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Open panel", null, (s, e) => ShowPanel());
        menu.Items.Add("Stop wallpaper", null, (s, e) => { Wall.Stop(); });
        menu.Items.Add("Exit", null, (s, e) => ExitAll());
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (s, e) => ShowPanel();
        ThreadPool.RegisterWaitForSingleObject(showEvt, (st, to) =>
        {
            try { sync.BeginInvoke(new Action(ShowPanel)); } catch { }
        }, null, -1, false);
        string active = Store.Str(c, "active");
        if (active != null && Directory.Exists(Path.Combine(Store.Lib, active))) Wall.Apply(active);
        if (!silent) ShowPanel();
    }

    void ShowPanel()
    {
        if (panel == null || panel.IsDisposed) panel = new PanelForm();
        panel.Show();
        panel.WindowState = FormWindowState.Normal;
        panel.Activate();
        panel.SendList();
    }

    void ExitAll()
    {
        Wall.Stop();
        tray.Visible = false;
        tray.Dispose();
        if (panel != null && !panel.IsDisposed) { panel.Exiting = true; panel.Close(); }
        ExitThread();
    }
}

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        bool created;
        Mutex mutex = new Mutex(true, "lua_walpapersMutex", out created);
        EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, "lua_walpapersShow");
        if (!created)
        {
            show.Set();
            return;
        }
        Native.SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.Run(new TrayContext(show, args.Contains("--silent")));
        GC.KeepAlive(mutex);
    }
}
