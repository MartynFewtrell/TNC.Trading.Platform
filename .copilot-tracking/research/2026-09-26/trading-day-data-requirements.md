# Trading-day market-data requirements

## Purpose

Capture the desired behavior of automatic market-data gathering and viewing from a user's perspective. This records the original request and the choices confirmed in discussion; it is not an implementation proposal.

## Requirements stated by the user

- The backend gathers the necessary data automatically on a schedule.
- The frontend provides a way to view data after it has been obtained.
- IG requests are ordinarily initiated only within the Trading Day; see the explicitly confirmed exception below for an update already in progress when the window closes.
- If there is one scheduled update, it is requested at least at the beginning of the Trading Day.
- Once an update request starts, it updates the following data:
  1. Market Categories.
  2. Instruments belonging to Categories marked as interesting. The original description mentioned individually flagged Instruments; the decision below explicitly replaces that selection rule.
  3. Details for **all** Instruments in interested Categories.
- The scheduler is flexible: when the Trading Day changes, it reassesses its schedule and initiates updates if required.

## Remaining decisions

No further user-behavior choices are outstanding from this discussion. Any future changes to the desired behavior should be recorded explicitly rather than inferred from these notes.

## Decision log

- **Updated decision (2026-09-26):** The market-data Trading Day must align with the **applied broker's trading calendar**. The broker-calendar settings may be updated to achieve this alignment. The earlier decision to run on every calendar day, including weekends, is superseded: only days permitted by the aligned broker calendar are Trading Days.
- There is **one start and one end for each Trading Day governing all market-data processes** (categories, instrument lists, details, manual updates and follow-up retries), not independent windows for each process, category or instrument. Calendar day eligibility may vary, but the start/end times are shared rather than independently configured per process.
- **Updated decision (2026-09-26):** One shared application process/code path determines the current trading state from the aligned broker calendar and other relevant operational conditions. All subsequent processes consume that shared determination rather than independently deciding whether the application is able to trade or whether the Trading Day is open. Being within the shared Trading Day, being operationally able to trade, and being allowed to start a market-data update are related but distinct states; a full update already admitted during the Trading Day may continue after closing as specified below.
- The window's times use a **named time zone selected in settings**, initially **Europe/London**, so daylight-saving changes follow that time zone.
- Initial shared window: **08:00–17:00, same day**, in the selected time zone, on broker-calendar trading days. **The times must be configurable.**
- Configured start and end **must stay within the same local calendar day**; overnight windows are not required.
- The number of **scheduled complete updates per Trading Day is user-configurable**, initially **one**. When positive, divide the window into that many equal slots and start a full update at the **beginning of each slot**, including at Trading Day opening but **never at closing time**. For an 08:00–17:00 window with three updates, starts would be 08:00, 11:00 and 14:00.
- A count of **zero** disables **timed scheduled updates only**. Manual updates remain available during the Trading Day, and marking a Category as interesting **still triggers** an automatic full update during the Trading Day. Existing data remains visible while the timed schedule is disabled.
- There is **no explicit maximum** number of timed full updates. Warn the user when they configure **more than four** timed updates in a day, rather than rejecting the setting.
- If the application starts late or returns after missing one or more scheduled starts **during the Trading Day**, start **one complete catch-up update promptly**, then resume future scheduled starts. Do not replay each missed start.
- A catch-up beginning within a **configurable number of minutes** before the next slot, initially **15 minutes**, covers that upcoming slot too rather than requesting a near-duplicate update.
- If the Trading Day window, time zone, or update count changes, **recalculate the schedule immediately**. When the new window is active and an update required under the new schedule has not been covered, **initiate a complete update promptly**; do not start a new update outside the new Trading Day.
- **Explicit exception to the normal IG request window:** once a full update has started within the Trading Day, its IG requests may continue after closing while it attempts to update all three data groups. Do not start a new update outside the Trading Day. This completion rule takes precedence over the original blanket out-of-hours restriction for an already-started update; item failures may still leave the cycle partial.
- Full updates **must not overlap**. If a scheduled slot arrives while a full update is still running, the running update **covers that slot**; do not queue or start a duplicate.
- On failure of an individual Category or Instrument, **continue updating the other items** and **retry that item up to three total attempts per cycle, with a short increasing wait between attempts**. Clearly show which data remains stale; a cycle can finish in a **partial** state.
- Users select interested **Categories**; there is **no individual Instrument flag controlling detail collection**. Refresh the list and details for **every Instrument** in each interested Category. This explicitly supersedes the initial "instruments flagged within a Category of interest" selection rule.
- Marking a Category as interesting **during the Trading Day** should prompt a **complete update promptly** rather than waiting for the next scheduled slot. No update may start outside the Trading Day.
- An active update uses the Category-interest selection **captured at its start**. If that selection changes, finish the current update; if newly interested data remains missing, start **one further full update during the Trading Day**. If the window has closed, defer that new update until the next Trading Day.
- The frontend keeps the **most recent available data visible** while updates run or partially fail. Display **per-section or per-item updating/stale/failed labels** and **last-successful timestamps**, so users can tell which values are current and which are older.
- Users can request an **extra manual full update only during the Trading Day**. It refreshes all three data groups and follows the same rule that only one full update can run at once.
- A manual update or Category-change update covers an upcoming scheduled slot **if it begins within the configurable lead-in period (initially 15 minutes) or is still running when that slot arrives**. Otherwise it remains an extra update.
- Before any successful market-data collection, show an explicit **"no data yet"** state with the **next scheduled update, if one exists**, and, only during the Trading Day, a **manual full-update action**. With zero timed updates configured, show that no timed update is scheduled rather than inventing a next time.
- If Category or Instrument-list refresh fails after its retries, continue fetching details using the **last known list**, if available, while clearly marking that list as **stale**. This can omit newly added Instruments until a subsequent successful list refresh.
- After a partially failed full update, schedule **failed-item-only follow-up retries** rather than another full update. This is an **explicit exception to the "every update refreshes all three groups" rule**: a full update still attempts all three groups, but later recovery requests target only failed items. These follow-up retries are **new work allowed only during the Trading Day**; cancel any still pending at closing and wait for the next scheduled full update on the following Trading Day.
- After an item's three attempts in the full update fail, make **one additional failed-item-only attempt 15 minutes later** if still within the Trading Day. If it fails again, leave it stale until the next scheduled full update (or a manually requested full update). No follow-up attempt starts at or after closing.
- When no Categories are marked interesting, a scheduled full update still refreshes the **Market Categories list**. The Instrument-list and detail stages have no selected items to refresh; show that explicitly rather than requesting data for every Category.
- The frontend browses the **latest available data for each Category and Instrument**, with its **last-successful timestamp**; no historic update snapshots are required.
- When interest in a Category is removed, its previously collected Instrument data **remains visible and explicitly labeled as stale/historical**. Do not automatically refresh its Instrument details unless it becomes interesting again.
- If a user requests a manual update while one is already running, show that the update is **in progress** and **do not queue or start another**.
- Retain the **latest collected market data, status, and last-successful timestamps across application restarts** until newer results replace them.
- If the application starts outside the Trading Day, keep retained data visible and **wait until the next opening** for the scheduled full update **when the timed update count is positive**. With a zero count, wait for an allowed manual update or a Category-interest change instead. Do not replay missed prior-day updates or initiate an out-of-window catch-up.
- A full update proceeds in the stated order: **Market Categories, Instrument lists in interested Categories, then details for their Instruments**. Complete each group's attempts before moving to the next. Mark the cycle **successful only when all selected items succeed**; otherwise mark it **partial** and identify failures.
- If Trading Day settings or Category interest change repeatedly during an active cycle, **coalesce those changes into at most one additional full update**, using the latest settings and selection, only if still needed and able to start during the current Trading Day. Do not queue one update per edit.
- At a daylight-saving transition in the selected named time zone, if a configured local start time does not exist, **start at the next valid local time**; if the local time repeats, **run the slot only once, at the first occurrence**.
- The frontend shows **current/last update status**, **last-successful timestamps** and the **next scheduled update time, if one exists**, but does not need a browsable list of prior update outcomes or a full update history.
