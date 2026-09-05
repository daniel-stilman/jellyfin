# Reader changes and upstream contribution guide

This guide explains the changes, their preservation in the forks, and the separate upstream submissions. It provides the context needed to assess the implementation and answer review feedback.

## Where the changes are saved

The full source, dependency versions, unit tests and build/deployment instructions are committed in these GitHub forks:

- [Server](https://github.com/daniel-stilman/jellyfin), default branch `custom/10.11.11-readers`.
- [Web](https://github.com/daniel-stilman/jellyfin-web), default branch `custom/10.11.11-readers`.

Both repositories have a `readers-r5` tag identifying the exact deployed source. Later commits on the default branches document the deployment and contribution process.

The former [maintenance repository](https://github.com/daniel-stilman/jellyfin-custom-reader) held an overlay patch, standalone comic plugin source, build instructions and installation records. The new forks contain the complete upstream source history plus the custom commits.

The entire old web patch was independently applied to the original v10.11.11 source in a temporary Git index. Its resulting tree exactly matched the imported fork tree at `20ec84b1b`: `afe8c6ac8199dfa60fdda72f18d1e110deb6605e`. Thus the previous EPUB and comic web improvements were imported intact before adding the resume fix. The native server port was separately tested because its namespace, dependency version and registration changed.

GitHub preserves the source needed to rebuild the custom release. The Jellyfin database, media files, compiled release directories and private raw benchmark data need their own backup outside this PC.

## What changed

### EPUB startup and progress indexing

Previously, Jellyfin kept the reader behind its loading indicator until it had calculated the entire book's location map, even when the first section had already rendered. That map supports reading percentages and resume. EPUB.js also inserted a 100 ms delay between sections during this calculation, which accumulated badly for large books.

The reader now reveals the first rendered section and prepares the map in the background. It removes that fixed delay while still yielding to the browser, and stores completed maps in IndexedDB so later opens can reuse them. Cache failures fall back to generating the map. Saved-position restoration uses the map once it is available; the first visible page metric does not mean every background operation has finished.

During the fork migration, five EPUBs passed the original cold/warm performance gates over 30 opens. The large omnibus reached its first usable page in a median 214.8 ms cold and 209.2 ms warm, versus 20,173 ms in the original unmodified baseline. These are local compiled-reader measurements, not a promise of identical tablet or network timings.

Preserved in [web commit 9ef9b5540](https://github.com/daniel-stilman/jellyfin-web/commit/9ef9b55400bc0bc564d5f023500b75cd3a2e7bb1), together with the illustrated-EPUB layout and navigation improvements.

### Comic startup, memory and page reliability

Previously, the client downloaded the comic archive and extracted all its pages before opening the reader. Large archives caused long waits and excessive browser memory use. The virtual page display also had cases where pages stayed blank.

The reader now asks the server for a manifest containing the page count and archive version, then fetches individual images as needed. It prioritizes the requested starting page, keeps a small cache of nearby/recent pages, cancels pending work and releases image URLs on close. The virtual page display is refreshed as images become available. A local archive fallback remains for servers without the page API.

On the server, archive entries are sorted in natural page order, archive indexes persist across restarts, and extracted images are cached for reuse. The requested starting page can be prepared while a new archive is already open for indexing. This originally lived in the Custom Comic Pages plugin; the fork now supplies it as a native authenticated API. Bookshelf remains supported.

The migrated build opened a 309-page CBR in 503 ms and a 1,064-page CBR in 898 ms in local browser checks. Sampled beginning, adjacent, distant and final pages decoded correctly, with no whole-archive download. Image URLs were bounded during use and all released on close. These checks confirm the optimization still works; they are not a controlled speed comparison with the preceding custom plugin.

A later matched comparison of the original 10.11.11 web reader and the fork measured the same 309-page comic over three opens per build: median startup fell from **7.86 seconds to 0.16 seconds**, a **97.93% reduction**. These measurements supersede the earlier after-only figures for the proposal headline. [Performance evidence](READER-PERFORMANCE.md) records the conditions, raw samples and the boundaries of the older measurements.

Preserved in [web commit 20ec84b1b](https://github.com/daniel-stilman/jellyfin-web/commit/20ec84b1bbaafc7d503d9a821b688c78a3b30d5a) and the [native server commit](https://github.com/daniel-stilman/jellyfin/commit/6b863321b).

### Illustrated EPUB layout and navigation

Some illustrated EPUBs contain fixed page dimensions but omit the usual fixed-layout declaration. The reader now recognizes that information and chooses the appropriate layout. It also has matching previous/next edge controls. This preserves complete pages within the mobile viewport; the first and next page were checked at a 390 by 844 viewport with device scale factor 3.

This is a distinct user-facing improvement from EPUB startup performance and is a good candidate for a separate upstream PR.

### Resume rollback

A restored details screen could keep an old resume value when it missed a WebSocket notification. The server had successfully saved newer progress, but opening the book from the stale screen reused the older value and could write it back to the server.

Both readers now refresh current user progress when resuming from that cached nonzero value. Explicit starts, restarts and deliberately saved backward progress remain supported. A real-browser test reproduced zero-based page 960 rolling back to 836; the same test with the fix retains 960. Seven unit cases cover the related behaviors.

Deployed in [web commit 19fcbc2f4](https://github.com/daniel-stilman/jellyfin-web/commit/19fcbc2f4).

## Existing contribution branches

| Repository and branch | Scope | Preparation status |
| --- | --- | --- |
| [Web: fix/book-resume-progress](https://github.com/daniel-stilman/jellyfin-web/tree/fix/book-resume-progress) | Resume rollback fix and unit tests, adapted to upstream's newer SDK and reader UI | Based on upstream master; 169 web tests, types and changed-file lint passed |
| [Server: feature/comic-page-api](https://github.com/daniel-stilman/jellyfin/tree/feature/comic-page-api) | Authenticated comic manifest/page endpoints, archive index and image cache | Based on upstream master; 147 API tests and .NET 10 analyzer build passed; [design proposal #149](https://github.com/jellyfin/jellyfin-meta/discussions/149) is open |
| [Web: perf/epub-startup](https://github.com/daniel-stilman/jellyfin-web/tree/perf/epub-startup) | EPUB first-page display, background location generation and IndexedDB cache; excludes layout/comic/resume-fetch changes | Based on current master; 190 tests, types, lint, production build, 28 benchmark opens and six browser scenarios passed |

The EPUB speedup now has a separate current-master contribution branch in the existing web fork. The comic web adaptation and illustrated-EPUB layout contribution remain separate work; their stable implementations are still committed and deployed on the custom release. The comic client and server parts must agree on the page API.

The [resume PR #8421](https://github.com/jellyfin/jellyfin-web/pull/8421) and [comic API proposal #149](https://github.com/jellyfin/jellyfin-meta/discussions/149) were submitted on 2026-09-05. The PR references the existing [progress report #3575](https://github.com/jellyfin/jellyfin-web/issues/3575). See [submission status](SUBMISSIONS.md).

## How contributing works

A push uploads commits to the owner's fork. A pull request asks Jellyfin's maintainers to review and merge selected commits into their repository. The normal route is fork, feature branch, push to the fork, then PR targeting upstream master. Maintainers review the change, automated checks run, the contributor answers questions and updates the same branch, and maintainers decide whether to merge it. Updating a branch updates its existing PR. See GitHub's [fork-to-PR instructions](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/proposing-changes-to-your-work-with-pull-requests/creating-a-pull-request-from-a-fork).

The [development guide](https://jellyfin.org/docs/general/contributing/development/) asks for an issue when a bug has no existing issue, a Meta discussion for major changes spanning projects, and a first-time contributor entry in CONTRIBUTORS.md. The web-specific guide treats that contributor entry as optional. The issue/proposal and review preparation still remain. The complete deployment branches should not be submitted as one large PR.

## What accepted descriptions look like

Examples were checked as merged PRs, including their public discussion. Their acceptance does not establish that a particular writing style guarantees acceptance.

- [Web #8025: screensaver handling while reading](https://github.com/jellyfin/jellyfin-web/pull/8025) explains the visible problem, why the previous activity detection missed readers, and how reader open/close events change that. A reviewer discusses the edge case where those events become unbalanced. This is a useful example of a concise problem-and-mechanism explanation and normal technical review.
- [Web #8242: book fullscreen support](https://github.com/jellyfin/jellyfin-web/pull/8242) uses a very short body for a readily visible change. A lengthy essay is not always needed.
- [Server #17422: query performance](https://github.com/jellyfin/jellyfin/pull/17422) has more implementation detail, a defined benchmark workload, comparable before/after measurements and an assistance disclosure. Discussion separates the reusable benchmark harness from the production change. This is a useful model for the evidence accompanying our speed improvements.
- [Server #13605: cache limits](https://github.com/jellyfin/jellyfin/pull/13605) includes discussion of eviction behavior, implementation choice and observed cache sizes. Cache behavior and limits are substantive review topics for our comic API.

Both current [server](https://github.com/jellyfin/jellyfin/blob/master/.github/pull_request_template.md) and [web](https://github.com/jellyfin/jellyfin-web/blob/master/.github/pull_request_template.md) templates ask for a Changes section of one to five sentences, related issues and a Code assistance section. The web template additionally requests before/after images for UI changes and confirmations of testing, duplicate checking and a substantive review of another web PR. Those boxes should only be checked after completing the stated work.

A sample of 20 recent merged, non-dependency web PRs checked on 2026-09-05 contained 19 checked testing confirmations and no separate Testing heading. This is a small descriptive sample, not a requirement or a guarantee of acceptance. Testing remains expected; the template does not require a separate testing narrative. For the resume fix, keep the description short and retain the normal checklist instead of listing every test count.

For our work, a useful description should cover the visible problem, its cause, the chosen change, and evidence that the change works. A performance PR benefits from a small results table and the test conditions; a layout PR benefits from screenshots; the resume PR benefits from the reproduction steps and the explicit-start/backward-progress checks. There is no need to narrate the whole development session or every changed line.

Use a short action title. The web [contributing guide](https://github.com/jellyfin/jellyfin-web/blob/master/CONTRIBUTING.md) requests plain titles and discourages Conventional Commit prefixes. The Code assistance disclosure must accurately describe the assistance used here, which included investigation, implementation, tests and benchmarks.

The contributor remains responsible for understanding the implementation, assessing the evidence and answering review feedback. Each submission retains the Code assistance section describing the implementation and validation work.

The recommended order is the focused resume fix first, a separately adapted EPUB startup PR, an illustrated-EPUB layout PR, and the coordinated comic API/client work after the Meta discussion. This keeps each change understandable and independently reviewable.

The [revised contribution drafts](CONTRIBUTION-DRAFTS.md) lead with the observed problem or measured speed reduction, retain the code-assistance disclosure, and omit the unnecessary standalone testing narrative and broad closing question. The owner approved these descriptions for submission; the published links and validation state are recorded in [SUBMISSIONS.md](SUBMISSIONS.md).
