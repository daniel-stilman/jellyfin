# Mobile reader navigation — readers-r6

## Scope and cause

The reported Criminal: Coward volume is a 148-page PDF. Its PDF player was unchanged from official v10.11.11: it placed the page at the top, changed pages on touch-down and published canvases before rendering completed. The replacement uses centered Swiper slides with a small render cache: at most two simultaneous renders, a seven-page cache window, bounded canvas resolution and cancellation on close/resize. Only finished canvases enter the visible slides.

The comic archive player used Swiper already, but each prefetch callback forced a virtual-slide rebuild. Updates now modify only the affected image/status and preserve the displayed slide and its drag position. The native comic API, archive fallback, protected prefetch window, object-URL limit, zoom, right-to-left mode and double-page option remain.

EPUBs now use continuous pagination and an explicit touch/mouse drag controller. Horizontal movement follows the gesture; release snaps to the nearer page. Reversing, vertical gestures, pinches, touch cancellation, controls and synthetic mouse events are handled separately. Pages keep their aspect ratio; the illustrated page is centered within its iframe. Side and toolbar page-turn arrows were removed; edge taps and keyboard shortcuts remain.

Image-only EPUBs have no usable text-location index. Their progress and resume now use linear spine pages. Reflowable EPUBs retain asynchronous indexing and persistent caching, with cancellation and delayed-resume safeguards. The regression run also found and repaired nested EPUB navigation links: a chapter link in `Text/nav.html` must resolve relative to that document, not directly to the package directory.

## Validation

- 226 unit tests across 24 files passed. New coverage includes edge taps, drag reversal/cancellation, synthetic mouse events, RTL scroll coordinates, fixed-page progress, EPUB navigation paths, centering, bounded PDF rendering, cancellation and completed-canvas publication.
- TypeScript, changed-reader ESLint, changed stylesheet lint and the production web build passed. The final release also passes the ES5 bundle syntax check; it is built from a checkout with real dependency paths so all vendor transpilation rules apply. Webpack reports its existing two asset-size warnings.
- Browser matrices covered Coward PDF, Invincible CBR, a fixed-layout illustrated EPUB and the large reflowable omnibus. Checks include both directions of edge taps, short/reversed/canceled drags, portrait/landscape, zoom, comic RTL/double-page mode, final pages/distant jumps, EPUB chapter links/font/theme controls, saved positions/reopen and closing a pending download.
- A deliberately held comic page response was released during a drag. The visible slide kept the same DOM identity and translation. The browser archive fallback also passed paging after the API was made unavailable.
- The 309-page and 1,064-page comics opened in 396 ms and 181 ms in the final local-server sample. No whole-archive downloads occurred. At most 9 and 12 object URLs were retained in the sampled jumps; zero remained after close. These warm local observations check preservation of the earlier improvements, not a new universal speed claim.
- Native Android Jellyfin reproduced the original PDF alignment problem. Candidate PDF and both EPUB formats passed on-device Chrome gesture checks on the connected Samsung phone. PDF centering was checked numerically; reversal kept the original page. Further device checks were stopped when the owner confirmed another task was using the phone. The final native-app pass is therefore not claimed.
- PDF.js emitted a worker-termination rejection only in the deliberate close-during-blocked-download test. The reader stayed closed and did not save a new position. The harness records this expected cancellation separately from unexpected browser errors. Missing fixture-cover HTTP 404s are likewise distinct from reader errors.

All automated reading-progress writes used the isolated `reader-test` account and database on port 8097. Its server identity was checked against production. Library metadata writes were disabled. Raw screenshots, private paths and test credentials remain outside the public repositories.

## Release and rollback

The paired release uses the same server binaries as readers-r5. `Manage-WebReaderDeployment.ps1` refuses a web-only deployment if any managed server file differs. It backs up the current web tree and release marker, prepares and hashes the replacement, preserves local web preferences and older hashed chunks for already-open clients, and swaps the web directories. It does not modify the reading database. Rollback restores the previous web tree and marker.

A disposable deployment rehearsal verified prepare/install/rollback, preserved settings and older chunks, and unchanged reading-progress data. Copying now includes files whose sizes and timestamps match so release hashes, rather than copy heuristics, determine correctness.

This release does not change the submitted upstream resume/performance branches or the comic API proposal. Mobile presentation changes remain a separate customization.
