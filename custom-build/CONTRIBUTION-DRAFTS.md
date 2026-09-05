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
