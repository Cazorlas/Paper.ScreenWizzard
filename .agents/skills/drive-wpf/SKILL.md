---
name: drive-wpf
description: Drive a WPF window yourself until the task is done - build the view inside the test process on an STA thread with fakes for the platform, attach FlaUI to it by handle, click and type as a user would, and look at the screenshots rather than trusting a count. Use when a task creates or changes a window, view, ViewModel, XAML binding or style, when a UI bug needs reproducing, or when a UI test is flaky and nobody knows why.
---

# Driving a WPF window until it is right

Nobody is going to click it for you. You build it, you use it as a user would, and you look at what came
out. A test that only asks the ViewModel questions has not seen the window.

**Two layers, both in the same test process, both in every UI task:**

| Layer | How it runs | What it catches | Pace |
|---|---|---|---|
| **In process** | the view is built in the test, the platform behind it is a fake | binding, converters, what is drawn, pixels | seconds |
| **Real input** | the same window on its own STA thread, FlaUI clicking and typing from the test thread | focus, modal, tab order, what only breaks when a human touches it | tens of seconds |

The fast layer is for iterating; the input layer is for believing. **A pass that only ran against fakes is
not a verified pass** - say so in the evidence row rather than rounding it up.

## Build the window inside the test, not in another process

Launching a UI host executable and driving it from outside looks equivalent and is not. Measured
2026-09-19 on a Paper project: four tests failed with a bare `NullReferenceException` because the
cross-process UIA tree went stale mid-test - after a row click the icon buttons were **on the screen and
not in the tree**. Closing the host application, running one test alone, and reverting the day's edits all
left it red. A screenshot is what settled it: the window was correct, the way the test reached it was not.

In-process, the same suite went from four failures every run to **six consecutive runs green**.

What that host needs:

- **Its own STA thread**, one `Application` per process, and `Dispatcher.Run()` on that thread. Create the
  window there, show it, and keep the dispatcher alive until disposal.
- **FlaUI attached by handle** - `automation.FromHandle(new WindowInteropHelper(window).Handle)` - not by
  launching or by searching the desktop.
- **A settle that asks WPF, not the clock.** `Wait.UntilInputIsProcessed()` then
  `dispatcher.Invoke(() => {}, DispatcherPriority.ApplicationIdle)`. A `Thread.Sleep` is a guess that is
  too long on a fast machine and too short on a busy one.
- **Disposal that closes the window and joins the thread.** A window left open is a window the next test
  finds.
- **A faster pointer.** FlaUI's default 0.5 px/ms spends seconds crossing a window; 20 px/ms with 10 px
  steps measured 186 ms for the same trip and changes nothing else.

One shared case catalogue, read by the gallery, the screenshot shooter and the tests. Two registries drift.

## Find things the way they are actually built

- **Wait for the element, then assert on it by name.** `Find(id)!.Click()` turns a missing control into a
  `NullReferenceException` with a line number and no subject. Waiting and asserting turns it into
  "'MoveUpButton' never appeared in the window", which is a question someone can answer.
- **A menu or a tooltip is a popup in its own window**, not a descendant of yours. Look inside your window
  first, then the other top-level windows **of your own process** - in-process, the process owns the test
  runner's windows and any case not yet disposed. Never search the whole desktop: one busy application
  times the search out with a `COMException`.
- **A control that appears in response to something else needs the settle first** - priority buttons come
  alive when a row is selected, and WPF builds them a frame after the click that caused them.

## Look at it

| | How | Read it | When |
|---|---|---|---|
| **Screenshot** | FlaUI `Capture.Element(window)`, or `RenderTargetBitmap` in process | open the PNG and judge it | after anything that changes the screen |
| **Video** | FlaUI `VideoRecorder` (needs ffmpeg) | watch it | multi-step flows, something that flashes past |
| **Tree dump** | dump UIA to JSON | compare to what you expect | when you need exact values, not eyes |

**Counting elements proves something was drawn; only a picture proves the right thing was drawn.** Shoot
both themes - a colour that works in dark and vanishes in light is a real defect and a count never sees it.

Every UI task ends with at least one screenshot **opened and judged**, and the evidence row says which file.

## Before you call it done

1. The suite is green **more than once** - a UI suite that passes one run in three is not green, it is
   lucky. Run it again before reporting.
2. Every bug found while driving became a test case, not just a fix.
3. At least one screenshot per theme was opened and judged against the plan's wireframe, plus one with the
   window at its `MinWidth`/`MinHeight` - clipped text and a squeezed column only show up there.
4. Anything the fakes could not reach is named as not verified, not implied as passing.

Where a host application is involved (an add-in's real window, the ribbon, a dockable pane), this skill
stops and the host's own live skill takes over - the window there belongs to a process you did not start.
