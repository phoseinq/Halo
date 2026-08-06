# The local API

Halo can listen on `127.0.0.1` so another program on the same machine can raise a banner, ask the user a
question, read what the pill is showing, or drive it.

It is **off by default**. Turn it on in **Settings → API**.

---

## Security model, stated plainly

Two things, and they are the whole of it:

1. **The listener is bound to `IPAddress.Loopback`, never to `Any`.** Nothing off this machine can reach it,
   with or without a token. There is no remote attack surface to reason about.
2. **A bearer token**, generated the first time the API is switched on and stored in `settings.json` under
   `api.token`. That is protection against *other programs on this machine*, not against the network.

Every capability is a separate switch and every one of them is off unless you turn it on. `control` and
`settings` are the two that stay off even when the rest are enabled — one is a program pressing buttons on
your desktop, the other is a program rewriting how Halo behaves.

## Connecting

| | |
| :-- | :-- |
| Base URL | `http://127.0.0.1:7317` |
| Port setting | `api.port` (default 7317; must be 1024–65535) |
| Token setting | `api.token` |
| Auth header | `Authorization: Bearer <token>` **or** `X-Halo-Token: <token>` |

The token is compared with a length-independent comparison. A bad or missing one is `401`.

```powershell
$t = (Get-Content "$env:LOCALAPPDATA\Halo\settings.json" | ConvertFrom-Json).'api.token'
$h = @{ Authorization = "Bearer $t" }
Invoke-RestMethod http://127.0.0.1:7317/health -Headers $h
```

## `GET /health` — always answers

The one endpoint that replies whatever is switched off, so a caller can tell "Halo is not listening" apart
from "Halo is listening and will not do that for you".

```json
{ "ok": true, "product": "Halo", "capabilities": ["notify", "ask", "state"] }
```

`capabilities` lists only what is currently enabled. Check it before assuming an endpoint will work.

---

## Notifications — `api.notify`

### `POST /notify`

Raises one of Halo's own banners.

| field | required | default | notes |
| :-- | :-- | :-- | :-- |
| `title` | **yes** | | empty title is `400` |
| `app` | no | `"Halo"` | the small name above the title |
| `body` | no | `""` | |
| `seconds` | no | `6` | clamped to 2–30 |
| `code` | no | | a short code shown for copying |
| `launch` | no | | a path or URI opened when the banner is clicked |

```bash
curl -s -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
     -d '{"title":"Build finished","body":"14 tests, all green","seconds":8}' \
     http://127.0.0.1:7317/notify
```

## Questions — `api.ask`

### `POST /ask`

Puts a question on the pill and returns immediately with a nonce. At least one option is required.

`options` accepts either plain strings or objects with `label` and `description`.

```json
{
  "question": "Deploy to production?",
  "options": [
    { "label": "Deploy", "description": "Runs the release pipeline" },
    { "label": "Not now" }
  ]
}
```

### `GET /ask/{nonce}`

Poll for the answer. This is a real question put to a person, so poll rather than expecting it to be there.

Note on how this works, because it explains the shape: the question is written into the same directory the
pill's `AskStore` already watches, in the format it already parses, and the answer comes back as
`answer-{nonce}.json`. The tool name is Halo's own rather than an agent's, deliberately — an agent's
question is answered by typing a number into its terminal, which an HTTP caller has not got.

## Reading state — `api.state`

All read-only.

| endpoint | returns |
| :-- | :-- |
| `GET /state` | what the pill is showing right now |
| `GET /media` | the media sessions Halo can see |
| `GET /agents` | live agent sessions: kind, state, tool, target, cwd, pid |
| `GET /tray` | the file tray's current paths |

Percentages that are not known come back as `null`, never as a number. Halo does not invent figures, and a
caller reading `-100%` because "not known yet" was multiplied by 100 was the reason that rule is written
down.

## Driving the pill — `api.control` (off by default)

### `POST /media`

`{ "action": "...", "slot": -1 }` — `slot` defaults to the primary session.

Actions: `play`, `pause`, `toggle`, `next`, `previous`.

### `POST /pill`

`{ "action": "..." }` — one of `expand`, `collapse`, `pin`, `unpin`, `recenter`.

`expand` holds the pill open rather than faking a hover: hover is recomputed from the real cursor every
frame, so a written hover would be overwritten within 8ms.

### `POST /tray`

`{ "paths": ["C:\\...\\a.png", "C:\\...\\b.pdf"] }`, or `{ "path": "..." }` for one.

Returns `{ "added": n, "skipped": m }`. A path that does not exist is **skipped and counted**, not silently
dropped — a caller that got `200` with nothing added would otherwise have no way to find out.

## Settings — `api.settings` (off by default)

`GET /settings` returns the current values. `PATCH /settings` writes them.

This one rewrites how Halo behaves, which is why it is a separate switch from `control`.

---

## Status codes

| code | meaning |
| :-- | :-- |
| `200` | done |
| `400` | the request is missing something, or names an action that does not exist |
| `401` | bad or missing token |
| `403` | that capability is switched off in Halo's settings |
| `404` | no such endpoint |

A `403` is a settings problem, not a bug — check `GET /health` to see what is enabled.

## Where this lives in the source

`src/Halo.App/Api/HaloApi.cs` is the listener, the routing and the token check.
`src/Halo.App/Shell/NotchController.Api.cs` is everything it asks the pill to do, and it is where the
threading rule lives: an API call arrives off the UI thread and is posted onto it, because the pill's state
may only be touched there.
