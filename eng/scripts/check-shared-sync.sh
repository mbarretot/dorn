#!/usr/bin/env bash
#
# eng/scripts/check-shared-sync.sh
#
# templates/shared/ is the canonical source for code that must also exist, byte-for-byte,
# as a physical copy inside templates/webapi/ (see docs/adr/0008-templates-shared-physical-copy-sync.md
# for why this is a plain file copy instead of a symlink or an MSBuild <Compile Include>
# reaching outside the template root: templates must stay self-contained to be packaged
# as NuGet template packages in the future, and `dotnet new`/NuGet packaging does not
# follow references outside a template's own directory).
#
# This script fails CI (non-zero exit) if any file under templates/shared/ has drifted
# from its corresponding copy under templates/webapi/. It does not modify anything.
#
# Usage: eng/scripts/check-shared-sync.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# "shared source dir|webapi copy dir" pairs kept in sync by convention. Every *.cs file
# found under the left-hand side must have a byte-identical copy at the same relative
# path under the right-hand side.
PAIRS=(
  "templates/shared/Domain|templates/webapi/src/CleanArchWebApi.Domain"
  "templates/shared/Application/Messaging|templates/webapi/src/CleanArchWebApi.Application/Messaging"
)

failed=0

for pair in "${PAIRS[@]}"; do
  shared_rel="${pair%%|*}"
  webapi_rel="${pair##*|}"
  shared_dir="$REPO_ROOT/$shared_rel"
  webapi_dir="$REPO_ROOT/$webapi_rel"

  if [[ ! -d "$shared_dir" ]]; then
    echo "ERROR: expected shared source directory '$shared_rel' does not exist." >&2
    failed=1
    continue
  fi

  if [[ ! -d "$webapi_dir" ]]; then
    echo "ERROR: expected webapi copy directory '$webapi_rel' does not exist." >&2
    failed=1
    continue
  fi

  while IFS= read -r -d '' shared_file; do
    rel_path="${shared_file#"$shared_dir"/}"
    webapi_file="$webapi_dir/$rel_path"

    if [[ ! -f "$webapi_file" ]]; then
      echo "MISSING COPY: '$webapi_rel/$rel_path' does not exist (source of truth: '$shared_rel/$rel_path')." >&2
      failed=1
      continue
    fi

    if ! diff -u "$webapi_file" "$shared_file" >/dev/null 2>&1; then
      echo "" >&2
      echo "DRIFT DETECTED: '$webapi_rel/$rel_path' differs from '$shared_rel/$rel_path':" >&2
      diff -u "$webapi_file" "$shared_file" >&2 || true
      failed=1
    fi
  done < <(find "$shared_dir" -type f -name '*.cs' -print0)
done

if [[ "$failed" -ne 0 ]]; then
  echo "" >&2
  echo "check-shared-sync: FAILED - templates/shared/ and templates/webapi/ have drifted." >&2
  echo "templates/shared/ is the source of truth; copy the changed file(s) into templates/webapi/" >&2
  echo "at the corresponding path. See docs/adr/0008-templates-shared-physical-copy-sync.md." >&2
  exit 1
fi

echo "check-shared-sync: OK - templates/shared/ and templates/webapi/ are in sync."
exit 0
