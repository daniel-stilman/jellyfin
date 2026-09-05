# Reader fork

This is a full Jellyfin fork. The `custom/10.11.11-readers` branch and companion [web fork](https://github.com/daniel-stilman/jellyfin-web/tree/custom/10.11.11-readers) form one tested release based on official v10.11.11.

The native comic API replaces the earlier Custom Comic Pages plugin. It authenticates requests, resolves items for the current user, sorts archive pages naturally, and extracts only requested pages. Archive indexes survive restarts; extracted images use a bounded disk cache. Bookshelf remains supported.

The web fork retains early EPUB rendering, background location generation, persistent location caching, illustrated-book layout and navigation fixes, and bounded comic image caching. Its additional resume fix reads current saved progress before resuming a cached item. Missed WebSocket notifications can no longer cause an old nonzero resume value to overwrite newer progress. Explicit starts and deliberate backward reading remain supported.

See [build, installation and rollback](custom-build/README.md) and [validation evidence](custom-build/VALIDATION.md).

Make future work, including themes, on feature branches. Keep upstream upgrades separate from reader changes and test both repositories before deploying. A fork preserves the history; upgrades still require conflict resolution and testing. An official installer can replace custom files, so verify the release after repairs or updates.

Upstream contributions use focused branches based on current upstream `master`. The independent resume fix can be reviewed separately. The cross-project comic page API needs a Jellyfin Meta discussion before a large PR. Follow the [development process](https://jellyfin.org/docs/general/contributing/development/) and [LLM policy](https://jellyfin.org/docs/general/contributing/llm-policies/): understand the code and write your own upstream descriptions, issues and comments.
