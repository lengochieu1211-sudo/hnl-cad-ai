# Dependency build note — v2.0.0

Direct dependency versions in `package.json` are pinned (no `^`/`~`) to reduce build drift.

The repository still uses `npm install` in GitHub Actions because a complete `package-lock.json`
must be generated in a network-enabled Node environment. After the first successful Windows build,
commit the generated lockfile and change the workflow to `npm ci`.

Do not fabricate a lockfile manually.
