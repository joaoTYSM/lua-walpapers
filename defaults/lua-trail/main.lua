local w, h = screen.width, screen.height
local fade = settings.fade or 0.16
local hueSpeed = settings.hueSpeed or 3
local particles = {}
local orbs = {}
local pressed = false
local lastx, lasty = 0, 0
local hue = 200
local pulse = 0

set_fps(40)

for i = 1, 36 do
  orbs[i] = {
    x = math.random() * w,
    y = math.random() * h,
    vx = (math.random() - 0.5) * 24,
    vy = (math.random() - 0.5) * 24,
    r = 1.5 + math.random() * 3.5,
    h = math.random() * 360
  }
end

local function color(hh, s, l, a)
  a = math.max(0, math.floor(a * 100) / 100)
  return "hsla(" .. math.floor(hh % 360) .. "," .. s .. "%," .. l .. "%," .. a .. ")"
end

function on_property(name, value)
  if name == "fade" then fade = value end
  if name == "hueSpeed" then hueSpeed = value end
end

function on_audio(bins)
  local sum = 0
  for i = 1, 12 do sum = sum + bins[i] end
  pulse = math.max(pulse * 0.9, sum / 12 * 6)
end

function on_mouse(x, y, ev, buttons)
  local down = buttons % 2 == 1
  if down and pressed and #particles < 400 then
    local dx, dy = x - lastx, y - lasty
    local steps = math.max(1, math.floor(math.sqrt(dx * dx + dy * dy) / 8))
    for i = 1, steps do
      local t = i / steps
      particles[#particles + 1] = {
        x = lastx + dx * t,
        y = lasty + dy * t,
        vx = (math.random() - 0.5) * 40,
        vy = (math.random() - 0.5) * 40,
        life = 1,
        size = 5 + math.random() * 8,
        h = hue
      }
      hue = hue + hueSpeed
    end
  end
  pressed = down
  lastx, lasty = x, y
end

function on_tick(dt)
  gfx.fade("#05060d", fade)
  pulse = pulse * 0.95
  for i = 1, #orbs do
    local o = orbs[i]
    o.x = (o.x + o.vx * dt) % w
    o.y = (o.y + o.vy * dt) % h
    gfx.circle(o.x, o.y, o.r + pulse * 2, color(o.h, 60, 70, 0.35 + pulse * 0.3))
  end
  local alive = {}
  for i = 1, #particles do
    local p = particles[i]
    p.life = p.life - dt * 0.7
    if p.life > 0 then
      p.x = p.x + p.vx * dt
      p.y = p.y + p.vy * dt
      gfx.circle(p.x, p.y, p.size * p.life + 1, color(p.h, 85, 62, p.life * 0.8))
      alive[#alive + 1] = p
    end
  end
  particles = alive
end
