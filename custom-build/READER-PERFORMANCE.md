# Reader performance evidence

These measurements support the owner's contribution drafts. Comic delivery and EPUB indexing are separate changes, with separate benchmark methods.

## Comic startup: matched original and fork comparison

A new comparison on 2026-09-05 measured the preserved original Jellyfin web 10.11.11 build and the deployed readers-r5 web build against the same isolated readers-r5 server. The original web build uses the existing whole-archive download endpoint; the fork uses the new comic page API.

| 309-page CBR | Original reader | Fork reader |
| --- | ---: | ---: |
| Open 1 | 10,727.2 ms | 312.8 ms |
| Open 2 | 7,862.2 ms | 162.7 ms |
| Open 3 | 6,667.8 ms | 162.0 ms |
| Median | **7,862.2 ms** | **162.7 ms** |
| Whole archive downloads per open | 1 | 0 |

The median reduction is **97.93%**. Suggested short wording: **7.86 seconds to 0.16 seconds, about a 98% reduction in startup time**.

The measurement starts at the Read button click and ends when the active first-page image has decoded, has nonzero display dimensions, and its dialog is visible, followed by an animation frame. Both variants reported 309 pages and the same 4096 by 2880 first-page image. No uncaught page errors occurred in the six completed measurements.

Conditions: Windows, Intel Core i7-13700F, 32 GiB RAM, headless Edge 152.0.4191.62, 1280 by 900 viewport, 715,110,283-byte CBR, local loopback connection without network throttling. Each variant/repetition used a fresh server process and empty application cache; each open used a fresh browser context. The operating system's file cache was retained. Execution order was original/fork, fork/original, original/fork. This small local sample describes these files and this machine; it is not a universal tablet or network speed claim.

The fork's comic JavaScript/CSS, web index, archive worker, Jellyfin.Api.dll and jellyfin.dll hashes matched the deployed release manifest. [Measurements, environment and build identifiers](benchmarks/comic-startup-2026-09-05.json) are retained without account credentials or private server logs.

## EPUB startup: separate implementation and benchmark

The original compiled-reader benchmark and the rebuilt fork used the same first-usable-page definition. Each reported condition has three samples. The baseline was recorded on 2026-08-17 and the fork was measured on 2026-09-05; these are historical harness comparisons, not the same run as the comic comparison above.

| Large omnibus EPUB | First usable page, median |
| --- | ---: |
| Original, cold location cache | **20,173.0 ms** |
| Fork, cold location cache | **214.8 ms** |
| Fork, warm location cache | **209.2 ms** |

Cold first-page time fell by **98.94%**. Suggested short wording: **20.17 seconds to 0.21 seconds, about a 99% reduction**.

This measures when reading can begin. Cold location-map generation still took a median 3,067.2 ms in the background; the first-page result does not mean the map or exact saved-percentage restoration is complete. Completed maps are cached for reuse. Five EPUBs passed the original performance gates over 30 fork opens. [Comparison results](benchmarks/epub-startup-2026-09-05.json) retain all five workloads and their sample counts.

## EPUB startup: current-master contribution

The isolated `perf/epub-startup` branch was compared with unmodified upstream master `b8a9496d4` on 2026-09-05, using the same production-compiled BookPlayer harness and EPUB.js 0.3.93. The final candidate is `4f313fc72738b60861733f9a67ed9efd936217d8`.

| Six-book omnibus EPUB (171 spine items, 6,754,551 bytes) | Upstream master | EPUB performance branch |
| --- | ---: | ---: |
| First usable page, cold browser context, median | **20,172.3 ms** | **204.8 ms** |
| Location map ready, cold context, median | 20,172.3 ms | 3,072.7 ms |
| First usable page, warm context, median | 20,181.2 ms | 203.6 ms |
| Location map ready, warm context, median | 20,181.2 ms | 205.6 ms |

The cold first-page reduction is **98.98%**: **20.17 seconds to 0.20 seconds**. The cold map finishes about 3.07 seconds after opening; saved-percentage restoration waits for that map. New page choices during indexing take precedence over a delayed resume, and the existing saved percentage is retained until valid progress is available.

These are medians of three opens per build and cache condition. Four smaller EPUBs also passed one cold/warm pair per build, for **28 successful opens** in total. Both builds produced identical location counts for all five books. Instrumentation confirmed one generation per cold candidate open and zero on warm opens.

Conditions: Windows, Intel Core i7-13700F, 32 GiB RAM, headless Edge 152.0.4191.62, 1440 by 900 viewport, local HTTP without throttling. Each cold open used a fresh browser context; its warm open used a new page in the same context, retaining HTTP and IndexedDB caches. The operating system's file cache was retained. Build order alternated between repetitions. The harness compiles the real BookPlayer and supplies the SDK download URL locally; it does not exercise a Jellyfin backend or mount the React OSD. Timing runs from BookPlayer.play to a rendered, visible, spinner-free frame followed by two animation frames. Device and network conditions will change the result.

Six separate browser checks passed: cold and cached resume, corrupt-cache recovery, navigation during indexing, closing during indexing, and unavailable IndexedDB. A regression test also covers EPUB.js resolving display before emitting the new location, avoiding an intermediate report of page-zero progress. All **190 unit tests**, TypeScript, changed-file lint and the production build passed. [Sanitized samples, source identifiers, conditions and checks](benchmarks/epub-upstream-2026-09-05.json) retain the evidence without media files, credentials or local paths.

## Earlier figures and measurement boundaries

The earlier migrated-build checks recorded 503 ms for the 309-page comic and 898 ms for the 1,064-page compendium. Those were individual startup checks, with a different timing boundary, and had no matched original-reader run. They demonstrate that the deployed optimization still works, but are not the denominator for a before/after percentage.

The old comic harness's 17.11-second and 18.27-second wall times included subsequent page navigation and waits for blank pages. They must not be presented as startup times or paired with 503/898 ms to calculate a startup improvement.

A historical compendium check recorded 15,047 ms to 282 ms, but its baseline was an earlier custom reader falling back after a missing API endpoint, not untouched upstream. In the new attempt, the preserved original reader did not display the compendium's first page within the 120-second limit. That single timeout is not a completed before/after comparison and is not used in the headline. The repeated comparison was completed for the requested 309-page comic.

## PR writing convention check

A sample of 20 recent merged non-dependency web PRs contained 19 checked testing confirmations and no separate Testing heading. The [current template](https://github.com/jellyfin/jellyfin-web/blob/master/.github/pull_request_template.md) has a testing checkbox, Changes, Issues and Code assistance. The [sample list](benchmarks/pr-testing-style-2026-09-05.json) supports the count. This supports concise testing confirmation for the resume PR; it does not remove the need to test the code.
