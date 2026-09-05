# Reader contribution descriptions

Published descriptions, audited against their branch diffs on 2026-09-05. See [submission status](SUBMISSIONS.md).

## 1. Resume fix PR

**Title:** Refresh cached resume positions for EPUB books and comics

### Changes

Resuming an EPUB book or comic from a cached details screen can reuse an outdated saved position when a progress notification is missed. Reusing that position can overwrite newer progress already saved on the server. When the requested resume position matches the cached nonzero value, this change fetches the latest user progress before opening the reader. Starts from the beginning and other explicit positions are preserved, and the latest saved position may move backwards. A failed progress lookup stops the resume attempt instead of reusing stale progress.

### Issues

Related to #3575.

### Code assistance

Codex was used to investigate the bug, implement the fix, and write and run regression tests.

---

* [ ] I have read and followed the [contributing guidelines](https://github.com/jellyfin/jellyfin-web/blob/master/CONTRIBUTING.md).
* [x] I have tested these changes.
* [x] I have verified that this is not duplicating changes in an existing PR.
* [ ] I have provided a *substantive* review of [another web PR](https://github.com/jellyfin/jellyfin-web/pulls).


## 2. Comic API proposal for Meta

**Title:** Reduce comic startup time with individual page delivery

In a local comparison, the time to display the first page of a **309-page comic fell from 7.86 seconds to 0.16 seconds**, a **98% reduction**.

The current reader downloads the entire archive and extracts its images before opening the comic. The proposed API provides an authenticated manifest containing the page count and archive version, plus endpoints for individual page images. The reader can show the requested page as soon as it is available and fetch nearby pages as needed. The server reuses archive indexes and extracted images, while the browser keeps a bounded page cache.

[These comic figures](https://github.com/daniel-stilman/jellyfin/blob/4b25579a58a09b4b7d9aa294c571cb8d4d55db85/custom-build/READER-PERFORMANCE.md#comic-startup-matched-original-and-fork-comparison) are medians of three opens per build on the same local server, using fresh browser sessions and an empty application cache. They were measured in desktop Edge over loopback; device, archive and network conditions affect the result.

The proposed comic work consists of coordinated server and web changes. The working 10.11.11 implementation consists of the [comic reader changes](https://github.com/daniel-stilman/jellyfin-web/commit/20ec84b1bbaafc7d503d9a821b688c78a3b30d5a) and [native comic API changes](https://github.com/daniel-stilman/jellyfin/commit/6b863321b), and the [server API has also been adapted to master](https://github.com/daniel-stilman/jellyfin/tree/feature/comic-page-api). The web adaptation is still pending.

### Code assistance

Codex was used to investigate the loading delays, implement the changes, and develop and run tests and benchmarks.

## 3. EPUB startup PR

**Title:** Reduce EPUB startup time with background location indexing

### Changes

In a local comparison, opening a six-book EPUB omnibus reached its first usable page in **0.20 seconds instead of 20.17 seconds**, about a **99% reduction**. This displays the first page before generating the full location map, removes EPUB.js's fixed 100 ms pause between sections while retaining browser yields, and caches completed maps in IndexedDB for reopening. Existing saved progress is retained until valid progress is available, and page turns during indexing take precedence over a delayed resume. Unusable cache entries are regenerated, and storage failures fall back to uncached generation.

| Omnibus EPUB | Current master | With this change |
| --- | ---: | ---: |
| First usable page, cold context | 20.17 s | **0.20 s** |
| Location map ready, cold context | 20.17 s | 3.07 s |
| Location map ready, warm context | 20.18 s | 0.21 s |

Medians of three opens per build and cache condition, using the same 6.75 MB EPUB with 171 spine items in a production-compiled BookPlayer harness, desktop Edge and local HTTP. Exact saved-percentage restoration still waits for the map; these figures describe this local desktop workload. [The measurement method, samples and validation results](https://github.com/daniel-stilman/jellyfin/blob/df536b8255be53158cdd6b43c33b3a022f82cde7/custom-build/READER-PERFORMANCE.md#epub-startup-current-master-contribution) include the other four EPUBs checked.

### Issues

Fixes #8422.

### Code assistance

Codex was used to investigate the loading delays, implement the changes, and develop and run regression tests and benchmarks.

---

* [ ] I have read and followed the [contributing guidelines](https://github.com/jellyfin/jellyfin-web/blob/master/CONTRIBUTING.md).
* [x] I have tested these changes.
* [x] I have verified that this is not duplicating changes in an existing PR.
* [ ] I have provided a *substantive* review of [another web PR](https://github.com/jellyfin/jellyfin-web/pulls).
