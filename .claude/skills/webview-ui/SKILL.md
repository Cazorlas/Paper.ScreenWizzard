---
name: webview-ui
description: Build and prove a screen whose content is HTML inside a WebView2 control hosted by a desktop app or an add-in - the environment and its user-data folder, the bridge between page and host, what a host process that already ships its own WebView2 does to assembly binding, and how to verify a page that FlaUI can see the control of but not the DOM inside. Use when a task puts a chart, a diagram, a large table or any web-rendered view inside a WPF window, dialog or dockable pane, or when deciding whether a screen belongs in a WebView2 at all.
---

# A screen that is a web page inside a desktop host

WebView2 is not a second UI framework to spread work across. It earns a screen only when the web already
does that screen better than the app could - a chart, a layered diagram, a big virtualised table, a
preview. Everything else stays where it is.

## Is this screen actually a WebView2 screen

| The screen | Where it belongs |
|---|---|
| touches host elements **live** - select, highlight, drag a grip in step with the model | **the native canvas**: same process, same thread, no bridge in between |
| is mostly **presenting and collecting** - chart, diagram, table, preview, wizard | **WebView2**, when a mature web library saves rebuilding it natively |
| is a form with a handful of fields | neither. A normal window is less work than either |

**A bridge you add is a bridge you debug forever.** Two serialisation hops per interaction is fine for a
chart that redraws on demand and wrong for something that follows the pointer.

## The host process was there first

This is the part that bites, and it bites only at run time.

- **A host that ships its own WebView2 assemblies has already loaded them** by the time an add-in runs,
  and what happens next depends on the **runtime**, not on the host's version number. Measured in one
  desktop host across two runtimes, and the two behave oppositely:

  | Runtime | How it binds | What the add-in gets |
  |---|---|---|
  | **.NET Framework** | strong name, **exact version**, probes the add-in's own folder | **its own newer copy loads** |
  | **.NET Core** | **simple name**; the copy the host already loaded wins | **silently the host's older one** |

  The second is the dangerous one: asking for an exact newer version returns the older assembly with **no
  exception and no warning**, and the first sign is a `MissingMethodException` in the middle of the user's
  work. A compile proves nothing here, and neither does a successful load.

  **So the API ceiling is the version the OLDEST .NET Core host ships**, and a start-up check should read
  the `AssemblyVersion` actually loaded and say so, rather than waiting for the failure.
- **The runtime is Evergreen and shared machine-wide.** Nothing is bundled and nothing is pinned; the
  page runs on whatever Edge runtime the machine has. A page that depends on a very new web feature is a
  page that works here and not on a colleague's machine.
- **Never ship a second Chromium into a host that already embeds one.** Native libraries and a browser
  subprocess cannot be side-by-side versioned inside one process, and no binding redirect reaches them.
- **A host that embeds Chromium AND lets you load a newer WebView2 is the configuration that crashes.**
  The vendor of one such host documents its own dialog crashing exactly when a third-party add-in brings
  a recent WebView2 alongside the host's embedded Chromium. If the host is in that shape, every screen
  you add must be tried **together with the host's own browser-backed windows**, not only on its own.
- The environment needs an explicit, **stable user-data folder** under the app's own data directory. The
  default puts it next to the executable, which in a host add-in is a directory you may not write to.

## Getting the control up

1. Create the environment with that user-data folder, then `EnsureCoreWebView2Async`. **Both are async and
   both must finish before any navigation** - setting `Source` first is the usual reason a blank control
   appears with no error at all.
2. Serve local content as files next to the assembly, resolved from the assembly's own location. A relative
   path resolves against the process directory, which is the host's, not yours.
3. Put the bridge behind one named object with a small surface. Host to page and page to host both cross a
   serialisation boundary; keep the messages coarse.
4. **Dispose the control with the window.** A WebView2 left alive holds its browser process, and a host
   that opens the window twice then has two.

## Proving it, because the usual tools cannot see inside

`drive-wpf` drives the window; it sees the **control**, never the DOM. A UI test that finds the WebView2
element and stops there has proved the control exists and nothing about the page.

Three ways in, in the order worth trying:

| | How | Proves |
|---|---|---|
| **Ask the page** | `ExecuteScriptAsync` returning JSON, from the test | the DOM is what the plan said |
| **Look at it** | screenshot the window, open the PNG and judge it | it rendered, in both themes |
| **Drive the bridge** | call the host-side handler the page calls | the contract, without a browser |

**A pass that only checked the control exists is not a pass** - say so in the evidence row rather than
rounding it up. Every WebView2 task ends with at least one screenshot **opened and judged**, and at least
one assertion about the page's own content.

## Before you call it done

1. It ran **inside the real host process**, not only in a standalone test window. Assembly binding and the
   user-data folder are both host-specific, and both fail only there.
2. The page's content was asserted, not just the control's presence.
3. A screenshot per theme was opened and judged.
4. Anything the bridge could not reach is named as not verified.
