# Reader validation, September 5, 2026

Baseline: official server and web v10.11.11. The previous verified web overlay was imported unchanged before adding the resume fix. Its comic service was ported into Jellyfin.Api using SharpCompress 0.50.4.

A real browser opened a synthetic 1,200-page CBZ at zero-based page 836. With the notification WebSocket disconnected and HTTP working, it moved to 960. The server saved 960 before and after closing. Reopening the cached details view with the old reader opened and saved 836. The fixed reader opened and retained 960, including against the native API.

| Check | Result |
| --- | --- |
| Web suite | 193 passed, 18 files |
| Web types and changed-file lint | Passed |
| Production web build | Passed; 2 asset-size warnings |
| Server solution | 2,554 passed, 9 skipped, 0 failed |
| API suite after analyzer cleanup | 87 passed |
| Server Debug analyzers | Passed; 8 warnings in unchanged upstream code |
| Windows self-contained publish | Passed |
| Anonymous manifest and page requests | 401 |
| 1,200-page fixture bounds | Index 1,199 succeeds; 1,200 returns 404 |
| Bookshelf 13.0.0.0 | Loaded alongside the native API |

Seven resume unit cases cover stale data, explicit beginning, explicit positions, backward progress, cleared progress, failed refresh and a new book. Both readers share this helper.

Five EPUB fixtures were opened three times cold and warm: 30 opens. All original performance gates passed. The large omnibus median was 214.8 ms cold and 209.2 ms warm, versus its original 20,173 ms cold baseline (98.94% reduction).

Real 309-page and 1,064-page CBRs opened in 503 ms and 898 ms. Sampled beginning, adjacent, distant and final pages decoded without blank pages or whole-archive downloads. At most 9 and 10 image URLs were retained; all were released on close. These are local-server observations, not a claim of improvement over the preceding custom plugin or a tablet-network guarantee.

An illustrated EPUB was checked at 390 by 844 pixels, device scale factor 3. Both its first and next page fit the viewport and navigation changed the image.

Raw runs, screenshots, private media paths and isolated test credentials stay outside public repositories. Re-run browser checks on a separate instance whenever reader rendering, storage, dependencies or resume behavior changes.

The deployment and rollback rehearsal passed: local web settings and installer extras were preserved, obsolete web files were removed from the candidate, the old plugin was retired/restored correctly, and rollback preserved newer reading progress. Production was switched to the verified release with a complete offline data backup. The running version and all 2,809 managed file hashes match.

The separate current-master resume branch passed 169 web tests, its seven focused regression cases, types and changed-file lint. The current-master server proposal passed 147 API tests and the .NET 10 analyzer build. These are prepared contribution branches, not accepted upstream changes.

On 2026-09-05 the resume contribution was rebased to current master and again passed 169 tests, types and changed-file lint before [PR #8421](https://github.com/jellyfin/jellyfin-web/pull/8421) was opened. The comic work was published as [Meta proposal #149](https://github.com/jellyfin/jellyfin-meta/discussions/149).
