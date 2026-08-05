# Privacy

**Halo 3.8.1** · Last updated 5 August 2026

Halo runs entirely on your machine. There is no Halo account, no analytics and no telemetry. Nothing
that appears in the pill — no notification, no track title, no file name, no coding-session text — is
uploaded anywhere, ever.

There is exactly one way data can leave your machine: a **bug report** that you read in full and press
send on yourself. It is described line by line below, including every field it can contain and every
field it never contains.

Halo does have a place to send those reports to: an intake run by the author at `halo.pvboy.dev`. That
is new in 3.8.0 and it is a change from earlier versions, which had a blank box for an address of your
own and no default. **It changes nothing about when a report is sent** — that is still your press, and
only your press. There is one switch, off by default, that lets a *crash* report send itself; if you
never turn it on, nothing is ever sent without you pressing something.

This page exists so you can check those claims rather than take them. It lists every kind of data Halo
reads, everything it writes to disk, every network request it is capable of making, and what a report
holds.

**The short version**

- Halo collects nothing for its own purposes. It has one server, it receives only bug reports, and it
  receives them only when you send one.
- Nothing is transmitted without you pressing something, unless you switch on **Send crashes without
  asking** — which is off until you turn it on, and covers crash reports only.
- There is no retry queue and no scheduled task that sends anything.
- Uninstalling removes everything Halo stored, and puts back the one Windows setting it changes.

---

## What Halo reads from your machine

All of it stays on your machine.

| What | What it is for | Where it comes from |
| :-- | :-- | :-- |
| Track title, artist, artwork, position | the media panel | Windows' own media session (the same one the volume flyout uses) |
| Notification title, body, app icon | mirroring toasts into the pill | Windows' `UserNotificationListener` |
| A toast's launch arguments | so clicking a banner opens the exact message | Windows' own notification database (`wpndatabase.db`) |
| A verification code inside a notification | the one-click **Copy** button | matched in memory from the notification's text. It reaches your clipboard only when you press the button |
| Download name, size and progress | the download panel | your browser's own local download database |
| Bluetooth device battery level | the battery panel | Windows Bluetooth APIs |
| Coding-session state | the Claude Code / Codex panels | JSON files those tools' own hooks write under `~/.claude/notch`, `~/.codex/notch` and `~/.halo/agents` |
| Paths of files you drag onto the pill | the file tray | you dragged them there. Only the paths are kept, never the contents |
| Which window is in front | so the pill can follow the app you're using | Windows' foreground-window API. Only the process id is used |
| Your device's location | the weather on the hourly banner | Windows' own location service — **only if location is switched on and Halo is allowed to use it**. If it is off, or Halo is denied, Halo never asks again and falls back to your timezone's city |

---

## What Halo writes to disk

Almost everything lives in `%LOCALAPPDATA%\Halo\`. Deleting that folder resets Halo completely. There
is one exception, and it is the section directly below this list — Halo edits your coding agent's own
configuration file so its panels can work at all.

- `offset`, `pinned`, `scale`, `capturable` — where you put the pill and how you like it
- `settings.json` — every switch in the settings window
- `tray.txt` — the paths currently in the file tray
- `notif-seen.txt` — the id of the last notification shown, so restarts don't replay your Action Center
- `banner-orig.tsv` — **each app's original Windows banner setting before Halo changed it** (see below)
- `limit-fired.txt`, `usage-cache.json`, `codex-limits-cache.json` — which alerts have fired, and the last usage numbers
- `downloaders.tsv` — download bookkeeping
- `fps`, `shape` — two small files the pill writes so the settings window, which is a separate program,
  can say what the pill measured and which panel was in front. `shape` holds class names and true/false —
  `MediaWidget`, `expanded=0` — never what a panel was showing
- `reports\` — bug reports you or a crash have produced, waiting for you to decide what to do with them,
  each with a `.sent` file beside it if that one was actually sent
- `crash-sent` — a short hash of the last few crashes that were sent, so the same fault is not reported
  again on every relaunch. It holds hashes and timestamps, never the text it was made from
- `hooks-connect.txt` — one line per agent recording that Halo connected it, or that you disconnected it.
  The second is what stops Halo ever offering again
- `*-debug.txt` — local diagnostics, off unless you ask for them. `hooks-debug.txt` only appears while a
  file named `hooks-debug.on` sits beside it, and records which agent was seen and which check stopped a
  connection — never anything from your prompts or your code

The diagnostics are worth being specific about, because they concern your notifications:
`notif-debug.txt` records **which app** sent a notification and **how many characters** its title and
body had — never the text. A line looks like this, and that is the whole of it:

```
15:37:26 toast 67750: aumid='Logi.GHUB.Systray' app='Logitech G HUB' t=14 b=22
```

None of these files are ever transmitted anywhere on their own.

### The one file Halo changes that is not its own

For the Claude Code and Codex panels to show anything, those tools have to tell Halo when something
happens. They do that through their own hook settings, so Halo writes nine handler lines into
**`~/.claude/settings.json`**, and the equivalent into Codex's config, the first time it sees that agent
running on your machine.

This is the only thing Halo writes outside its own folder, and it is the one place it changes another
program's configuration. So, precisely:

- **Your previous file is copied to `settings.json.halo-bak` first**, every time, before anything is
  written. If a policy or a sync tool had marked that file read-only, Halo clears the flag long enough to
  write and then **puts it back**
- **A notification tells you it happened**, naming the file and the backup. It is not silent
- **Only Halo's own handlers are touched.** Hooks you or another tool put there are left exactly as they
  were, including the other agent's
- **"Disconnect" in the settings window removes them**, and that answer is permanent — Halo will not put
  them back on the next launch, or ever, unless you ask
- Halo reads **nothing** out of that file except whether its own handlers are present

What the handlers then send Halo is described under "What Halo reads from your machine": which tool is
running, what state it is in, and how long it has been at it. Never your prompts, your code, or the
model's replies.

---

## Bug reports and crash reports

When Halo crashes it writes a report to `%LOCALAPPDATA%\Halo\reports\` and stops there. It does not
send it, then or on the next launch. The next time you open the settings window it shows you that
report, says in as many words that it has **not** been sent, and leaves the decision to you.

That is the default and it is what you get unless you change it. Settings → Docs & About → Problems has one
switch, **Send crashes without asking**, which is **off**. Turning it on means a crash may post itself
on its way out — the same report, the same fields, the same address, without the trip through the
window. Nothing else is affected by that switch: a report you write yourself still needs your press,
and no other data is sent under any setting.

You can also write a report yourself from the settings window at any time.

### What a report contains — the complete list

Halo mirrors **other people's** notifications, the title of whatever is playing, the names of files you
drop in the tray, and live coding-session text. So a report is not a scrubbed dump of Halo's state; it is
an **allowlist**. A field is in a report because it was named below, and anything not on this list is
absent by construction:

| Field | Why it is safe to include |
| :-- | :-- |
| Halo version | ours |
| Windows build, system DPI, screen resolution and refresh rate | the shape of the machine, not anything on it |
| .NET runtime version | ours |
| Exception type, message and stack trace | see the note on paths below |
| Which panel was in front, and which surfaces were live, as class names and true/false | shape, never titles |
| Whether the pill was expanded, whether it was in its reduced-rate mode, the frame-rate setting | shape |
| **The description you type** | you wrote it, and you are looking at it |

**Never in a report:** notification titles, bodies or app names; media title, artist or album; file names
or paths from the tray; coding-session prompts, transcripts or tool arguments; window titles; your local
API token; your settings file; the contents of any file; your IP address; any identifier for you or your
machine.

**Paths are reduced to file names.** `C:\Users\<your name>\OneDrive\...\MediaWidget.cs` names your account
and your folder layout. `MediaWidget.cs:line 148` debugs exactly as well and names neither, so that is
what a report carries — in the stack trace and in the exception message, both. Your Windows account name
is removed from that text wherever else it appears.

### How one is sent, if you send it

A report is written as indented JSON, meant to be read. Before anything happens, the settings window
shows you **the file itself**, with an "open in Notepad" escape hatch. **The bytes you are shown are the
bytes that are sent** — nothing is assembled a second time at send time, because then the preview would
be a claim about the report rather than the report.

Then it is your press, and only your press:

- **Copy report**, **Save as file…** and **Open a GitHub issue** (which opens your browser with the text
  filled in). These involve no server at all.
- **Send report** — an HTTPS POST to `halo.pvboy.dev:2053`, the intake the author runs. It receives the
  bytes you were just shown and nothing else.

If the send fails, Halo tells you so and leaves the report on disk. There is no silent retry, because a
retry queue is a background upload wearing a different hat.

**About the key.** Requests carry a token that is compiled into Halo, and it is not a secret: the source
is public, so the token is printed in it, and anyone who downloads the installer could read it out
anyway. It identifies the build, not you — every copy of Halo carries the same one, so it cannot tell
one user from another. It is sent as an `Authorization` header, never inside the report body, so a
report you forward to somebody else does not carry it. What stops the address being abused is the
intake's own limits, not the token's secrecy.

**What the intake stores.** The report as shown, the time it arrived, and the source IP address — which
any HTTPS request discloses, and which is what per-address rate limiting is done against. Reports are
kept on that server, for reading and fixing bugs, and for nothing else: they are not analysed in
aggregate, not shared, not sold, and not joined to anything. There is no account, no cookie and no
identifier that would let two reports be recognised as coming from the same machine.

### How long reports are kept

The newest ten, up to 2 MB in total — whichever limit is reached first — and then the oldest are deleted.
You can delete any of them at any time; they are ordinary files in a folder you own. **Uninstalling Halo
deletes the folder.**

---

## The one registry change Halo makes

When Halo mirrors a toast, it silences Windows' own banner for that app so you aren't told the same
thing twice. It does that by setting `ShowBanner` to `0` for that app under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings`.

It writes down each app's **original** value first, and the change is fully reversible:

```
Halo.App.exe --restore-notifications
```

The uninstaller runs that for you, so removing Halo puts every app's notifications back the way it
found them.

---

## The local API, which is off until you switch it on

Halo can let other programs on your machine drive the pill. It is **off by default**. When you turn it
on, it listens on `127.0.0.1` only — nothing outside your machine can reach it, by binding rather than by
policy — and every request needs a token that is generated on your machine and never leaves it. What a
caller is allowed to do is a set of switches you control: posting a notification and asking a question
are separate permissions from reading what is on screen or pressing buttons, and the last two are off
until you say otherwise.

---

## Every network request Halo can make

Halo makes no request that is not on this list.

| Endpoint | When | What it discloses |
| :-- | :-- | :-- |
| `www.google.com/generate_204` | connectivity check, while a coding-session panel is live | nothing. This is the standard connectivity-check endpoint, chosen because it returns an empty response |
| `api.anthropic.com` — `/api/oauth/usage` and `/v1/messages` | your Claude Code usage limits and whether the API is reachable | **your own** Claude credentials, read from `~/.claude/.credentials.json` — the same token Claude Code itself uses, sent only to Anthropic |
| `chatgpt.com/backend-api/codex/responses` | whether Codex is reachable | a reachability probe |
| `ipwho.is` | to show which country your connection is leaving from | **your public IP address**, unavoidably. This is a third party |
| `api.ipapi.is` | only while you hover the exit block, to say whether that address looks like a datacenter, a known vpn, or a flagged one | **your public IP address**. This is a third party. Sent once per address and then cached, so hovering repeatedly costs no further requests |
| `bash.ws` — `/id`, six lookups of `<n>.<id>.bash.ws`, and `/dnsleak/test/<id>` | only while you hover the exit block, to test whether your DNS lookups leave by the same exit as your traffic | **which resolvers answer for you**, and your public IP. This is a third party, and it is a wider disclosure than the two above: the whole mechanism is that their nameserver watches which resolver comes asking. Once per address, then cached |
| `flagcdn.com` | the flag image for that country | the two-letter country code |
| `geocoding-api.open-meteo.com` and `api.open-meteo.com` | the weather on the hourly banner, refreshed every half hour | **coordinates.** If Windows location is on and Halo is allowed, those are **your device's own coordinates**, to about 11 m. Otherwise they are the coordinates of the city from your timezone — "Asia/Tehran" becomes "Tehran" — which is a whole city wide. The city name is also sent once, to look it up |
| `displaycatalog.mp.microsoft.com` | the name and art of a Microsoft Store install in progress | the Store product id |
| `halo.pvboy.dev:2053` — the author's bug-report intake | when you press send on a report you have read, and — only if you switch it on — when Halo crashes | the report shown above, and nothing else. Your IP address, as any HTTPS request does. **You can redirect this or switch it off entirely** — see below |
| `127.0.0.1` | VLC playback controls, and Halo's own local API when you enable it | nothing — it never leaves your machine |

**Redirecting or disabling reports.** Two keys in `%LOCALAPPDATA%\Halo\settings.json` decide where a
report goes. Set `report.endpoint` to your own https address and reports go there instead, with
`report.key` as the credential — Halo's own token is never sent to an address that is not Halo's.
**Set `report.endpoint` to an empty string and reports never touch the network at all**, including the
automatic crash path; they are still written to disk for you to read. Leave both alone and the built-in
intake above is used, and only when you press send.

**`ipwho.is`, `api.ipapi.is` and `bash.ws` are the only requests that tell a third party anything about
you.** All three disclose your public IP the same way opening any web page does. `ipwho.is` runs only
while a coding-session panel is open, and at most once every five minutes. The other two run only when
you actually **hover the exit block**, once per address, cached until the address changes — they answer a
question you asked by pointing at it, so each costs exactly one lookup.

`api.ipapi.is` is asked over HTTPS deliberately: other providers serve the same flags over plaintext
HTTP, and asking "is my exit private" over a channel the local network can read and rewrite is the wrong
trade.

**`bash.ws` deserves its own paragraph**, because a DNS leak test cannot be done quietly. There is no way
to see which resolver actually answers for you from inside your own machine — the only way is to look up
names under a domain whose nameserver is watching, and read back which resolvers came asking. So the test
necessarily tells `bash.ws` who resolves your names. That is the entire point of it, and it is why it
never runs on its own: no hover, no test.

**Open-Meteo** needs no key and is sent nothing that identifies you — no name, no id, no account. What
it is sent is a point to fetch the weather for, and that point is as precise as you have allowed:

- **Location switched on and Halo allowed** — your device's own coordinates, at roughly 11 m. Halo asks
  Windows for a fix at most every ten minutes, and only on the half-hourly weather refresh.
- **Location off, or Halo denied** — the city from the timezone Windows is already set to, which is the
  coarsest location fact on the machine and one you chose yourself. Halo reads the system switch before
  asking, so a denied app never triggers a prompt and never asks again in that session.

**You control this in Windows, not in Halo**: Settings → Privacy & security → Location. Turn it off and
the banner keeps working, one city wide instead of one street. Halo never uses the exit-IP lookup above
for the weather.

If any of these trades isn't worth it to you, say so in an issue — all of them are good candidates for a
switch.

---

## What Halo never does

- No account and no sign-in — there is nothing to sign in to.
- No analytics, telemetry or usage statistics, in any build. The intake receives bug reports and
  nothing else — there is no event, no ping and no "app started" anywhere in Halo.
- **No automatic crash reporting unless you ask for it.** A crash is written to a file on your machine
  and goes nowhere else until you read it and press send. One switch, off by default, changes that for
  crash reports only.
- Notification text, media titles, file names, download names and clipboard contents **never leave
  your machine**.
- **No update checks and no background downloads.** Halo does not phone home for new versions; it has
  no updater at all. You update it the way you installed it.
- Nothing is sold, rented or shared for advertising. There is no advertising in Halo.
- Nothing is sent to the author or to `pvboy.dev` **except a bug report you sent**. There is no other
  traffic to that host, at any time, under any setting.

---

## Your choices

| If you want to | Do this |
| :-- | :-- |
| Stop Halo reading your notifications | turn off **Mirror notifications** in settings, or revoke notification access in Windows Settings → Privacy & security → Notifications |
| Keep your location out of it | Windows Settings → Privacy & security → Location. The weather falls back to your timezone's city |
| Stop the exit-IP and DNS lookups | don't hover the exit block; `ipwho.is` stops with the coding-session panels |
| Stop other programs driving the pill | the local API is off by default; leave it off, or turn it off |
| Send no reports | send none. Nothing is sent unless you press send. Leave **Send crashes without asking** off — it ships off — and that stays true of crashes too |
| Have a crash reported without being asked each time | Settings → Docs & About → Problems → **Send crashes without asking** |
| Have a report deleted after you sent it | [open an issue](https://github.com/phoseinq/Halo/issues) or ask, quoting roughly when you sent it. There is no account to look you up by, so the time is what finds it |
| Delete everything Halo knows | delete `%LOCALAPPDATA%\Halo\`, or uninstall |

## Keeping data, and getting rid of it

Everything Halo stores is a file on your own disk, in one folder, and you can delete any of it at any
time. Bug reports are capped at ten files or 2 MB; the rest is small bookkeeping that is overwritten as
it changes.

**Uninstalling** deletes `%LOCALAPPDATA%\Halo\`, including any reports you never sent, and restores the
Windows notification setting described above for every app Halo changed.

**A report you actually sent** also exists on the intake, and deleting your local copy does not remove
that one. It holds no name, account or device identifier, so there is nothing to look you up by — ask
and quote roughly when you sent it, and it will be deleted.

## Children

Halo is a desktop utility for general audiences. It is not directed at children, and it does not
knowingly collect anything from anyone — there is no collection to speak of.

## Security

What protects your data here is mostly that it never moves: it lives in your own user profile, under the
permissions Windows already gives it. Beyond that — the local API binds to loopback and requires a token
generated on your machine; report uploads go over HTTPS and nothing is ever sent over plain HTTP.

The one credential Halo ships with is the intake token described above, and it is deliberately not a
secret: it identifies the build, is identical in every copy, and is printed in the public source. The
intake is protected by rate limits per source address rather than by that token being hidden, and it can
be revoked without a restart if it is abused.

## Changes to this statement

The date at the top is when this was last changed. Because the source is public, so is the history of
this file — every change to it is in the repository's commit log, next to the code change that caused
it. Material changes are called out in the release notes.

## Who to ask

Halo is written by [@phoseinq](https://github.com/phoseinq). Questions, corrections and complaints about
anything on this page: [open an issue](https://github.com/phoseinq/Halo/issues).

---

## Checking any of this yourself

The source is here and builds from it. Every outbound request in the table above is one `grep` away:

```
grep -rn "https\?://" --include="*.cs" src/
```

And the entire contents of a bug report is one file — the list of fields above is the code:

```
src/Halo.App/Reports/ReportPayload.cs
```

Something missing or wrong? [Open an issue](https://github.com/phoseinq/Halo/issues).
