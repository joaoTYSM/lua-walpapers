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
    public static object Any(string s) { return S.DeserializeObject(s); }
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

    public static string[] Permissions(string dir)
    {
        List<string> r = new List<string>();
        Dictionary<string, object> info = Read(Path.Combine(dir, "info.json"));
        if (info.ContainsKey("permissions") && info["permissions"] is object[])
        {
            foreach (object o in (object[])info["permissions"])
            {
                string v = o as string;
                if ((v == "files" || v == "exec") && !r.Contains(v)) r.Add(v);
            }
        }
        return r.ToArray();
    }

    public static string[] Granted(string id, string dir)
    {
        string[] want = Permissions(dir);
        if (want.Length == 0) return want;
        Dictionary<string, object> c = Cfg();
        Dictionary<string, object> g = c.ContainsKey("granted") ? c["granted"] as Dictionary<string, object> : null;
        if (g == null) g = new Dictionary<string, object>();
        string key = id + ":" + string.Join(",", want);
        if (g.ContainsKey(key)) return (g[key] is bool && (bool)g[key]) ? want : new string[0];
        string what = "";
        foreach (string w in want) what += (w == "files" ? "  - files: read and write any file your account can access\n" : "  - exec: open programs, links and read environment variables\n");
        DialogResult dr = MessageBox.Show("The wallpaper \"" + id + "\" asks for extra access:\n\n" + what + "\nOnly allow this for wallpapers you trust.", "lua_walpapers", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        bool ok = dr == DialogResult.Yes;
        g[key] = ok;
        c["granted"] = g;
        SaveCfg(c);
        return ok ? want : new string[0];
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
            it["pv"] = id.StartsWith("default-") ? Remote.Raw("defaults/" + id.Substring(8) + "/p") : null;
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
            if (forced != null && forced.StartsWith("default-")) foreach (string pn in new[] { "p.png", "p.gif" }) { try { File.Delete(Path.Combine(tmp, pn)); } catch { } }
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

static class Remote
{
    public const string Repo = "joaoTYSM/lua-walpapers";
    public static string Branch = "main";
    public static List<Dictionary<string, object>> Cache = new List<Dictionary<string, object>>();
    public static DateTime Last = DateTime.MinValue;
    static Dictionary<string, List<string>> files = new Dictionary<string, List<string>>();
    static readonly object gate = new object();

    public static string Raw(string path) { return "https://raw.githubusercontent.com/" + Repo + "/" + Branch + "/" + path; }
    public static string LoadingUrl { get { return Raw("loading-bg.mp4"); } }

    static string Get(string url)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
        rq.UserAgent = "lua_walpapers";
        rq.Timeout = 15000;
        rq.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
        using (WebResponse rs = rq.GetResponse())
        using (StreamReader r = new StreamReader(rs.GetResponseStream(), Encoding.UTF8))
            return r.ReadToEnd();
    }

    static string Tree()
    {
        Exception last = null;
        foreach (string br in new[] { Branch, Branch == "main" ? "master" : "main" })
        {
            try { string t = Get("https://api.github.com/repos/" + Repo + "/git/trees/" + br + "?recursive=1"); Branch = br; return t; }
            catch (Exception ex) { last = ex; }
        }
        throw last ?? new Exception("sem resposta");
    }

    // Lists the folders of /defaults in the repo that are not installed yet. Nothing is downloaded except a few KB of JSON.
    public static void Refresh()
    {
        lock (gate)
        {
            try
            {
                Dictionary<string, object> o = J.Obj(Tree());
                Dictionary<string, List<string>> f = new Dictionary<string, List<string>>();
                foreach (object x in (IEnumerable)o["tree"])
                {
                    Dictionary<string, object> e = x as Dictionary<string, object>;
                    if (e == null || Store.Str(e, "type") != "blob") continue;
                    string path = Store.Str(e, "path");
                    string[] pt = path.Split('/');
                    if (pt.Length < 3 || pt[0] != "defaults") continue;
                    if (!f.ContainsKey(pt[1])) f[pt[1]] = new List<string>();
                    f[pt[1]].Add(path);
                }
                files = f;
                List<string> ign = Store.Strs(Store.Cfg(), "ignoredRemote");
                List<Dictionary<string, object>> r = new List<Dictionary<string, object>>();
                foreach (KeyValuePair<string, List<string>> kv in f)
                {
                    string n = kv.Key;
                    if (!Store.ValidId(n) || ign.Contains(n)) continue;
                    if (Directory.Exists(Path.Combine(Store.Defaults, n))) continue;
                    if (Directory.Exists(Path.Combine(Store.Lib, "default-" + Store.Slug(n)))) continue;
                    if (!kv.Value.Any(v => { string b = v.Substring(10 + n.Length); return b != "p.png" && b != "p.gif" && b != "i"; })) continue;
                    string title = n, desc = "";
                    try
                    {
                        Dictionary<string, object> inf = J.Obj(Get(Raw("defaults/" + Uri.EscapeDataString(n) + "/info.json")));
                        title = Store.Str(inf, "Title") ?? n;
                        desc = Store.Str(inf, "Desc") ?? "";
                    }
                    catch { }
                    Dictionary<string, object> it = new Dictionary<string, object>();
                    it["id"] = n; it["name"] = title; it["desc"] = desc;
                    it["pv"] = Raw("defaults/" + Uri.EscapeDataString(n) + "/p");
                    r.Add(it);
                }
                Cache = r;
                Last = DateTime.UtcNow;
            }
            catch (Exception ex) { Maint.Log(ex); }
        }
    }

    public static void Ignore(string name)
    {
        Dictionary<string, object> c = Store.Cfg();
        List<string> l = Store.Strs(c, "ignoredRemote");
        if (!l.Contains(name)) l.Add(name);
        c["ignoredRemote"] = l;
        Store.SaveCfg(c);
        Cache = Cache.Where(x => Store.Str(x, "id") != name).ToList();
    }

    // Downloads the wallpaper files (NOT p.png / p.gif) and installs it as a default.
    public static string Download(string name)
    {
        List<string> list;
        lock (gate) { if (!files.TryGetValue(name, out list)) { Refresh(); files.TryGetValue(name, out list); } }
        if (list == null) throw new Exception("Default não encontrado no GitHub.");
        string tmp = Path.Combine(Path.GetTempPath(), "lw_dl_" + Guid.NewGuid().ToString("N"));
        string dir = Path.Combine(tmp, name);
        Directory.CreateDirectory(dir);
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string pre = "defaults/" + name + "/";
            foreach (string path in list)
            {
                string rel = path.Substring(pre.Length);
                if (rel == "p.png" || rel == "p.gif" || rel == "i" || rel.Split('/').Contains("..")) continue;
                string dst = Path.Combine(dir, rel.Replace('/', '\\'));
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                using (WebClient wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "lua_walpapers";
                    wc.DownloadFile(Raw(Uri.EscapeUriString(path)), dst);
                }
            }
            string id = Store.ImportPath(dir, "default-" + Store.Slug(name));
            Cache = Cache.Where(x => Store.Str(x, "id") != name).ToList();
            return id;
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    // Injected in every wallpaper page: plays loading-bg.mp4 (streamed from raw, never saved) until the page finishes loading.
    public static string LoadingScript()
    {
        Dictionary<string, object> c = Store.Cfg();
        if (c.ContainsKey("loadingVideo") && c["loadingVideo"] is bool && !(bool)c["loadingVideo"]) return "";
        return @"(function(){if(window.top!==window||window.__lwl)return;window.__lwl=1;var U=" + J.To(LoadingUrl) + @",d=document,el,v,t=0,done=0;
function add(){if(el||done||!d.documentElement)return;el=d.createElement('div');el.style.cssText='position:fixed;left:0;top:0;width:100%;height:100%;z-index:2147483647;background:#07070f;transition:opacity .6s;pointer-events:none';
v=d.createElement('video');v.muted=true;v.loop=true;v.autoplay=true;v.setAttribute('playsinline','');v.style.cssText='width:100%;height:100%;object-fit:cover';
v.onerror=function(){if(++t<8)setTimeout(function(){v.src=U+'?r='+t;v.play().catch(function(){})},3000)};v.src=U;el.appendChild(v);d.documentElement.appendChild(el)}
function rm(){if(done)return;done=1;if(!el)return;var e=el;e.style.opacity=0;setTimeout(function(){e.remove()},700)}
add();if(!el){var mo=new MutationObserver(function(){add();if(el)mo.disconnect()});mo.observe(d,{childList:true})}
addEventListener('load',function(){setTimeout(rm,500)});setTimeout(rm,25000)})();";
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
        retries++;
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        t.Interval = Math.Min(3000 * retries, 30000);
        t.Tick += (s, e) =>
        {
            t.Stop();
            t.Dispose();
            if (Active == id) Apply(id);
        };
        t.Start();
    }

    public static void Check()
    {
        string id = Active;
        if (id == null) return;
        if (form == null || form.IsDisposed) { Apply(id); return; }
        if (form.HostGone()) { Halt(); Apply(id); }
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
    static int fallback;
    public static void ResetEnv() { env = null; }
    public static void UseFallback() { fallback++; env = null; }

    public static Task<CoreWebView2Environment> Env()
    {
        if (env == null)
            env = CoreWebView2Environment.CreateAsync(null, Path.Combine(Store.Root, fallback == 0 ? "webview" : "webview_" + fallback), new CoreWebView2EnvironmentOptions(Wv.Args));
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

static class MediaBridge
{
    public static event Action<string> Updated;
    static readonly object gate = new object();
    static Process proc;
    static Thread th;
    static volatile bool stop;
    static bool started;
    static string lastTrack, lastCover, lastLyrics, lastPos, lastNone, lyricKey;

    const string PsSource = @"param($ppid)
$ErrorActionPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, $type) {
  $t = $asTask.MakeGenericMethod($type).Invoke($null, @($op))
  [void]$t.Wait(-1)
  $t.Result
}
[void][Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType = WindowsRuntime]
[void][Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media.Control, ContentType = WindowsRuntime]
[void][Windows.Storage.Streams.IRandomAccessStreamWithContentType, Windows.Storage.Streams, ContentType = WindowsRuntime]
$mgr = Await ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]::RequestAsync()) ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager])
function Emit($o) { [Console]::Out.WriteLine(($o | ConvertTo-Json -Compress -Depth 4)); [Console]::Out.Flush() }
function GetCover($props) {
  try {
    $ref = $props.Thumbnail
    if ($ref -eq $null) { return '' }
    $st = Await ($ref.OpenReadAsync()) ([Windows.Storage.Streams.IRandomAccessStreamWithContentType])
    $net = [System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($st)
    $ms = New-Object System.IO.MemoryStream
    $net.CopyTo($ms)
    $mime = $st.ContentType
    if (-not $mime) { $mime = 'image/jpeg' }
    return 'data:' + $mime + ';base64,' + [Convert]::ToBase64String($ms.ToArray())
  } catch { return '' }
}
$lastKey = ''
$pending = $false
$none = $false
while ($true) {
  if (-not (Get-Process -Id $ppid -ErrorAction SilentlyContinue)) { break }
  try {
    $s = $null
    foreach ($x in $mgr.GetSessions()) { if ($x.SourceAppUserModelId -match 'spotify') { $s = $x; break } }
    if ($s -eq $null) { $s = $mgr.GetCurrentSession() }
    if ($s -eq $null) {
      if (-not $none) { Emit ([ordered]@{ type = 'none' }); $none = $true; $lastKey = '' }
    } else {
      $none = $false
      $props = Await ($s.TryGetMediaPropertiesAsync()) ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties])
      $tl = $s.GetTimelineProperties()
      $playing = ([int]$s.GetPlaybackInfo().PlaybackStatus -eq 4)
      $pos = $tl.Position.TotalSeconds
      if ($playing) { $pos += ([DateTimeOffset]::UtcNow - $tl.LastUpdatedTime).TotalSeconds }
      $dur = $tl.EndTime.TotalSeconds
      $key = '' + $props.Artist + '|' + $props.Title
      if ($key -ne $lastKey) {
        $lastKey = $key
        $cover = GetCover $props
        $pending = ($cover -eq '')
        Emit ([ordered]@{ type = 'track'; title = '' + $props.Title; artist = '' + $props.Artist; album = '' + $props.AlbumTitle; app = '' + $s.SourceAppUserModelId; duration = $dur; pos = $pos; playing = $playing; cover = $cover })
      } elseif ($pending) {
        $cover = GetCover $props
        if ($cover -ne '') { $pending = $false; Emit ([ordered]@{ type = 'cover'; cover = $cover }) }
      }
      Emit ([ordered]@{ type = 'pos'; pos = $pos; duration = $dur; playing = $playing })
    }
  } catch {}
  Start-Sleep -Milliseconds 500
}
";

    public static void Start()
    {
        lock (gate)
        {
            if (started) return;
            Dictionary<string, object> c = Store.Cfg();
            if (c.ContainsKey("spotify") && c["spotify"] is bool && !(bool)c["spotify"]) return;
            started = true;
        }
        th = new Thread(Loop);
        th.IsBackground = true;
        th.Name = "lua_walpapers_media";
        th.Start();
    }

    public static void Stop()
    {
        stop = true;
        try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
    }

    public static List<string> Snapshot()
    {
        List<string> r = new List<string>();
        lock (gate)
        {
            if (lastNone != null) { r.Add(lastNone); return r; }
            if (lastTrack != null) r.Add(lastTrack);
            if (lastCover != null) r.Add(lastCover);
            if (lastLyrics != null) r.Add(lastLyrics);
            if (lastPos != null) r.Add(lastPos);
        }
        return r;
    }

    static void Raise(string json)
    {
        Action<string> h = Updated;
        if (h != null) h(json);
    }

    static void Loop()
    {
        string path = Path.Combine(Store.Root, "media.ps1");
        try
        {
            Directory.CreateDirectory(Store.Root);
            File.WriteAllText(path, PsSource, new UTF8Encoding(true));
        }
        catch (Exception ex) { Maint.Log(ex); return; }
        while (!stop)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + path + "\" " + Process.GetCurrentProcess().Id);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                Process p = Process.Start(psi);
                proc = p;
                string line;
                while ((line = p.StandardOutput.ReadLine()) != null) Handle(line);
                try { p.WaitForExit(1000); } catch { }
            }
            catch (Exception ex) { Maint.Log(ex); }
            for (int i = 0; i < 20 && !stop; i++) Thread.Sleep(250);
        }
    }

    static void Handle(string line)
    {
        line = line.Trim().TrimStart('\uFEFF');
        if (line.Length < 2 || line[0] != '{') return;
        Dictionary<string, object> o = null;
        try { o = J.Obj(line); } catch { }
        if (o == null) return;
        string type = Store.Str(o, "type");
        if (type == "track")
        {
            string artist = Store.Str(o, "artist") ?? "";
            string title = Store.Str(o, "title") ?? "";
            double dur = 0;
            try { dur = Convert.ToDouble(o["duration"], CultureInfo.InvariantCulture); } catch { }
            string key = artist + "|" + title;
            lock (gate)
            {
                lastNone = null; lastTrack = line; lastCover = null; lastLyrics = null; lastPos = null; lyricKey = key;
            }
            Raise(line);
            ThreadPool.QueueUserWorkItem(x => FetchLyrics(key, artist, title, dur));
        }
        else if (type == "cover") { lock (gate) { lastCover = line; } Raise(line); }
        else if (type == "pos") { lock (gate) { lastPos = line; } Raise(line); }
        else if (type == "none")
        {
            lock (gate) { lastTrack = null; lastCover = null; lastLyrics = null; lastPos = null; lastNone = line; lyricKey = null; }
            Raise(line);
        }
    }

    static string Get(string url)
    {
        try
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers[HttpRequestHeader.UserAgent] = "lua_walpapers/1.1";
                return wc.DownloadString(url);
            }
        }
        catch { return null; }
    }

    static List<object> Lrc(string artist, string title, double dur)
    {
        List<object> r = new List<object>();
        string clean = Regex.Split(title, @" - | \(")[0];
        string t = Get("https://lrclib.net/api/get?artist_name=" + Uri.EscapeDataString(artist) + "&track_name=" + Uri.EscapeDataString(title) + "&duration=" + ((int)dur).ToString(CultureInfo.InvariantCulture));
        Dictionary<string, object> d = null;
        try { if (t != null) d = J.Obj(t); } catch { }
        string synced = d == null ? null : Store.Str(d, "syncedLyrics");
        if (string.IsNullOrEmpty(synced))
        {
            string t2 = Get("https://lrclib.net/api/search?q=" + Uri.EscapeDataString(clean + " " + artist));
            object[] arr = null;
            try { if (t2 != null) arr = J.Any(t2) as object[]; } catch { }
            if (arr != null)
            {
                foreach (object x in arr)
                {
                    Dictionary<string, object> xd = x as Dictionary<string, object>;
                    if (xd == null) continue;
                    string sl = Store.Str(xd, "syncedLyrics");
                    if (!string.IsNullOrEmpty(sl)) { synced = sl; break; }
                }
            }
        }
        if (string.IsNullOrEmpty(synced)) return r;
        foreach (string ln in synced.Split('\n'))
        {
            Match m = Regex.Match(ln.Trim(), @"^\[(\d+):(\d+(?:\.\d+)?)\](.*)$");
            if (!m.Success) continue;
            double ts = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            r.Add(new object[] { ts, m.Groups[3].Value.Trim() });
        }
        return r;
    }

    static void FetchLyrics(string key, string artist, string title, double dur)
    {
        List<object> lines = null;
        try { lines = Lrc(artist, title, dur); } catch { }
        string json = J.To(new Dictionary<string, object> { { "type", "lyrics" }, { "key", key }, { "lyrics", lines ?? new List<object>() } });
        lock (gate)
        {
            if (lyricKey != key) return;
            lastLyrics = json;
        }
        Raise(json);
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
    readonly string dataDir;
    readonly string[] perms;
    int inflight;

    public static string Check(string code)
    {
        try { new Script(Mods).LoadString(code); return null; }
        catch (InterpreterException ex) { return ex.DecoratedMessage ?? ex.Message; }
        catch (Exception ex) { return ex.Message; }
    }

    public LuaHost(string code, string name, Dictionary<string, object> settings, int w, int h, Action<string> emit, Action<string> exec, string dataDir, string[] perms)
    {
        this.emit = emit;
        this.exec = exec;
        this.dataDir = dataDir;
        this.perms = perms ?? new string[0];
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

    void Gfx(params object[] c) { if (cmds.Count < 30000) cmds.Add(c); }

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

        Table ht = new Table(sc);
        ht.Set("get", Cb((c, a) => { Http("GET", Str(a, 0, ""), null, a.Count > 1 ? a[1] : DynValue.Nil); return DynValue.Nil; }));
        ht.Set("post", Cb((c, a) => { Http("POST", Str(a, 0, ""), Str(a, 1, ""), a.Count > 2 ? a[2] : DynValue.Nil); return DynValue.Nil; }));
        sc.Globals.Set("http", DynValue.NewTable(ht));

        Table st = new Table(sc);
        st.Set("get", Cb((c, a) =>
        {
            string f = DataFile(Str(a, 0, ""));
            try { if (f != null && File.Exists(f)) return DynValue.NewString(File.ReadAllText(f)); } catch { }
            return DynValue.Nil;
        }));
        st.Set("set", Cb((c, a) =>
        {
            string f = DataFile(Str(a, 0, ""));
            string v = Str(a, 1, "");
            if (f == null || v.Length > 1048576) return DynValue.False;
            try { File.WriteAllText(f, v); return DynValue.True; } catch { return DynValue.False; }
        }));
        st.Set("delete", Cb((c, a) =>
        {
            string f = DataFile(Str(a, 0, ""));
            try { if (f != null && File.Exists(f)) File.Delete(f); } catch { }
            return DynValue.Nil;
        }));
        sc.Globals.Set("storage", DynValue.NewTable(st));

        Table jt = new Table(sc);
        jt.Set("encode", Cb((c, a) => DynValue.NewString(a.Count > 0 ? J.To(FromDyn(a[0], 0)) : "null")));
        jt.Set("decode", Cb((c, a) =>
        {
            try { return ToDyn(J.Any(Str(a, 0, "null"))); } catch { return DynValue.Nil; }
        }));
        sc.Globals.Set("json", DynValue.NewTable(jt));

        sc.Globals.Set("media", DynValue.NewTable(new Table(sc)));

        if (Array.IndexOf(perms, "files") >= 0)
        {
            Table ft = new Table(sc);
            ft.Set("read", Cb((c, a) =>
            {
                try { return DynValue.NewString(File.ReadAllText(Str(a, 0, ""))); } catch { return DynValue.Nil; }
            }));
            ft.Set("write", Cb((c, a) =>
            {
                try { File.WriteAllText(Str(a, 0, ""), Str(a, 1, "")); return DynValue.True; } catch { return DynValue.False; }
            }));
            ft.Set("exists", Cb((c, a) =>
            {
                string p = Str(a, 0, "");
                return DynValue.NewBoolean(File.Exists(p) || Directory.Exists(p));
            }));
            ft.Set("list", Cb((c, a) =>
            {
                try
                {
                    Table t = new Table(sc);
                    int i = 1;
                    foreach (string e in Directory.GetFileSystemEntries(Str(a, 0, "."))) t.Set(i++, DynValue.NewString(Path.GetFileName(e)));
                    return DynValue.NewTable(t);
                }
                catch { return DynValue.Nil; }
            }));
            sc.Globals.Set("fs", DynValue.NewTable(ft));
        }

        if (Array.IndexOf(perms, "exec") >= 0)
        {
            Table sy = new Table(sc);
            sy.Set("open", Cb((c, a) =>
            {
                try { Process.Start(Str(a, 0, "")); return DynValue.True; } catch { return DynValue.False; }
            }));
            sy.Set("run", Cb((c, a) =>
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(Str(a, 0, ""), Str(a, 1, ""));
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    Process.Start(psi);
                    return DynValue.True;
                }
                catch { return DynValue.False; }
            }));
            sy.Set("env", Cb((c, a) =>
            {
                string v = Environment.GetEnvironmentVariable(Str(a, 0, ""));
                return v == null ? DynValue.Nil : DynValue.NewString(v);
            }));
            sc.Globals.Set("sys", DynValue.NewTable(sy));
        }
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

    string DataFile(string key)
    {
        if (string.IsNullOrEmpty(dataDir) || string.IsNullOrEmpty(key)) return null;
        StringBuilder sb = new StringBuilder();
        foreach (char ch in key) sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_');
        if (sb.Length > 80) sb.Length = 80;
        try { Directory.CreateDirectory(dataDir); } catch { return null; }
        return Path.Combine(dataDir, sb.ToString() + ".txt");
    }

    void CallCb(DynValue f, params DynValue[] args)
    {
        try { sc.Call(f, args); }
        catch (InterpreterException ex) { Report(ex.DecoratedMessage ?? ex.Message); }
    }

    void Http(string method, string url, string body, DynValue cb)
    {
        if (!(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))) return;
        if (inflight >= 8) return;
        Interlocked.Increment(ref inflight);
        ThreadPool.QueueUserWorkItem(o =>
        {
            int status = 0;
            string text = "";
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                using (WebClient wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers[HttpRequestHeader.UserAgent] = "lua_walpapers";
                    if (method == "GET") text = wc.DownloadString(url);
                    else
                    {
                        string b = body ?? "";
                        wc.Headers[HttpRequestHeader.ContentType] = b.StartsWith("{") || b.StartsWith("[") ? "application/json" : "application/x-www-form-urlencoded";
                        text = wc.UploadString(url, "POST", b);
                    }
                    status = 200;
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse r = ex.Response as HttpWebResponse;
                status = r != null ? (int)r.StatusCode : 0;
                text = ex.Message;
            }
            catch (Exception ex) { text = ex.Message; }
            Interlocked.Decrement(ref inflight);
            if (cb == null || (cb.Type != DataType.Function && cb.Type != DataType.ClrFunction)) return;
            lock (qk) q.Enqueue(() => CallCb(cb, DynValue.NewNumber(status), DynValue.NewString(text)));
        });
    }

    public void MediaEvent(string json)
    {
        lock (qk) q.Enqueue(() =>
        {
            Dictionary<string, object> o = null;
            try { o = J.Obj(json); } catch { }
            if (o == null) return;
            string type = Store.Str(o, "type");
            o.Remove("cover");
            if (type == "none") sc.Globals.Set("media", DynValue.NewTable(new Table(sc)));
            else if (type == "track") sc.Globals.Set("media", ToDyn(o));
            else if (type == "pos" || type == "lyrics")
            {
                DynValue m = sc.Globals.Get("media");
                if (m.Type == DataType.Table)
                    foreach (KeyValuePair<string, object> kv in o)
                        if (kv.Key != "type" && kv.Key != "key") m.Table.Set(kv.Key, ToDyn(kv.Value));
            }
            else return;
            Safe("on_media", DynValue.NewString(type ?? ""), ToDyn(o));
        });
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
    WebView2 web = new WebView2();
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
        BackColor = Color.FromArgb(7, 7, 15);
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
        MediaBridge.Updated += OnMedia;
        MediaBridge.Start();
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

    public bool HostGone()
    {
        return host != IntPtr.Zero && !Maint.IsWindow(host);
    }

    void OnDisplay(object sender, EventArgs e)
    {
        try { BeginInvoke(new Action(Place)); } catch { }
    }

    async void Init()
    {
        if (!await Wv.Ensure(this, () => web, w => web = w))
        {
            App.ResetEnv();
            try { BeginInvoke(new Action(() => { string id = target; Wall.Halt(); Wall.Retry(id); })); } catch { }
            return;
        }
        CoreWebView2 c = web.CoreWebView2;
        try { web.DefaultBackgroundColor = Color.FromArgb(7, 7, 15); } catch { }
        c.Settings.AreDefaultContextMenusEnabled = false;
        c.Settings.AreDevToolsEnabled = false;
        c.PermissionRequested += (s, e) => { e.State = CoreWebView2PermissionState.Allow; };
        c.NewWindowRequested += (s, e) =>
        {
            e.Handled = true;
            try { if (e.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || e.Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) Process.Start(e.Uri); } catch { }
        };
        c.ProcessFailed += (s, e) =>
        {
            try { BeginInvoke(new Action(() => { string id = target; Wall.Halt(); Wall.Retry(id); })); } catch { }
        };
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
            PushMedia();
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
        scriptId = await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.luaProps=" + J.To(vals) + ";" + Remote.LoadingScript());
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
        lua = new LuaHost(File.ReadAllText(path), script, Store.Settings(curDir), b.Width, b.Height, LuaLine, RunJs, Path.Combine(Store.Root, "data", cur), Store.Granted(cur, curDir));
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

    void OnMedia(string json)
    {
        try { BeginInvoke(new Action(() => DeliverMedia(json))); } catch { }
    }

    void DeliverMedia(string json)
    {
        if (closing || !ready) return;
        if (paused && json.StartsWith("{\"type\":\"pos\"")) return;
        LuaHost l = lua;
        if (l != null) l.MediaEvent(json);
        if (web.CoreWebView2 != null)
            web.CoreWebView2.ExecuteScriptAsync("(function(d){window.dispatchEvent(new CustomEvent('host-media',{detail:d}));if(window.hostMedia)hostMedia(d);})(" + json + ")");
    }

    void PushMedia()
    {
        foreach (string j in MediaBridge.Snapshot()) DeliverMedia(j);
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
        MediaBridge.Updated -= OnMedia;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplay;
        StopLua();
        try { if (capture != null) { capture.StopRecording(); capture.Dispose(); } } catch { }
        capture = null;
        base.OnFormClosed(e);
    }
}

class PanelForm : Form
{
    WebView2 web = new WebView2();
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
        if (!await Wv.Ensure(this, () => web, w => web = w))
        {
            try { await Task.Delay(10000); } catch { }
            if (!IsDisposed) Init();
            return;
        }
        if (IsDisposed) return;
        CoreWebView2 c = web.CoreWebView2;
        c.SetVirtualHostNameToFolderMapping("lib.local", Store.Lib, CoreWebView2HostResourceAccessKind.Allow);
        c.WebMessageReceived += OnMsg;
        c.NavigationCompleted += (s, e) =>
        {
            pageReady = true;
            SendList();
            RefreshRemote();
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
        Send(new { evt = "list", items = Store.List(), active = Wall.Active, startup = File.Exists(App.StartupLink()), fresh = Remote.Cache });
    }

    public async void RefreshRemote()
    {
        if (DateTime.UtcNow - Remote.Last < TimeSpan.FromMinutes(10)) return;
        try { await Task.Run(() => Remote.Refresh()); } catch { }
        if (!IsDisposed && pageReady) SendList();
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
        else if (cmd == "dl") RunImport(() => Remote.Download(id));
        else if (cmd == "ignore") { Remote.Ignore(id); SendList(); }
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
        menu.Items.Add("Atualizar lua_walpapers", null, (s, e) => Maint.Spawn("--update"));
        menu.Items.Add("Deletar e resetar wallpaper", null, (s, e) => Maint.Spawn("--reset"));
        menu.Items.Add("Exit", null, (s, e) => ExitAll());
        tray.ContextMenuStrip = menu;
        Maint.RegisterMenu();
        Maint.Dog = new System.Windows.Forms.Timer();
        Maint.Dog.Interval = 15000;
        Maint.Dog.Tick += (s, e) => { try { Wall.Check(); } catch (Exception ex) { Maint.Log(ex); } };
        Maint.Dog.Start();
        tray.DoubleClick += (s, e) => ShowPanel();
        tray.BalloonTipClicked += (s, e) => ShowPanel();
        System.Windows.Forms.Timer rt = new System.Windows.Forms.Timer();
        rt.Interval = 25000;
        rt.Tick += (s, e) =>
        {
            rt.Interval = 30 * 60 * 1000;
            ThreadPool.QueueUserWorkItem(st =>
            {
                Remote.Refresh();
                try { sync.BeginInvoke(new Action(NotifyFresh)); } catch { }
            });
        };
        rt.Start();
        msg.ShellRestarted += OnShellRestart;
        ThreadPool.RegisterWaitForSingleObject(showEvt, (st, to) =>
        {
            try { sync.BeginInvoke(new Action(ShowPanel)); } catch { }
        }, null, -1, false);
        string active = Store.Str(c, "active");
        if (active != null && Directory.Exists(Path.Combine(Store.Lib, active))) StartActive(active, silent ? 3 : 0);
        if (!silent) ShowPanel();
    }

    readonly HashSet<string> told = new HashSet<string>();

    void NotifyFresh()
    {
        if (panel != null && !panel.IsDisposed && panel.Visible) { panel.SendList(); return; }
        foreach (Dictionary<string, object> it in Remote.Cache)
        {
            string n = Store.Str(it, "id");
            if (!told.Add(n)) continue;
            tray.ShowBalloonTip(6000, "Novo wallpaper default", Store.Str(it, "name") + " — clique para abrir o painel e baixar.", ToolTipIcon.Info);
            break;
        }
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
        Native.SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => Maint.Log(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (s, e) => Maint.Log(e.ExceptionObject as Exception);
        if (args.Contains("--reset")) { Maint.ResetCommand(); return; }
        if (args.Contains("--update")) { Maint.UpdateCommand(); return; }
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
        MediaBridge.Stop();
        GC.KeepAlive(mutex);
    }
}


static class Wv
{
    public const string Args = "--autoplay-policy=no-user-gesture-required --disable-web-security --allow-file-access-from-files --allow-running-insecure-content --disable-site-isolation-trials --disable-features=BlockInsecurePrivateNetworkRequests,PrivateNetworkAccessSendPreflights,PrivateNetworkAccessRespectPreflightResults";

    public static async Task<bool> Ensure(Control host, Func<WebView2> get, Action<WebView2> set)
    {
        for (int i = 0; i < 6; i++)
        {
            bool failed = false;
            try
            {
                await get().EnsureCoreWebView2Async(await App.Env());
                return true;
            }
            catch (Exception ex)
            {
                Maint.Log(ex);
                failed = true;
            }
            if (!failed) continue;
            App.ResetEnv();
            if (i >= 2) App.UseFallback();
            try { await Task.Delay(1200 * (i + 1)); } catch { }
            if (host.IsDisposed) return false;
            try
            {
                WebView2 old = get();
                WebView2 n = new WebView2();
                n.Dock = DockStyle.Fill;
                host.Controls.Remove(old);
                try { old.Dispose(); } catch { }
                host.Controls.Add(n);
                set(n);
            }
            catch { }
        }
        return false;
    }
}

static class Maint
{
    public const string Current = "1.1.0";
    const string Repo = "joaoTYSM/lua-walpapers";
    public static System.Windows.Forms.Timer Dog;

    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);

    public static string InstallDir { get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); } }

    public static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Store.Root);
            File.AppendAllText(Path.Combine(Store.Root, "error.log"), DateTime.Now.ToString("s") + " " + (ex == null ? "?" : ex.ToString()) + "\r\n");
        }
        catch { }
    }

    public static void Spawn(string arg)
    {
        try { Process.Start(Application.ExecutablePath, arg); } catch (Exception ex) { Log(ex); }
    }

    public static void RegisterMenu()
    {
        try
        {
            string exe = Application.ExecutablePath;
            string root = @"Software\Classes\DesktopBackground\Shell\lua_walpapers";
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(root))
            {
                k.SetValue("MUIVerb", "lua_walpapers");
                k.SetValue("Icon", exe);
                k.SetValue("SubCommands", "");
            }
            Sub(root, "01update", "Atualizar lua_walpapers", "--update", exe);
            Sub(root, "02reset", "Deletar e resetar wallpaper", "--reset", exe);
        }
        catch (Exception ex) { Log(ex); }
    }

    static void Sub(string root, string key, string text, string arg, string exe)
    {
        using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(root + @"\shell\" + key))
        {
            k.SetValue("MUIVerb", text);
            k.SetValue("Icon", exe);
        }
        using (var c = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(root + @"\shell\" + key + @"\command"))
            c.SetValue("", "\"" + exe + "\" " + arg);
    }

    static void UnregisterMenu()
    {
        try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\DesktopBackground\Shell\lua_walpapers", false); } catch { }
    }

    static void KillOthers()
    {
        int me = Process.GetCurrentProcess().Id;
        foreach (Process p in Process.GetProcessesByName("lua_walpapers"))
        {
            if (p.Id == me) continue;
            try { p.Kill(); p.WaitForExit(4000); } catch { }
        }
    }

    static bool Unpin()
    {
        bool any = false;
        try
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
            if (Directory.Exists(d))
                foreach (string f in Directory.GetFiles(d, "*.lnk"))
                {
                    bool m = Path.GetFileName(f).ToLower().Contains("lua_walpapers");
                    if (!m)
                    {
                        try
                        {
                            dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                            string t = sh.CreateShortcut(f).TargetPath;
                            m = t != null && t.ToLower().EndsWith("lua_walpapers.exe");
                        }
                        catch { }
                    }
                    if (m) { try { File.Delete(f); any = true; } catch { } }
                }
        }
        catch { }
        return any;
    }

    static void RestartExplorer()
    {
        try
        {
            foreach (Process p in Process.GetProcessesByName("explorer")) { try { p.Kill(); } catch { } }
            Thread.Sleep(1200);
            if (Process.GetProcessesByName("explorer").Length == 0) Process.Start("explorer.exe");
        }
        catch { }
    }

    static void RemoveShortcuts()
    {
        string[] f =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "lua_walpapers.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "lua_walpapers.lnk"),
            App.StartupLink()
        };
        foreach (string x in f) { try { File.Delete(x); } catch { } }
        if (Unpin()) RestartExplorer();
    }

    static void RefreshWallpaper()
    {
        try { Native.SystemParametersInfo(0x14, 0, null, 3); } catch { }
    }

    static void CleanTemp()
    {
        string t = Path.GetTempPath();
        foreach (string pat in new[] { "lua_walpapersWV2*", "aw_*", "lua_walpapers_src", "lua_walpapers_upd" })
            try { foreach (string d in Directory.GetDirectories(t, pat)) { try { Directory.Delete(d, true); } catch { } } } catch { }
    }

    public static void ResetCommand()
    {
        if (MessageBox.Show("Isso remove TUDO do lua_walpapers (app, wallpapers, configurações e atalhos) e volta ao wallpaper padrão do Windows.\n\nContinuar?",
            "lua_walpapers", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        KillOthers();
        UnregisterMenu();
        RemoveShortcuts();
        try { Directory.Delete(Store.Root, true); } catch { }
        CleanTemp();
        RefreshWallpaper();
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 4 >nul & rd /s /q \"" + InstallDir + "\"");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process.Start(psi);
        }
        catch { }
        MessageBox.Show("lua_walpapers removido e wallpaper resetado.", "lua_walpapers", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    static string FetchVersion()
    {
        Exception last = null;
        foreach (string br in new[] { "main", "master" })
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    HttpWebRequest rq = (HttpWebRequest)WebRequest.Create("https://raw.githubusercontent.com/" + Repo + "/" + br + "/version?t=" + DateTime.UtcNow.Ticks);
                    rq.UserAgent = "lua_walpapers";
                    rq.Timeout = 15000;
                    rq.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                    using (WebResponse rs = rq.GetResponse())
                    using (StreamReader r = new StreamReader(rs.GetResponseStream()))
                        return r.ReadToEnd().Trim();
                }
                catch (Exception ex)
                {
                    last = ex;
                    WebException we = ex as WebException;
                    HttpWebResponse hr = we == null ? null : we.Response as HttpWebResponse;
                    if (hr != null && hr.StatusCode == HttpStatusCode.NotFound) break;
                    Thread.Sleep(1200);
                }
            }
        throw new Exception(last == null ? "sem resposta" : last.Message);
    }

    static bool IsNewer(string remote)
    {
        Version a, b;
        string r = remote.Trim().TrimStart('v', 'V');
        if (Version.TryParse(r, out a) && Version.TryParse(Current, out b)) return a > b;
        return r != Current;
    }

    public static void UpdateCommand()
    {
        string remote;
        try { remote = FetchVersion(); }
        catch (Exception ex)
        {
            MessageBox.Show("Não consegui verificar atualização:\n" + ex.Message, "lua_walpapers", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!IsNewer(remote))
        {
            MessageBox.Show("Você já está na versão mais recente (" + Current + ").", "lua_walpapers", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        RunUpdate(remote);
    }

    static void RunUpdate(string remote)
    {
        Form f = new Form();
        f.Text = "lua_walpapers - atualização";
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.MaximizeBox = false;
        f.MinimizeBox = false;
        f.ControlBox = false;
        f.StartPosition = FormStartPosition.CenterScreen;
        f.ClientSize = new Size(420, 110);
        f.BackColor = Color.FromArgb(11, 11, 22);
        try { f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Label lb = new Label();
        lb.ForeColor = Color.White;
        lb.AutoSize = false;
        lb.SetBounds(16, 14, 388, 40);
        lb.Text = "Atualizando " + Current + " → " + remote.Trim() + "...";
        ProgressBar pb = new ProgressBar();
        pb.SetBounds(16, 62, 388, 24);
        pb.Minimum = 0;
        pb.Maximum = 100;
        f.Controls.Add(lb);
        f.Controls.Add(pb);
        int last = 0;
        Action<int, string> prog = (pct, txt) =>
        {
            try
            {
                f.BeginInvoke(new Action(() =>
                {
                    if (pct > last) last = pct;
                    pb.Value = Math.Max(0, Math.Min(100, last));
                    lb.Text = txt;
                }));
            }
            catch { }
        };
        f.Shown += (s, e) => Task.Run(() =>
        {
            try { DoUpdate(prog); f.BeginInvoke(new Action(() => { Application.Exit(); })); }
            catch (Exception ex)
            {
                Log(ex);
                try
                {
                    f.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("Falha na atualização:\n" + ex.Message, "lua_walpapers", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                    }));
                }
                catch { }
            }
        });
        Application.Run(f);
    }

    static void DoUpdate(Action<int, string> prog)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "lua_walpapers_upd");
        try { Directory.Delete(tmp, true); } catch { }
        Directory.CreateDirectory(tmp);
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        string zip = Path.Combine(tmp, "repo.zip");
        Exception lastEx = null;
        bool got = false;
        foreach (string br in new[] { "main", "master" })
        {
            for (int i = 0; i < 3 && !got; i++)
            {
                try
                {
                    HttpWebRequest rq = (HttpWebRequest)WebRequest.Create("https://github.com/" + Repo + "/archive/refs/heads/" + br + ".zip");
                    rq.UserAgent = "lua_walpapers";
                    rq.Timeout = 30000;
                    rq.ReadWriteTimeout = 30000;
                    using (HttpWebResponse rs = (HttpWebResponse)rq.GetResponse())
                    using (Stream st = rs.GetResponseStream())
                    using (FileStream fs = File.Create(zip))
                    {
                        byte[] buf = new byte[81920];
                        long tot = 0, len = rs.ContentLength;
                        int n;
                        while ((n = st.Read(buf, 0, buf.Length)) > 0)
                        {
                            fs.Write(buf, 0, n);
                            tot += n;
                            int p = len > 0 ? (int)(40.0 * tot / len) : (int)(40.0 * (1 - Math.Exp(-tot / 2000000.0)));
                            prog(Math.Min(p, 40), "Baixando atualização... " + (tot / 1024) + " KB");
                        }
                    }
                    got = true;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    WebException we = ex as WebException;
                    HttpWebResponse hr = we == null ? null : we.Response as HttpWebResponse;
                    if (hr != null && hr.StatusCode == HttpStatusCode.NotFound) break;
                    Thread.Sleep(1500);
                }
            }
            if (got) break;
        }
        if (!got) throw new Exception("Falha ao baixar: " + (lastEx == null ? "" : lastEx.Message));

        prog(42, "Extraindo...");
        string ex1 = Path.Combine(tmp, "x");
        ZipFile.ExtractToDirectory(zip, ex1);
        string src = Directory.GetDirectories(ex1).First();
        prog(46, "Preparando compilação...");

        string bp = Path.Combine(src, "build.ps1");
        File.WriteAllLines(bp, File.ReadAllLines(bp).Where(l => l.IndexOf("Stop-Process", StringComparison.OrdinalIgnoreCase) < 0).ToArray());

        ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + bp + "\"");
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.WorkingDirectory = src;
        Process b = Process.Start(psi);
        int lines = 0;
        DataReceivedEventHandler h = (s, e) =>
        {
            if (e.Data == null) return;
            lines++;
            prog(46 + (int)(44 * (1 - Math.Exp(-lines / 8.0))), "Compilando...");
        };
        b.OutputDataReceived += h;
        b.ErrorDataReceived += h;
        b.BeginOutputReadLine();
        b.BeginErrorReadLine();
        b.WaitForExit();
        string dist = Path.Combine(src, "dist");
        if (b.ExitCode != 0 || !File.Exists(Path.Combine(dist, "lua_walpapers.exe"))) throw new Exception("Falha na compilação.");

        prog(92, "Removendo versão antiga e atalhos...");
        bool hadStartup = File.Exists(App.StartupLink());
        KillOthers();
        RemoveShortcuts();
        RefreshWallpaper();

        prog(96, "Instalando...");
        string script = Path.Combine(tmp, "apply.ps1");
        File.WriteAllText(script, @"param([int]$ProcId,[string]$Src,[string]$Dst,[int]$Startup)
$ErrorActionPreference='SilentlyContinue'
for($i=0;$i -lt 60;$i++){ if(-not (Get-Process -Id $ProcId)){break}; Start-Sleep -Milliseconds 500 }
Get-Process lua_walpapers | Stop-Process -Force
Start-Sleep 1
New-Item -ItemType Directory -Force $Dst | Out-Null
Copy-Item ""$Src\dist\*"" $Dst -Recurse -Force
Copy-Item ""$Src\icon.ico"" $Dst -Force
$sh=New-Object -ComObject WScript.Shell
function L($p,$a){$l=$sh.CreateShortcut($p);$l.TargetPath=""$Dst\lua_walpapers.exe"";$l.Arguments=$a;$l.WorkingDirectory=$Dst;$l.IconLocation=""$Dst\lua_walpapers.exe"";$l.Save()}
L (Join-Path ([Environment]::GetFolderPath('Desktop')) 'lua_walpapers.lnk') ''
if($Startup -eq 1){ L (Join-Path ([Environment]::GetFolderPath('Startup')) 'lua_walpapers.lnk') '--silent' }
Start-Process ""$Dst\lua_walpapers.exe""
", new UTF8Encoding(true));
        ProcessStartInfo ap = new ProcessStartInfo("powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\" -ProcId " + Process.GetCurrentProcess().Id +
            " -Src \"" + src + "\" -Dst \"" + InstallDir + "\" -Startup " + (hadStartup ? 1 : 0));
        ap.UseShellExecute = false;
        ap.CreateNoWindow = true;
        Process.Start(ap);
        prog(100, "Pronto! Reiniciando...");
        Thread.Sleep(900);
    }
}
