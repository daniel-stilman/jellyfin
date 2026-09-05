# Upstream submissions

Submitted on 2026-09-05 with the owner's authorization and approved descriptions.

| Submission | Destination | Status when recorded |
| --- | --- | --- |
| [Refresh cached resume positions for EPUB books and comics](https://github.com/jellyfin/jellyfin-web/pull/8421) | jellyfin/jellyfin-web, PR #8421, targeting master | Open for review |
| [Reduce comic startup time with individual page delivery](https://github.com/jellyfin/jellyfin-meta/discussions/149) | jellyfin/jellyfin-meta, Proposals discussion #149 | Published |

## Resume PR

Source branch: `daniel-stilman:fix/book-resume-progress` at `c3b96c57e1d22988c54ac9cabe3a654cbe9f94ef`, rebased onto upstream `b8a9496d4` before submission. All 169 unit tests, TypeScript checks and changed-file lint passed after the rebase. The PR references existing issue #3575 without automatically closing it. Its published body and head commit were read back and verified.

The testing and duplicate-check confirmations are checked. The overall contributing-guidelines confirmation and substantive-review-of-another-PR confirmation remain unchecked. The code-assistance disclosure describes the investigation, implementation and regression testing performed with Codex.

## Comic API proposal

The proposal covers comic loading, the page API and caching, with the measured 309-page comic startup reduction from 7.86 to 0.16 seconds and links to the comic benchmark evidence and implementation. The unrelated EPUB paragraph was removed at the owner's request on 2026-09-05. The audit also corrected the manifest description to page count and archive version, and linked the focused comic commits instead of the entire custom release. The updated published body was read back and verified.

The server feature branch exists. The comic web adaptation still needs preparation. The separate EPUB branch `perf/epub-startup` is now committed and pushed, with its validation and benchmark evidence recorded. The proposal is a design discussion; no comic server or client PR has been opened yet.

The deployed custom release remains identified by the `readers-r5` tags. Upstream submission does not mean acceptance or inclusion in an official release.
