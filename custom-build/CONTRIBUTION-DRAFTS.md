# Reader contribution drafts for the fork owner

These descriptions were approved by the owner and submitted on 2026-09-05: [resume PR #8421](https://github.com/jellyfin/jellyfin-web/pull/8421) and [comic API proposal #149](https://github.com/jellyfin/jellyfin-meta/discussions/149). The submissions retain the code-assistance disclosure below. See [submission status](SUBMISSIONS.md).

## 1. Resume fix PR

**Title:** Fix books and comics resuming from outdated reading progress

### Changes

Reopening a book or comic from a cached details screen can use an outdated reading position if the client missed a progress notification. This can jump back through the book and overwrite newer progress already saved on the server. The reader now refreshes the saved position before resuming; explicit starting positions and intentionally saved backward progress are preserved.

### Code assistance

Codex was used to investigate the bug, implement the fix, and write and run regression tests.

## 2. Comic API proposal for Meta

**Title:** Reduce comic startup time with individual page delivery

In a local comparison, the time to display the first page of a **309-page comic fell from 7.86 seconds to 0.16 seconds**, a **98% reduction**.

The current reader downloads the entire archive and extracts its images before opening the comic. The proposed API provides an authenticated page list and individual page images, allowing the reader to show the requested page as soon as it is available and fetch nearby pages as needed. The server reuses archive indexes and extracted images, while the browser keeps a bounded page cache.

These comic figures are medians of three opens per build on the same local server, using fresh browser sessions and an empty application cache. They were measured in desktop Edge over loopback; device, archive and network conditions affect the result.

A separate EPUB startup change reduced the first usable page time for a large omnibus from **20.17 seconds to 0.21 seconds**, about a **99% reduction**. It displays the book while preparing its location map in the background and caches that map for later opens. This would be a separate web contribution; it does not depend on the comic API.

The proposed comic work consists of coordinated server and web changes. A working implementation is available in the 10.11.11 fork, and the server API has also been adapted to current master. The web adaptation is still pending.

### Code assistance

Codex was used to investigate the loading delays, implement the changes, and develop and run tests and benchmarks.

## Notes for the owner

- The resume draft follows the short Changes and Code assistance format used by recent merged web PRs. The normal testing checkbox remains; a separate Testing section and a list of every test count are unnecessary here.
- The comic proposal leads with a measured before/after comparison. The EPUB result is identified as separate work so it is not incorrectly attributed to the comic API.
- The broad closing question has been removed. The scope and current implementation status are stated directly.
- Measurement conditions and supporting figures are recorded in [READER-PERFORMANCE.md](READER-PERFORMANCE.md).
