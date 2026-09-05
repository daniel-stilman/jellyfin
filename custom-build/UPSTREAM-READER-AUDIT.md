# Reader contribution audit - 2026-09-06

The current reader changes were compared with `jellyfin/jellyfin-web` master at `18f0bc80c`. The deployed release remains `readers-r7`, web commit `ee6c7fef250b561147e775eeec7465f0968b2c97`; this contribution work changes no installed files.

## Submission status

The maintainer closed resume PR [#8421](https://github.com/jellyfin/jellyfin-web/pull/8421), EPUB startup PR [#8423](https://github.com/jellyfin/jellyfin-web/pull/8423), EPUB issue [#8422](https://github.com/jellyfin/jellyfin-web/issues/8422), and comic proposal [#149](https://github.com/jellyfin/jellyfin-meta/discussions/149), citing the LLM policy; the two PR closures also cite contribution requirements. No new official PR, issue, discussion or comment was posted during this audit.

The [project policy](https://jellyfin.org/docs/general/contributing/llm-policies/) requires contributor-written explanations, understanding and review of assisted code, and the ability to handle review feedback. The [web PR template](https://github.com/jellyfin/jellyfin-web/blob/master/.github/PULL_REQUEST_TEMPLATE.md) also asks for a substantive review of another web PR. Those human contribution requirements remain outstanding. The notes below are technical evidence for review, not text to paste into official submissions.

## Prepared candidate branches

Each branch starts directly from `18f0bc80c`, has one commit, and contains exactly three files. They are independent of each other and of the earlier resume, startup and comic API branches. Both have been pushed to the owner's GitHub fork.

### PDF render completion

- Branch: [`fix/pdf-render-completion`](https://github.com/daniel-stilman/jellyfin-web/tree/fix/pdf-render-completion).
- Commit: [`26c3447c6722523ce2143a6a2ab9f16ebad818be`](https://github.com/daniel-stilman/jellyfin-web/commit/26c3447c6722523ce2143a6a2ab9f16ebad818be).
- Files: `src/plugins/pdfPlayer/plugin.js`, `src/utils/pdfPageCache.ts`, `src/utils/pdfPageCache.test.ts`.

Upstream replaces the visible canvas immediately, before the asynchronous PDF.js render finishes. The candidate publishes only completed canvases and leaves the previous page visible while its replacement is rendering. Only the most recent display request can replace the visible page. A cancellable render cache deduplicates requests, limits rendering to two concurrent tasks, prioritizes the requested page and retains the existing two-page neighborhood on each side. Resizing invalidates old render work; closing cancels the cache.

This is an extraction and adaptation of the completed-canvas rendering work in `readers-r6`. It retains upstream's PDF controls, styling, fitting and device pixel ratio. It does not include Swiper, momentum, zoom controls, page counters, the custom canvas pixel cap, saved-resume lookup changes, or other reader formats. Cancellation here covers page rendering after the document has opened; it is not a fix for the separate document-download lifecycle.

Validation: all **167 unit tests** passed, including five PDF cache tests. TypeScript, changed-file ESLint, the production build and ES5 checks passed; ES5 checked 982 generated files. A production-compiled harness using the actual PDF player reproduced the unfinished canvas on upstream at 412x892 and 1024x1366. Both candidate runs kept the previous page until completion and passed rapid jumps, the final page, obsolete completion, resize and stop-during-render checks. There are no new startup-time benchmark claims.

### EPUB table-of-contents paths

- Branch: [`fix/epub-toc-relative-links`](https://github.com/daniel-stilman/jellyfin-web/tree/fix/epub-toc-relative-links).
- Commit: [`0eb5eb9de024dc899aa8155acafcbf398e0c1ab4`](https://github.com/daniel-stilman/jellyfin-web/commit/0eb5eb9de024dc899aa8155acafcbf398e0c1ab4).
- Files: `src/plugins/bookPlayer/tableOfContents.js`, `src/utils/bookPlayerToc.ts`, `src/utils/bookPlayerToc.test.ts`.

Upstream prepends the package directory to chapter links and removes one leading `../`. Navigation links actually refer to the navigation document's directory. For example, `chapter.xhtml` from `/OEBPS/Text/nav.xhtml` currently becomes `/OEBPS/chapter.xhtml`, so EPUB.js cannot find the section. The candidate resolves the navigation document through the book's package path, then resolves each chapter URL relative to that document. Existing display code converts the resulting archive path back to a package-relative target. Fragment identifiers and escaped filenames survive this resolution.

This is a focused adaptation of the TOC correction in `readers-r6`, with the package directory included so navigation outside the package's own directory also resolves correctly. It includes no startup indexing, cache, resume, layout, gestures, font, theme, PDF or comic changes.

Validation: all **165 unit tests** passed, including three TOC helper tests. TypeScript, changed-file ESLint, the production build and ES5 checks passed; ES5 checked 982 generated files. Actual TOC clicks in a production-compiled EPUB.js harness failed on upstream for four synthetic EPUB arrangements: nested EPUB 3 navigation, multiple parent paths in an NCX, navigation outside a nested package directory, and escaped filenames. All four passed on the candidate; a standard package-level NCX passed on both builds. This demonstrates chapter navigation, not faster book loading.

The open TOC React migration [#8330](https://github.com/jellyfin/jellyfin-web/pull/8330) overlaps this area and replaces the legacy file. Its diff was inspected; it uses `spine.get(chapter.href)` directly. The candidate fixes the existing legacy TOC, but any eventual submission must coordinate with that migration. It is not represented as an independent UI rewrite.

## Remaining changes and overlap

| Local change | Current upstream finding | Contribution treatment |
| --- | --- | --- |
| PDF vertical centering | Current master already uses a centered grid and fitted canvas. | Do not submit the old stable-layout correction as a new master bug fix. |
| Missing PDF/EPUB page indicator | Open [#8320](https://github.com/jellyfin/jellyfin-web/pull/8320) unifies the progress indicator in BookOsd. | Do not submit a competing counter change. |
| Half-screen threshold and momentum | The restrictive threshold was introduced by our shared `readers-r6` settings. Upstream comics use default Swiper short-swipes; upstream PDF/EPUB use TouchHelper. | Keep the r7 regression fix in the custom fork. Do not label it an existing upstream halfway-threshold bug. |
| Comics flashing during prefetch | The forced `virtual.update(true)` callback belongs to our on-demand comic implementation; current master does not contain it. | Keep the correction with the custom comic client/API contribution. |
| Finger-following PDF/EPUB dragging | Adds a continuous page-transition interaction beyond upstream's discrete swipe navigation. | A separate navigation design contribution, requiring discussion and adaptation to current BookOsd; not included in either candidate. |
| Removal of page arrows | A UI preference that affects controls and accessibility. | Not included in either bug-fix branch. |
| Image-only EPUB progress | The custom fork uses a spine-position fallback alongside its fixed-layout handling. Open [#7353](https://github.com/jellyfin/jellyfin-web/pull/7353) changes precise EPUB progress storage. | Remains preserved in r7. No independently validated master candidate prepared here; it needs its own reproduction and compatibility assessment. |
| EPUB close/load cleanup and illustrated-page layout | Coupled to the custom rendition, startup and drag lifecycle. | Preserved in r7; not silently carried into the TOC fix. Separate adaptation and tests remain necessary. |
| EPUB indexing/startup and cached resume | Existing focused branches and now-closed PRs remain backed up. | No duplicate PRs or unrelated performance claims in new candidates. |
| Native comic page delivery | Existing server prototype and stable client remain backed up; proposal #149 is closed. | No new server changes were part of r6/r7. Current-master client adaptation and a compliant proposal remain outstanding. |

## Evidence and limits

See [reader contribution evidence](reader-contribution-evidence/README.md) for the synthetic fixture generator, browser results and PDF images. Local runnable harnesses and full logs are in `validation/reader-contributions` outside the source branches. Baseline browser builds used `b8a9496d4`; its PDF, EPUB and comic source directories are byte-identical to `18f0bc80c` (the intervening commit only updates a translation). The candidates were built and checked independently, not validated by citing the 239-test custom release run.

The browser checks used desktop Edge 152 with controlled viewports and synthetic files. They did not use the occupied physical phone or production library credentials. Both production builds reported the existing webpack asset-size warnings. The source branches contain no harness files, fixture content, generated assets, release notes or unrelated source edits. Their file sets and remote commit hashes were checked after pushing.
