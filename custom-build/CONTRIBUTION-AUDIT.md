# Contribution scope audit - 2026-09-05

The public descriptions were compared with the actual diffs and benchmark records. The original web improvements remain preserved: applying the former maintenance overlay to v10.11.11 produces tree `afe8c6ac8199dfa60fdda72f18d1e110deb6605e`, identical to the imported fork at `20ec84b1b`. Both GitHub repositories are genuine forks with the expected Jellyfin parents. The deployed source remains identified by `readers-r5`.

| Submission or branch | Verified implementation | Audit result |
| --- | --- | --- |
| [Resume PR #8421](https://github.com/jellyfin/jellyfin-web/pull/8421) | Four files: EPUB and comic player call sites, the resume helper and seven regression tests | The body now specifies a cached **nonzero** resume value, EPUB/comic support, preserved explicit positions and rejection on progress lookup failure. It does not claim PDF support or repair of cached zero progress. |
| [Comic proposal #149](https://github.com/jellyfin/jellyfin-meta/discussions/149) | Six comic-only web files at `20ec84b1b`, plus the native server implementation at `6b863321b` | The body now describes the actual page-count/archive-version manifest. Links point directly to the comic commits. The EPUB paragraph remains removed. |
| [Server feature/comic-page-api](https://github.com/daniel-stilman/jellyfin/tree/feature/comic-page-api) | Nine files: controller, DTOs, service, comparer, registration, dependency and tests | No EPUB, resume or layout changes. Controller/service/manifest match the stable implementation. The current-master branch includes the required registration and dependency. |

The comic client has not yet been adapted to current master. The proposal states this explicitly; the stable client is a working prototype, not a submitted upstream client PR. The comic measurements are matched first-page timings, not the older whole-test wall times.

The original EPUB commit also contained illustrated-book layout inference and navigation changes. The new [`perf/epub-startup` branch / PR #8423](https://github.com/jellyfin/jellyfin-web/pull/8423) separates first-page availability, index generation and caching from those layout changes. Its five-file scope contains the book player, two TypeScript helpers and their tests; it contains no comic API, comic player, layout, stylesheet, template or cached-resume lookup changes.

See [submission status](SUBMISSIONS.md) for the resulting EPUB contribution and [performance evidence](READER-PERFORMANCE.md) for measurements. The saved-percentage location map becomes available after first-page display; first-page timing must not be described as completion of background indexing or exact resume restoration.
