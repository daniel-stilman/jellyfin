# Reader contribution evidence

These records accompany the [2026-09-06 scope audit](../UPSTREAM-READER-AUDIT.md). All pictured pages and EPUB fixtures were generated for the test; they contain no real library content.

`make_fixtures.py` creates a 24-page PDF and five EPUBs. The PDF browser harness delays a chosen PDF.js `RenderTask` before it paints, calls the actual player's page-loading method, then releases the task. It checks pixels and display request ordering while using the actual production-compiled player. A second delayed page is superseded by a jump, followed by resize and close checks. The EPUB harness constructs the actual legacy `TableOfContents` and follows its links in a real EPUB.js rendition.

| Record | Result |
| --- | --- |
| [baseline-pdf-browser.json](baseline-pdf-browser.json) | The upstream reader exposes a transparent, unfinished canvas at both tested sizes. |
| [pdf-pdf-browser.json](pdf-pdf-browser.json) | The candidate retains the completed page and passes completion, ordering, resize and cancellation checks at both sizes. |
| [baseline-toc-browser.json](baseline-toc-browser.json) | Four path arrangements fail with `No Section Found`; the standard NCX succeeds. |
| [toc-toc-browser.json](toc-toc-browser.json) | All five chapter links display their target with no recorded errors or rejections. |

For baseline records, `passed: true` means the expected upstream failure or control result was reproduced. It does not mean the bug is absent. These are deterministic regression scenarios, not timing benchmarks. The viewport is 412x892 or 1024x1366 for PDF and 800x1000 for EPUB.

## PDF rendering while the next page is held

Upstream:

![Upstream exposes an unfinished page while rendering is held](baseline-pdf-412-waiting.png)

Candidate, still showing the completed first page:

![Candidate retains the completed first page](pdf-pdf-412-waiting.png)

Candidate after rendering page 8 completes:

![Candidate displays the completed requested page](pdf-pdf-412-ready.png)
