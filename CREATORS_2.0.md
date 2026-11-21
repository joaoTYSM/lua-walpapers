# Documentation of Creators 2.0

Everything you need to build wallpapers for lua_walpapers.

## 1. Wallpaper types

| Folder contents | Result |
| --- | --- |
| `index.html` (+ assets) | Web wallpaper |
| `index.html` + `main.lua` | Web wallpaper with a Lua script |
| only `*.lua` files | Lua wallpaper (built-in canvas page) |
| image or video | Media wallpaper |
| URL added from the panel | Remote page |

Optional files: `info.json`, `properties.json`, a preview image (`preview.png`, `preview.jpg`, `preview.gif`, `thumbnail.png`, `thumbnail.jpg`).
Import by `.zip`, folder, files or URL (GitHub repo links work). Compiled Lua (`.luac`) and obfuscated Lua are rejected.

`info.json`

```json
{ "Title": "My Wallpaper", "Author": "you", "Thumbnail": "preview.png" }
```

## 2. Settings (`properties.json`)

Each key becomes a control in **Customize**. Types: `checkbox`, `slider` (`min`, `max`, `step`), `textbox`, `dropdown` (`items`, value is the index), `color`, `label`, `button`.

```json
{
  "speed": { "type": "slider", "value": 1, "min": 0.1, "max": 5, "step": 0.1, "text": "Speed" },
  "quality": { "type": "dropdown", "value": 1, "items": ["Low", "Medium", "High"], "text": "Quality" }
}
```

Values are delivered to the page with `wallpaperProperty(name, value)`, to Lua with `on_property(name, value)`, and are available at startup as `window.luaProps` (JS) or `settings` (Lua).

## 3. JavaScript host API

Define any of these globals in your page:

| Function / event | When |
| --- | --- |
| `wallpaperProperty(name, value)` | a setting changed or loaded |
| `hostAudio(bins)` | system audio, 128 values in 0..1, about 47 times per second |
| `hostMouse(x, y, type, buttons)` | desktop mouse, `x`/`y` normalized 0..1 from the top-left, `type` is `move`, `down` or `up`, `buttons` is a bitmask (1 left, 2 right, 4 middle) |
| `wallpaperPlaybackChanged('{"IsPaused":true}')` | paused while a window covers the desktop |
| `luaMsg(data)` and the `lua-msg` event | message sent from Lua with `send()` |
| `host-mouse` event | same data as `hostMouse`, in `event.detail` |

Mouse events are only forwarded while the cursor is over the desktop (or a drag that started there). Desktop icons keep working.

Send text to Lua from the page:

```js
chrome.webview.postMessage('hello');
```

## 4. Lua API

Lua 5.2 syntax (MoonSharp). Sandbox: `string`, `table`, `math`, `bit32`, `coroutine`, `os.time/clock`, `pcall/error`. No `io`, no `os.execute`, no `require`, no file access.

### Globals

| Name | Description |
| --- | --- |
| `screen.width`, `screen.height` | primary screen size in pixels |
| `settings` | table with saved/default values from `properties.json` |
| `time()` | seconds since the script started |
| `set_fps(n)` | tick rate, 1..120 (default 30) |
| `sleep(ms)` | wait while still dispatching events |
| `send(value)` | send a string or table to the page (`luaMsg` / `lua-msg`) |
| `js(code)` | run JavaScript in the wallpaper page |
| `print(...)` | shown in the built-in canvas page log |

### Callbacks (all optional)

```lua
function on_tick(dt) end                    -- every frame, dt in seconds
function on_mouse(x, y, ev, buttons) end    -- pixels; ev = "move" | "down" | "up"
function on_audio(bins) end                 -- bins[1..128], 0..1
function on_property(name, value) end
function on_message(text) end               -- from chrome.webview.postMessage
```

`buttons % 2 == 1` means the left button is held.

### Drawing with `gfx` (Lua wallpapers without HTML)

Coordinates are screen pixels. Colors are any CSS color string. Commands are batched and drawn once per tick.

```lua
gfx.clear(color)
gfx.fade(color, alpha)                       -- translucent overlay, creates trails
gfx.rect(x, y, w, h, color, fill)            -- fill defaults to true
gfx.circle(x, y, r, color, fill)
gfx.line(x1, y1, x2, y2, color, width)
gfx.text(str, x, y, color, size)
```

### Example: drag trail

```lua
local dots = {}

function on_mouse(x, y, ev, buttons)
  if buttons % 2 == 1 then
    dots[#dots + 1] = { x = x, y = y, life = 1 }
  end
end

function on_tick(dt)
  gfx.fade("#05060d", 0.15)
  local alive = {}
  for i = 1, #dots do
    local d = dots[i]
    d.life = d.life - dt
    if d.life > 0 then
      gfx.circle(d.x, d.y, 4 + d.life * 10, "rgba(120,200,255," .. d.life .. ")")
      alive[#alive + 1] = d
    end
  end
  dots = alive
end
```

### Driving your own HTML from Lua

```lua
function on_tick(dt)
  send({ t = time() })
  js("document.title = 'tick'")
end
```

```js
window.luaMsg = d => { /* d is the table as an object */ };
```

Errors in your script appear in the canvas page log (`Lua error: ...`) or as `{ lua_error: "..." }` in `luaMsg`.

## 5. Creating Lua wallpapers

- Tray icon → right-click → **Lua wallpaper...**
- Panel → **+ Lua**
- Panel → **Code** on an existing Lua wallpaper

Or drop a folder containing `main.lua` (and optional `info.json`, `properties.json`) into `defaults/` before building.

## 6. Tips

- Keep `on_tick` light: lower `set_fps` and cap particle counts.
- The wallpaper pauses automatically when a maximized or fullscreen window covers the desktop.
- Use `skeleton`-style placeholders in any UI you add; the panel never shows spinners while loading.
