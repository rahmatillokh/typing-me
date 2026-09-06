#!/usr/bin/env bash
# Publishes the WebGL build to GitHub Pages: copies Builds.noindex/WebGL onto the gh-pages
# branch (created on first run) and force-pushes it. The page is then served at
#   https://rahmatillokh.github.io/typing-me/
# Run from anywhere inside the repo after "Typing Me → Build → WebGL".
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

SRC="Builds.noindex/WebGL"
if [ ! -f "$SRC/index.html" ]; then
  echo "No WebGL build at $SRC — run Typing Me → Build → WebGL first." >&2
  exit 1
fi

WORKTREE="$(mktemp -d)"

if git show-ref --quiet refs/heads/gh-pages; then
  git worktree add --quiet "$WORKTREE" gh-pages
else
  # First deploy: an orphan branch with no history from main.
  git worktree add --quiet --detach "$WORKTREE"
  (cd "$WORKTREE" && git checkout --quiet --orphan gh-pages && git rm -rfq . 2>/dev/null || true)
fi

rsync -a --delete --exclude .git "$SRC"/ "$WORKTREE"/

# GitHub Pages runs Jekyll by default, which drops files it doesn't like; this opts out.
touch "$WORKTREE/.nojekyll"

(
  cd "$WORKTREE"
  git add -A
  git commit --quiet -m "Deploy WebGL build $(date -u +%Y-%m-%d)"
  git push --force --quiet origin gh-pages
)

git worktree remove --force "$WORKTREE"

echo "Published → https://rahmatillokh.github.io/typing-me/"
