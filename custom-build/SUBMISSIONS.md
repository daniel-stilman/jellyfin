# Upstream submissions

Submitted on 2026-09-05 with the owner's authorization and approved descriptions.

| Submission | Destination | Status when recorded |
| --- | --- | --- |
| [Fix books and comics resuming from outdated reading progress](https://github.com/jellyfin/jellyfin-web/pull/8421) | jellyfin/jellyfin-web, PR #8421, targeting master | Open for review |
| [Reduce comic startup time with individual page delivery](https://github.com/jellyfin/jellyfin-meta/discussions/149) | jellyfin/jellyfin-meta, Proposals discussion #149 | Published |

## Resume PR

Source branch: `daniel-stilman:fix/book-resume-progress` at `c3b96c57e1d22988c54ac9cabe3a654cbe9f94ef`, rebased onto upstream `b8a9496d4` before submission. All 169 unit tests, TypeScript checks and changed-file lint passed after the rebase. The PR references existing issue #3575 without automatically closing it. Its published body and head commit were read back and verified.

The testing and duplicate-check confirmations are checked. The overall contributing-guidelines confirmation and substantive-review-of-another-PR confirmation remain unchecked. The code-assistance disclosure describes the investigation, implementation and regression testing performed with Codex.

## Comic API proposal

The proposal includes the measured 309-page comic startup reduction from 7.86 to 0.16 seconds and the separate EPUB reduction from 20.17 to 0.21 seconds, with links to benchmark evidence and the implementation. Its published body and Proposals category were read back and verified.

The server feature branch exists. The comic web adaptation and separate EPUB contribution branch still need preparation. The proposal is a design discussion; no comic server or client PR has been opened yet.

The deployed custom release remains identified by the `readers-r5` tags. Upstream submission does not mean acceptance or inclusion in an official release.
