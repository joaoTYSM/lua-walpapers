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
using MoonSharp.Interpreter;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using NAudio.Wave;

static class Native
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    public delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string t);
    [DllImport("user32.dll")] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string t);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr c, IntPtr p);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref Point p);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern bool SystemParametersInfo(uint a, uint b, string c, uint d);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point p);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint f);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id, HookProc p, IntPtr mod, uint tid);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] public static extern IntPtr GetModuleHandle(string n);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern uint RegisterWindowMessage(string s);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);

    public static string Class(IntPtr h)
    {
        StringBuilder sb = new StringBuilder(64);
        GetClassName(h, sb, 64);
        return sb.ToString();
    }

    public static bool ShellReady()
    {
        if (FindWindow("Shell_TrayWnd", null) == IntPtr.Zero) return false;
        IntPtr pm = FindWindow("Progman", null);
        if (pm == IntPtr.Zero) return false;
        if (FindWindowEx(pm, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) return true;
        bool found = false;
        EnumWindows((h, l) =>
        {
            if (FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) found = true;
            return true;
        }, IntPtr.Zero);
        return found;
    }
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

    const int DefaultsVersion = 2;

    const string LuaIndex = @"<!DOCTYPE html><meta charset=utf-8><style>html,body{margin:0;height:100%;background:#05060d;overflow:hidden}canvas{position:fixed;left:0;top:0}#o{position:fixed;left:24px;bottom:24px;right:24px;color:#7dffb0;font:14px/1.5 Consolas,monospace;white-space:pre-wrap;pointer-events:none;transition:opacity .6s}</style><canvas id=c></canvas><div id=o></div><script>var c=document.getElementById('c'),x=c.getContext('2d'),o=document.getElementById('o'),L=[],T=0;
function fit(){c.width=innerWidth;c.height=innerHeight}fit();addEventListener('resize',fit);
function log(s){L.push(s);if(L.length>12)L.shift();o.textContent=L.join(String.fromCharCode(10));o.style.opacity=1;clearTimeout(T);T=setTimeout(function(){o.style.opacity=0},8000)}
function run(a){for(var i=0;i<a.length;i++){var d=a[i];switch(d[0]){
case 0:x.globalAlpha=1;x.fillStyle=d[1];x.fillRect(0,0,c.width,c.height);break;
case 1:x.globalAlpha=d[2];x.fillStyle=d[1];x.fillRect(0,0,c.width,c.height);x.globalAlpha=1;break;
case 2:if(d[6]){x.fillStyle=d[5];x.fillRect(d[1],d[2],d[3],d[4])}else{x.strokeStyle=d[5];x.strokeRect(d[1],d[2],d[3],d[4])}break;
case 3:x.beginPath();x.arc(d[1],d[2],Math.max(0,d[3]),0,6.2832);if(d[5]){x.fillStyle=d[4];x.fill()}else{x.strokeStyle=d[4];x.stroke()}break;
case 4:x.beginPath();x.moveTo(d[1],d[2]);x.lineTo(d[3],d[4]);x.strokeStyle=d[5];x.lineWidth=d[6];x.stroke();break;
case 5:x.fillStyle=d[4];x.font=d[5]+'px Segoe UI, sans-serif';x.fillText(d[1],d[2],d[3]);break;
}}}
window.luaMsg=function(d){if(d&&d.gfx)return run(d.gfx);if(d&&d.print!==undefined)return log(String(d.print));if(d&&d.lua_error)return log('Lua error: '+d.lua_error);log(typeof d=='string'?d:JSON.stringify(d))};</script>";

    public static bool ValidId(string id)
    {
        return !string.IsNullOrEmpty(id) && id == Path.GetFileName(id) && id != "." && id != "..";
    }

    public static Dictionary<string, object> Settings(string dir)
    {
        Dictionary<string, object> r = new Dictionary<string, object>();
        Dictionary<string, object> props = Read(PropsPath(dir));
        Dictionary<string, object> vals = Read(Path.Combine(dir, "settings.json"));
        foreach (KeyValuePair<string, object> kv in props)
        {
            Dictionary<string, object> def = kv.Value as Dictionary<string, object>;
            if (def == null) continue;
            string t = Str(def, "type");
            if (t == "label" || t == "button") continue;
            r[kv.Key] = vals.ContainsKey(kv.Key) ? vals[kv.Key] : (def.ContainsKey("value") ? def["value"] : null);
        }
        foreach (KeyValuePair<string, object> kv in vals) if (!r.ContainsKey(kv.Key)) r[kv.Key] = kv.Value;
        return r;
    }

    public static string ReadLua(string id, out string name)
    {
        name = null;
        if (!ValidId(id)) return null;
        string d = Path.Combine(Lib, id);
        Dictionary<string, object> m = Read(Path.Combine(d, "wallpaper.json"));
        string script = Str(m, "lua");
        if (script == null || !File.Exists(Path.Combine(d, script))) return null;
        name = Str(m, "name") ?? id;
        return File.ReadAllText(Path.Combine(d, script));
    }

    public static string SaveLua(string id, string name, string code)
    {
        string err = LuaHost.Check(code);
        if (err != null) throw new Exception(err);
        string nm = string.IsNullOrWhiteSpace(name) ? "Lua wallpaper" : name.Trim();
        if (!ValidId(id) || id.StartsWith("default-")) id = "lua-" + Slug(nm) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        string d = Path.Combine(Lib, id);
        Directory.CreateDirectory(d);
        Dictionary<string, object> m = Read(Path.Combine(d, "wallpaper.json"));
        string script = Str(m, "lua") ?? "main.lua";
        if (m.Count == 0)
        {
            m["kind"] = "lua";
            m["entry"] = "index.html";
            m["thumb"] = null;
            m["thumbVideo"] = "False";
            File.WriteAllText(Path.Combine(d, "index.html"), LuaIndex);
        }
        m["name"] = nm;
        m["lua"] = script;
        File.WriteAllText(Path.Combine(d, script), code, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(d, "wallpaper.json"), J.To(m));
        return id;
    }

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
        return Path.Combine(d, "properties.json");
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
            it["id"] = id; it["locked"] = id.StartsWith("default-");
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
        Dictionary<string, object> info = Read(Path.Combine(root, "info.json"));
        string entry = null, kind = "web", thumb = null;
        bool tv = false;
        if (File.Exists(Path.Combine(root, "index.html"))) entry = "index.html";
        else
        {
            string[] h = Directory.GetFiles(root, "*.htm*");
            if (h.Length > 0) entry = Path.GetFileName(h[0]);
        }
        if (entry == null && !all.Any(f => IsMedia(Path.GetExtension(f).ToLowerInvariant())) && all.Any(f => f.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)))
        {
            File.WriteAllText(Path.Combine(root, "index.html"), LuaIndex);
            entry = "index.html";
            kind = "lua";
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
        string keep = null;
        string keepPath = Path.Combine(dest, "settings.json");
        if (Directory.Exists(dest))
        {
            if (File.Exists(keepPath)) keep = File.ReadAllText(keepPath);
            Directory.Delete(dest, true);
        }
        CopyDir(root, dest);
        if (keep != null) File.WriteAllText(keepPath, keep);
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
        if (Directory.Exists(d) && !id.StartsWith("default-") && Path.GetFullPath(d).StartsWith(Lib)) Directory.Delete(d, true);
    }

    public static void SyncDefaults()
    {
        Directory.CreateDirectory(Lib);
        if (!Directory.Exists(Defaults)) return;
        Dictionary<string, object> c = Cfg();
        List<string> done = Strs(c, "imported");
        if (Str(c, "defaultsVersion") != DefaultsVersion.ToString())
        {
            done.Clear();
            c["defaultsVersion"] = DefaultsVersion.ToString();
        }
        foreach (string p in Directory.GetFileSystemEntries(Defaults))
        {
            string n = Path.GetFileName(p);
            if (done.Contains(n) && File.Exists(Path.Combine(Lib, "default-" + Slug(Path.GetFileNameWithoutExtension(n)), "wallpaper.json")) && (!Directory.Exists(Path.Combine(p, "")) || File.Exists(Path.Combine(Lib, "default-" + Slug(Path.GetFileNameWithoutExtension(n)), "properties.json")))) continue;
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
    static int retries;
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

    public static void Reset() { retries = 0; }

    public static void Retry(string id)
    {
        if (retries++ >= 6) return;
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        t.Interval = 3000;
        t.Tick += (s, e) =>
        {
            t.Stop();
            t.Dispose();
            if (Active == id) Apply(id);
        };
        t.Start();
    }

    public static void Halt()
    {
        if (form == null) return;
        WallpaperForm f = form;
        form = null;
        try { f.Close(); } catch { }
    }

    public static void Stop()
    {
        Active = null;
        Halt();
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
    public static void ResetEnv() { env = null; }

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

static class MouseHook
{
    [StructLayout(LayoutKind.Sequential)]
    struct Info { public int X, Y; public uint Data, Flags, Time; public IntPtr Extra; }

    static Native.HookProc proc;
    static IntPtr hook = IntPtr.Zero;
    static Action<int, int, string, int> handler;
    static int buttons;
    static bool dragging, overDesktop;
    static int lastCheck;

    public static void Start(Action<int, int, string, int> h)
    {
        handler = h;
        if (hook != IntPtr.Zero) return;
        proc = Callback;
        hook = Native.SetWindowsHookEx(14, proc, Native.GetModuleHandle(null), 0);
    }

    public static void Stop()
    {
        handler = null;
        if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        buttons = 0;
        dragging = false;
    }

    static bool Desktop(int x, int y)
    {
        IntPtr w = Native.WindowFromPoint(new Point(x, y));
        if (w == IntPtr.Zero) return false;
        string c = Native.Class(Native.GetAncestor(w, 2));
        return c == "Progman" || c == "WorkerW";
    }

    static IntPtr Callback(int code, IntPtr wp, IntPtr lp)
    {
        Action<int, int, string, int> h = handler;
        if (code >= 0 && h != null)
        {
            int m = (int)wp;
            string ev = null;
            int bit = 0;
            bool press = false;
            switch (m)
            {
                case 0x200: ev = "move"; break;
                case 0x201: bit = 1; press = true; break;
                case 0x202: bit = 1; break;
                case 0x204: bit = 2; press = true; break;
                case 0x205: bit = 2; break;
                case 0x207: bit = 4; press = true; break;
                case 0x208: bit = 4; break;
            }
            if (ev != null || bit != 0)
            {
                Info i = (Info)Marshal.PtrToStructure(lp, typeof(Info));
                if (bit != 0)
                {
                    if (press)
                    {
                        buttons |= bit;
                        overDesktop = Desktop(i.X, i.Y);
                        if (overDesktop) dragging = true;
                        ev = "down";
                    }
                    else
                    {
                        buttons &= ~bit;
                        ev = "up";
                    }
                }
                else if (!dragging && Environment.TickCount - lastCheck > 40)
                {
                    lastCheck = Environment.TickCount;
                    overDesktop = Desktop(i.X, i.Y);
                }
                if (dragging || (overDesktop && buttons == 0))
                {
                    h(i.X, i.Y, ev, buttons);
                }
                if (buttons == 0) dragging = false;
            }
        }
        return Native.CallNextHookEx(IntPtr.Zero, code, wp, lp);
    }
}

class MsgForm : Form
{
    readonly uint msg = Native.RegisterWindowMessage("TaskbarCreated");
    public event Action ShellRestarted;

    public MsgForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(1, 1);
    }

    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(false);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == (int)msg && ShellRestarted != null) ShellRestarted();
        base.WndProc(ref m);
    }
}

class LuaHost
{
    static readonly CoreModules Mods = CoreModules.Preset_HardSandbox | CoreModules.Metatables | CoreModules.ErrorHandling | CoreModules.Coroutine | CoreModules.OS_Time;

    readonly Script sc;
    readonly Thread th;
    readonly Action<string> emit;
    readonly Action<string> exec;
    readonly object qk = new object();
    readonly Queue<Action> q = new Queue<Action>();
    readonly List<object> cmds = new List<object>();
    readonly Stopwatch clock = Stopwatch.StartNew();
    double mx, my;
    int mb;
    bool hasMove;
    float[] audio;
    string lastErr;
    int fps = 30;
    volatile bool stop;
    public volatile bool Paused;
    public volatile bool WantAudio;

    public static string Check(string code)
    {
        try { new Script(Mods).LoadString(code); return null; }
        catch (InterpreterException ex) { return ex.DecoratedMessage ?? ex.Message; }
        catch (Exception ex) { return ex.Message; }
    }

    public LuaHost(string code, string name, Dictionary<string, object> settings, int w, int h, Action<string> emit, Action<string> exec)
    {
        this.emit = emit;
        this.exec = exec;
        sc = new Script(Mods);
        sc.Options.DebugPrint = s => Send(new Dictionary<string, object> { { "print", s } });
        Setup(settings, w, h);
        th = new Thread(() => Run(code, name));
        th.IsBackground = true;
        th.Name = "lua_walpapers_lua";
        th.Start();
    }

    static DynValue Cb(Func<ScriptExecutionContext, CallbackArguments, DynValue> f) { return DynValue.NewCallback(f); }
    static double Num(CallbackArguments a, int i, double d) { return a.Count > i && a[i].Type == DataType.Number ? a[i].Number : d; }
    static string Str(CallbackArguments a, int i, string d) { return a.Count > i && a[i].Type == DataType.String ? a[i].String : d; }
    static bool Bool(CallbackArguments a, int i, bool d) { return a.Count > i && a[i].Type == DataType.Boolean ? a[i].Boolean : d; }
    static double R(double v) { return Math.Round(v, 1); }

    void Gfx(params object[] c) { if (cmds.Count < 6000) cmds.Add(c); }

    void Send(object o) { emit(J.To(o)); }

    void Setup(Dictionary<string, object> settings, int w, int h)
    {
        Table g = new Table(sc);
        g.Set("clear", Cb((c, a) => { Gfx(0, Str(a, 0, "#000")); return DynValue.Nil; }));
        g.Set("fade", Cb((c, a) => { Gfx(1, Str(a, 0, "#000"), Num(a, 1, 0.1)); return DynValue.Nil; }));
        g.Set("rect", Cb((c, a) => { Gfx(2, R(Num(a, 0, 0)), R(Num(a, 1, 0)), R(Num(a, 2, 10)), R(Num(a, 3, 10)), Str(a, 4, "#fff"), Bool(a, 5, true)); return DynValue.Nil; }));
        g.Set("circle", Cb((c, a) => { Gfx(3, R(Num(a, 0, 0)), R(Num(a, 1, 0)), R(Num(a, 2, 5)), Str(a, 3, "#fff"), Bool(a, 4, true)); return DynValue.Nil; }));
        g.Set("line", Cb((c, a) => { Gfx(4, R(Num(a, 0, 0)), R(Num(a, 1, 0)), R(Num(a, 2, 0)), R(Num(a, 3, 0)), Str(a, 4, "#fff"), Num(a, 5, 1)); return DynValue.Nil; }));
        g.Set("text", Cb((c, a) => { Gfx(5, Str(a, 0, ""), R(Num(a, 1, 0)), R(Num(a, 2, 0)), Str(a, 3, "#fff"), Num(a, 4, 16)); return DynValue.Nil; }));
        sc.Globals.Set("gfx", DynValue.NewTable(g));

        Table s = new Table(sc);
        s.Set("width", DynValue.NewNumber(w));
        s.Set("height", DynValue.NewNumber(h));
        sc.Globals.Set("screen", DynValue.NewTable(s));
        sc.Globals.Set("settings", ToDyn(settings));

        sc.Globals.Set("send", Cb((c, a) =>
        {
            if (a.Count > 0) emit(a[0].Type == DataType.String ? a[0].String : J.To(FromDyn(a[0], 0)));
            return DynValue.Nil;
        }));
        sc.Globals.Set("js", Cb((c, a) => { if (a.Count > 0 && a[0].Type == DataType.String) exec(a[0].String); return DynValue.Nil; }));
        sc.Globals.Set("time", Cb((c, a) => DynValue.NewNumber(clock.Elapsed.TotalSeconds)));
        sc.Globals.Set("set_fps", Cb((c, a) => { fps = (int)Math.Max(1, Math.Min(120, Num(a, 0, 30))); return DynValue.Nil; }));
        sc.Globals.Set("sleep", Cb((c, a) =>
        {
            int end = Environment.TickCount + (int)Math.Max(0, Num(a, 0, 10));
            do
            {
                Pump();
                Flush();
                Thread.Sleep(8);
            } while (Environment.TickCount < end);
            return DynValue.Nil;
        }));
    }

    static object FromDyn(DynValue v, int depth)
    {
        if (depth > 8) return null;
        switch (v.Type)
        {
            case DataType.Boolean: return v.Boolean;
            case DataType.Number: return v.Number;
            case DataType.String: return v.String;
            case DataType.Table:
                Table t = v.Table;
                int n = t.Length, cnt = 0;
                foreach (TablePair p in t.Pairs) cnt++;
                if (n > 0 && cnt == n)
                {
                    List<object> l = new List<object>();
                    for (int i = 1; i <= n; i++) l.Add(FromDyn(t.Get(i), depth + 1));
                    return l;
                }
                Dictionary<string, object> d = new Dictionary<string, object>();
                foreach (TablePair p in t.Pairs) d[p.Key.CastToString() ?? ""] = FromDyn(p.Value, depth + 1);
                return d;
            default: return null;
        }
    }

    DynValue ToDyn(object o)
    {
        if (o == null) return DynValue.Nil;
        if (o is bool) return DynValue.NewBoolean((bool)o);
        if (o is string) return DynValue.NewString((string)o);
        if (o is Dictionary<string, object>)
        {
            Table t = new Table(sc);
            foreach (KeyValuePair<string, object> kv in (Dictionary<string, object>)o) t.Set(kv.Key, ToDyn(kv.Value));
            return DynValue.NewTable(t);
        }
        if (o is IEnumerable)
        {
            Table t = new Table(sc);
            int i = 1;
            foreach (object x in (IEnumerable)o) t.Set(i++, ToDyn(x));
            return DynValue.NewTable(t);
        }
        try { return DynValue.NewNumber(Convert.ToDouble(o, CultureInfo.InvariantCulture)); }
        catch { return DynValue.Nil; }
    }

    bool Has(string fn)
    {
        DynValue f = sc.Globals.Get(fn);
        return f.Type == DataType.Function || f.Type == DataType.ClrFunction;
    }

    void Safe(string fn, params DynValue[] args)
    {
        try
        {
            DynValue f = sc.Globals.Get(fn);
            if (f.Type == DataType.Function || f.Type == DataType.ClrFunction) sc.Call(f, args);
        }
        catch (InterpreterException ex) { Report(ex.DecoratedMessage ?? ex.Message); }
    }

    void Report(string msg)
    {
        if (msg == lastErr) return;
        lastErr = msg;
        Send(new Dictionary<string, object> { { "lua_error", msg } });
    }

    void Pump()
    {
        WantAudio = Has("on_audio");
        bool mv;
        double x, y;
        int b;
        float[] au;
        List<Action> list = null;
        lock (qk)
        {
            mv = hasMove; x = mx; y = my; b = mb; hasMove = false;
            au = audio; audio = null;
            if (q.Count > 0) { list = new List<Action>(q); q.Clear(); }
        }
        if (mv) Safe("on_mouse", DynValue.NewNumber(x), DynValue.NewNumber(y), DynValue.NewString("move"), DynValue.NewNumber(b));
        if (list != null) foreach (Action a in list) a();
        if (au != null && WantAudio)
        {
            Table t = new Table(sc);
            for (int i = 0; i < au.Length; i++) t.Set(i + 1, DynValue.NewNumber(au[i]));
            Safe("on_audio", DynValue.NewTable(t));
        }
    }

    void Flush()
    {
        if (cmds.Count == 0) return;
        string j = J.To(new Dictionary<string, object> { { "gfx", cmds } });
        cmds.Clear();
        emit(j);
    }

    void Run(string code, string name)
    {
        try
        {
            sc.DoString(code, null, name);
            double last = clock.Elapsed.TotalSeconds;
            while (!stop)
            {
                if (Paused) { Thread.Sleep(100); last = clock.Elapsed.TotalSeconds; continue; }
                Pump();
                double now = clock.Elapsed.TotalSeconds;
                Safe("on_tick", DynValue.NewNumber(now - last));
                last = now;
                Flush();
                Thread.Sleep(Math.Max(1, 1000 / fps));
            }
        }
        catch (ThreadAbortException) { }
        catch (InterpreterException ex) { Report(ex.DecoratedMessage ?? ex.Message); }
        catch (Exception ex) { Report(ex.Message); }
    }

    public void Mouse(int x, int y, string ev, int buttons)
    {
        lock (qk)
        {
            if (ev == "move") { mx = x; my = y; mb = buttons; hasMove = true; return; }
            double px = x, py = y;
            int bb = buttons;
            q.Enqueue(() => Safe("on_mouse", DynValue.NewNumber(px), DynValue.NewNumber(py), DynValue.NewString(ev), DynValue.NewNumber(bb)));
        }
    }

    public void Message(string s)
    {
        lock (qk) q.Enqueue(() => Safe("on_message", DynValue.NewString(s ?? "")));
    }

    public void Property(string name, object val)
    {
        lock (qk) q.Enqueue(() => Safe("on_property", DynValue.NewString(name), ToDyn(val)));
    }

    public void Audio(float[] bins)
    {
        lock (qk) audio = bins;
    }

    public void Stop()
    {
        stop = true;
        try { if (!th.Join(250)) th.Abort(); } catch { }
    }
}

class WallpaperForm : Form
{
    const int N = 1024;
    const int BINS = 128;
    static readonly float[] Win = new float[N];
    static readonly float[] TwC = new float[N / 2];
    static readonly float[] TwS = new float[N / 2];
    static readonly int[] Rev = new int[N];
    readonly float[] ring = new float[N];
    readonly float[] re = new float[N];
    readonly float[] im = new float[N];
    readonly float[] bins = new float[BINS];
    readonly WebView2 web = new WebView2();
    readonly System.Windows.Forms.Timer watch = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer pump = new System.Windows.Forms.Timer();
    string target;
    WasapiLoopbackCapture capture;
    LuaHost lua;
    IntPtr host = IntPtr.Zero;
    string scriptId, cur, curDir;
    Dictionary<string, object> curMeta;
    int fill;
    int channels = 2;
    volatile bool ready;
    volatile bool paused;
    volatile bool closing;
    bool pauseEnabled = true;
    bool silentSent;
    bool moveDirty;
    double moveX, moveY;
    int moveButtons;

    static WallpaperForm()
    {
        for (int i = 0; i < N; i++)
        {
            Win[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1)));
            int r = 0;
            for (int b = 0; b < 10; b++) if ((i & (1 << b)) != 0) r |= 1 << (9 - b);
            Rev[i] = r;
        }
        for (int k = 0; k < N / 2; k++)
        {
            TwC[k] = (float)Math.Cos(2 * Math.PI * k / N);
            TwS[k] = (float)Math.Sin(2 * Math.PI * k / N);
        }
    }

    public WallpaperForm(string id)
    {
        target = id;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        web.Dock = DockStyle.Fill;
        Controls.Add(web);
        watch.Interval = 1000;
        watch.Tick += (s, e) => Watch();
        pump.Interval = 16;
        pump.Tick += (s, e) =>
        {
            if (!moveDirty) return;
            moveDirty = false;
            SendMouse(moveX, moveY, "move", moveButtons);
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Dictionary<string, object> c = Store.Cfg();
        if (c.ContainsKey("pauseWhenCovered") && c["pauseWhenCovered"] is bool && !(bool)c["pauseWhenCovered"]) pauseEnabled = false;
        Attach();
        watch.Start();
        pump.Start();
        MouseHook.Start(OnMouse);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplay;
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
        host = workerw;
        Native.SetParent(Handle, workerw);
        Place();
    }

    void Place()
    {
        if (host == IntPtr.Zero) return;
        Rectangle b = Screen.PrimaryScreen.Bounds;
        Point pt = new Point(b.X, b.Y);
        Native.ScreenToClient(host, ref pt);
        Bounds = new Rectangle(pt.X, pt.Y, b.Width, b.Height);
    }

    void OnDisplay(object sender, EventArgs e)
    {
        try { BeginInvoke(new Action(Place)); } catch { }
    }

    async void Init()
    {
        try { await web.EnsureCoreWebView2Async(await App.Env()); }
        catch
        {
            App.ResetEnv();
            try { BeginInvoke(new Action(() => { string id = target; Wall.Halt(); Wall.Retry(id); })); } catch { }
            return;
        }
        CoreWebView2 c = web.CoreWebView2;
        c.Settings.AreDefaultContextMenusEnabled = false;
        c.Settings.AreDevToolsEnabled = false;
        c.SetVirtualHostNameToFolderMapping("lib.local", Store.Lib, CoreWebView2HostResourceAccessKind.Allow);
        c.WebMessageReceived += (s, e) =>
        {
            try { if (lua != null) lua.Message(e.TryGetWebMessageAsString()); } catch { }
        };
        web.NavigationCompleted += (s, e) =>
        {
            ready = true;
            StartLua();
            PushAll();
            StartAudio();
        };
        LoadWp(target);
    }

    public async void LoadWp(string id)
    {
        target = id;
        StopLua();
        if (web.CoreWebView2 == null) return;
        ready = false;
        cur = id;
        curDir = Path.Combine(Store.Lib, id);
        curMeta = Store.Read(Path.Combine(curDir, "wallpaper.json"));
        Dictionary<string, object> vals = Store.Read(Path.Combine(curDir, "settings.json"));
        if (scriptId != null) web.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(scriptId);
        scriptId = await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.luaProps=" + J.To(vals) + ";");
        if (Store.Str(curMeta, "kind") == "url") web.CoreWebView2.Navigate(Store.Str(curMeta, "url"));
        else web.CoreWebView2.Navigate("https://lib.local/" + id + "/" + Uri.EscapeUriString(Store.Str(curMeta, "entry") ?? "index.html"));
    }

    void PushAll()
    {
        if (cur == null) return;
        foreach (KeyValuePair<string, object> kv in Store.Settings(curDir)) Push(kv.Key, kv.Value);
    }

    public void Push(string name, object val)
    {
        if (lua != null) lua.Property(name, val);
        if (!ready || web.CoreWebView2 == null) return;
        web.CoreWebView2.ExecuteScriptAsync("if(window.wallpaperProperty)wallpaperProperty(" + J.To(name) + "," + J.To(val) + ");");
    }

    void StartLua()
    {
        StopLua();
        if (curMeta == null) return;
        string script = Store.Str(curMeta, "lua");
        if (script == null) return;
        string path = Path.Combine(curDir, script);
        if (!File.Exists(path)) return;
        Rectangle b = Screen.PrimaryScreen.Bounds;
        lua = new LuaHost(File.ReadAllText(path), script, Store.Settings(curDir), b.Width, b.Height, LuaLine, RunJs);
        lua.Paused = paused;
    }

    void LuaLine(string line)
    {
        string js = "(function(l){var d=l;try{d=JSON.parse(l)}catch(e){}window.dispatchEvent(new CustomEvent('lua-msg',{detail:d}));if(window.luaMsg)luaMsg(d);})(" + J.To(line) + ")";
        RunJs(js);
    }

    void RunJs(string js)
    {
        try { BeginInvoke(new Action(() => { if (web.CoreWebView2 != null) web.CoreWebView2.ExecuteScriptAsync(js); })); } catch { }
    }

    void StopLua()
    {
        LuaHost l = lua;
        lua = null;
        if (l != null) l.Stop();
    }

    void OnMouse(int x, int y, string ev, int buttons)
    {
        if (!ready || paused) return;
        Rectangle sb = Screen.PrimaryScreen.Bounds;
        int px = x - sb.Left, py = y - sb.Top;
        bool outside = px < 0 || py < 0 || px >= sb.Width || py >= sb.Height;
        if (outside && ev != "up") return;
        px = Math.Max(0, Math.Min(sb.Width - 1, px));
        py = Math.Max(0, Math.Min(sb.Height - 1, py));
        if (lua != null) lua.Mouse(px, py, ev, buttons);
        double nx = (double)px / sb.Width, ny = (double)py / sb.Height;
        if (ev == "move")
        {
            moveX = nx; moveY = ny; moveButtons = buttons; moveDirty = true;
            return;
        }
        if (moveDirty) { moveDirty = false; SendMouse(moveX, moveY, "move", moveButtons); }
        SendMouse(nx, ny, ev, buttons);
    }

    void SendMouse(double nx, double ny, string ev, int buttons)
    {
        if (web.CoreWebView2 == null) return;
        string js = "(function(x,y,e,b){window.dispatchEvent(new CustomEvent('host-mouse',{detail:{x:x,y:y,type:e,buttons:b}}));if(window.hostMouse)hostMouse(x,y,e,b);})(" +
            nx.ToString("0.####", CultureInfo.InvariantCulture) + "," + ny.ToString("0.####", CultureInfo.InvariantCulture) + ",'" + ev + "'," + buttons + ")";
        web.CoreWebView2.ExecuteScriptAsync(js);
    }

    void Watch()
    {
        if (ready && capture == null && !paused) StartAudio();
        bool p = pauseEnabled && Covered();
        if (p == paused) return;
        paused = p;
        LuaHost l = lua;
        if (l != null) l.Paused = p;
        if (ready && web.CoreWebView2 != null)
            web.CoreWebView2.ExecuteScriptAsync("if(window.wallpaperPlaybackChanged)wallpaperPlaybackChanged('{\"IsPaused\":" + (p ? "true" : "false") + "}');");
    }

    bool Covered()
    {
        IntPtr h = Native.GetForegroundWindow();
        if (h == IntPtr.Zero || h == Handle) return false;
        if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return false;
        string c = Native.Class(h);
        if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return false;
        int cloaked;
        if (Native.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return false;
        Native.RECT r;
        if (!Native.GetWindowRect(h, out r)) return false;
        Rectangle w = Screen.PrimaryScreen.WorkingArea;
        return r.Left <= w.Left + 1 && r.Top <= w.Top + 1 && r.Right >= w.Right - 1 && r.Bottom >= w.Bottom - 1;
    }

    void StartAudio()
    {
        if (capture != null || closing) return;
        try
        {
            WasapiLoopbackCapture cap = new WasapiLoopbackCapture();
            channels = cap.WaveFormat.Channels;
            fill = 0;
            cap.DataAvailable += OnData;
            cap.RecordingStopped += (s, e) =>
            {
                if (closing) return;
                try { BeginInvoke(new Action(ResetAudio)); } catch { }
            };
            cap.StartRecording();
            capture = cap;
        }
        catch { capture = null; }
    }

    void ResetAudio()
    {
        WasapiLoopbackCapture c = capture;
        capture = null;
        try { if (c != null) c.Dispose(); } catch { }
    }

    void OnData(object sender, WaveInEventArgs e)
    {
        WasapiLoopbackCapture cap = capture;
        if (!ready || paused || cap == null || cap.WaveFormat.BitsPerSample != 32) return;
        int frames = e.BytesRecorded / (4 * channels);
        for (int i = 0; i < frames; i++)
        {
            float m = 0;
            for (int c = 0; c < channels; c++) m += BitConverter.ToSingle(e.Buffer, (i * channels + c) * 4);
            ring[fill++] = m / channels;
            if (fill == N) { fill = 0; Analyze(); }
        }
    }

    void Fft()
    {
        for (int i = 0; i < N; i++)
        {
            int j = Rev[i];
            re[j] = ring[i] * Win[i];
            im[j] = 0;
        }
        for (int size = 2; size <= N; size <<= 1)
        {
            int half = size >> 1, step = N / size;
            for (int start = 0; start < N; start += size)
            {
                for (int k = 0, t = 0; k < half; k++, t += step)
                {
                    float wr = TwC[t], wi = -TwS[t];
                    int a = start + k, b = a + half;
                    float xr = re[b] * wr - im[b] * wi;
                    float xi = re[b] * wi + im[b] * wr;
                    re[b] = re[a] - xr;
                    im[b] = im[a] - xi;
                    re[a] += xr;
                    im[a] += xi;
                }
            }
        }
    }

    void Analyze()
    {
        double energy = 0;
        for (int n = 0; n < N; n++) energy += ring[n] * ring[n];
        bool silent = Math.Sqrt(energy / N) < 0.0004;
        if (silent)
        {
            if (silentSent) return;
            silentSent = true;
            for (int k = 0; k < BINS; k++) bins[k] = 0;
        }
        else
        {
            silentSent = false;
            Fft();
            for (int k = 0; k < BINS; k++)
            {
                double v = Math.Sqrt(re[k + 1] * re[k + 1] + im[k + 1] * im[k + 1]) / N * 16;
                bins[k] = (float)Math.Max(0.0001, Math.Min(1.0, v));
            }
        }
        StringBuilder sb = new StringBuilder("if(window.hostAudio)hostAudio([");
        for (int k = 0; k < BINS; k++)
        {
            if (k > 0) sb.Append(',');
            sb.Append(bins[k].ToString("0.###", CultureInfo.InvariantCulture));
        }
        sb.Append("])");
        string js = sb.ToString();
        LuaHost l = lua;
        float[] copy = l != null && l.WantAudio ? (float[])bins.Clone() : null;
        try
        {
            BeginInvoke(new Action(() =>
            {
                if (web.CoreWebView2 != null) web.CoreWebView2.ExecuteScriptAsync(js);
                if (copy != null && lua != null) lua.Audio(copy);
            }));
        }
        catch { }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        closing = true;
        ready = false;
        watch.Stop();
        pump.Stop();
        MouseHook.Stop();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplay;
        StopLua();
        try { if (capture != null) { capture.StopRecording(); capture.Dispose(); } } catch { }
        capture = null;
        base.OnFormClosed(e);
    }
}

class PanelForm : Form
{
    readonly WebView2 web = new WebView2();
    public bool Exiting;
    bool pageReady, wantEditor;

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

    public void OpenEditor()
    {
        if (pageReady) SendEditor(null, null, null);
        else wantEditor = true;
    }

    void SendEditor(string id, string name, string code)
    {
        Send(new { evt = "luaEditor", id = id, name = name, code = code });
    }

    async void Init()
    {
        await web.EnsureCoreWebView2Async(await App.Env());
        CoreWebView2 c = web.CoreWebView2;
        c.SetVirtualHostNameToFolderMapping("lib.local", Store.Lib, CoreWebView2HostResourceAccessKind.Allow);
        c.WebMessageReceived += OnMsg;
        c.NavigationCompleted += (s, e) =>
        {
            pageReady = true;
            SendList();
            if (wantEditor) { wantEditor = false; SendEditor(null, null, null); }
        };
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
        Wall.Reset();
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
        if (id != null && !Store.ValidId(id)) return;
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
            if (id.StartsWith("default-")) { SendList(); return; }
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
        else if (cmd == "readLua")
        {
            string nm;
            string code = Store.ReadLua(id, out nm);
            if (code != null) SendEditor(id, nm, code);
        }
        else if (cmd == "saveLua")
        {
            bool ap = m.ContainsKey("apply") && Convert.ToBoolean(m["apply"]);
            try
            {
                string nid = Store.SaveLua(id, Store.Str(m, "name"), Store.Str(m, "code") ?? "");
                Send(new { evt = "luaSaved", id = nid });
                if (ap || Wall.Active == nid) Apply(nid); else SendList();
            }
            catch (Exception ex) { Send(new { evt = "error", msg = ex.Message }); }
        }
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
    readonly MsgForm msg = new MsgForm();
    readonly EventWaitHandle showEvt;
    PanelForm panel;

    public TrayContext(EventWaitHandle show, bool silent)
    {
        showEvt = show;
        IntPtr h = sync.Handle;
        IntPtr h2 = msg.Handle;
        Store.SyncDefaults();
        Dictionary<string, object> c = Store.Cfg();
        if (!c.ContainsKey("linked"))
        {
            try { App.Link(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "lua_walpapers.lnk"), ""); } catch { }
            c["linked"] = true;
        }
        if (!c.ContainsKey("startupInit"))
        {
            try { App.Link(App.StartupLink(), "--silent"); } catch { }
            c["startupInit"] = true;
        }
        else if (File.Exists(App.StartupLink()))
        {
            try { App.Link(App.StartupLink(), "--silent"); } catch { }
        }
        Store.SaveCfg(c);
        tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        tray.Text = "lua_walpapers";
        tray.Visible = true;
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Open panel", null, (s, e) => ShowPanel());
        menu.Items.Add("Lua wallpaper...", null, (s, e) => { ShowPanel(); panel.OpenEditor(); });
        menu.Items.Add("Stop wallpaper", null, (s, e) => { Wall.Stop(); });
        menu.Items.Add("Exit", null, (s, e) => ExitAll());
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (s, e) => ShowPanel();
        msg.ShellRestarted += OnShellRestart;
        ThreadPool.RegisterWaitForSingleObject(showEvt, (st, to) =>
        {
            try { sync.BeginInvoke(new Action(ShowPanel)); } catch { }
        }, null, -1, false);
        string active = Store.Str(c, "active");
        if (active != null && Directory.Exists(Path.Combine(Store.Lib, active))) StartActive(active, silent ? 3 : 0);
        if (!silent) ShowPanel();
    }

    void StartActive(string id, int minTicks)
    {
        int n = 0;
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        t.Interval = 700;
        t.Tick += (s, e) =>
        {
            n++;
            if (n < minTicks) return;
            if (Native.ShellReady() || n > 130)
            {
                t.Stop();
                t.Dispose();
                Wall.Apply(id);
            }
        };
        t.Start();
    }

    void OnShellRestart()
    {
        string id = Wall.Active;
        if (id == null) return;
        Wall.Halt();
        StartActive(id, 3);
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
