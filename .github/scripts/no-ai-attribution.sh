#!/usr/bin/env bash
# Commits and PR descriptions carry no AI attribution (CLAUDE.md, Project conventions): no co-author line, session link
# or "Generated with" line for an AI tool. Reads the text to check on stdin; prints each line that breaks the rule, and
# fails when there is one. A steering line alone wasn't enough: a harness reminder in the same context asked for them
# (step 123's retro).
#   git log --format=%B origin/main..HEAD | .github/scripts/no-ai-attribution.sh
set -euo pipefail

pattern='Co-Authored-By:.*(Claude|Anthropic|noreply@anthropic\.com)|Claude-Session:|claude\.ai/code/session|Generated with \[?Claude Code'

if found=$(grep -inE "$pattern"); then
  echo "AI attribution found; CLAUDE.md asks for none in commits and PR descriptions:" >&2
  echo "$found" >&2
  exit 1
fi
echo "No AI attribution."
