#!/usr/bin/env bash
# Discover (and optionally sweep) long, low-value comments across the repo.
#
# Categories:
#   Banner            // ---- or // ==== separator lines (zero content, always safe to strip)
#   VerboseSummary    /// <summary> ... </summary> blocks with >= MIN_LINES lines
#   VerboseLineBlock  consecutive non-doc // comment lines with >= MIN_LINES lines
#   VerboseXmlComment <!-- ... --> in *.csproj/*.props/*.targets, multi-line or > MAX_LINE_LENGTH chars
#
# Usage:
#   eng/scripts/find-verbose-comments.sh [--path DIR] [--min-lines N] [--max-line-length N] [--sweep] [--json]
#
# Default mode only reports candidates — nothing is deleted. --sweep removes ONLY the
# Banner category (pure decoration, no information ever lives there); VerboseSummary,
# VerboseLineBlock and VerboseXmlComment always require manual review, since only a
# human/reviewer reading the text can tell WHAT-restating filler apart from load-bearing
# WHY context (see git log --grep=comment for this repo's prior manual cleanup rounds).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SEARCH_PATH="$REPO_ROOT"
MIN_LINES=5
MAX_LINE_LENGTH=150
SWEEP=0
JSON=0

while [[ $# -gt 0 ]]; do
    case "$1" in
        --path) SEARCH_PATH="$2"; shift 2 ;;
        --min-lines) MIN_LINES="$2"; shift 2 ;;
        --max-line-length) MAX_LINE_LENGTH="$2"; shift 2 ;;
        --sweep) SWEEP=1; shift ;;
        --json) JSON=1; shift ;;
        -h|--help) grep '^#' "$0" | sed 's/^#//'; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

WHY_HINTS='GHSA|ADR|workaround|quirk|bug|vulnerab|issue #|CVE-'

EXCLUDE_ARGS=(
    -path '*/bin/*' -o -path '*/obj/*' -o -path '*/TestResults/*' -o
    -path '*/artifacts/*' -o -path '*/.git/*' -o -path '*/.vs/*' -o
    -path '*/.idea/*' -o -path '*/node_modules/*'
)

cs_files() {
    find "$SEARCH_PATH" \( "${EXCLUDE_ARGS[@]}" \) -prune -o -type f -name '*.cs' -print
}

xml_project_files() {
    find "$SEARCH_PATH" \( "${EXCLUDE_ARGS[@]}" \) -prune -o -type f \
        \( -name '*.csproj' -o -name '*.props' -o -name '*.targets' \) -print
}

RESULTS_FILE="$(mktemp)"
trap 'rm -f "$RESULTS_FILE"' EXIT

# --- .cs scanning: banners, verbose XML doc summaries, verbose // blocks ---
scan_cs_file() {
    local file="$1"
    awk -v minlines="$MIN_LINES" -v fname="$file" '
        function flush_line_block() {
            if (lb_start > 0 && (NR - lb_start) >= minlines) {
                printf "VerboseLineBlock|%s|%d|%d|%d|%s\n", fname, lb_start, NR - 1, (NR - lb_start), substr(lb_preview, 1, 100) >> "'"$RESULTS_FILE"'"
            }
            lb_start = 0
            lb_preview = ""
        }
        /^[ \t]*\/\/[ \t]*[-=][-=][-=][-=][-=][-=][-=][-=][-=][-=]+[ \t]*$/ {
            printf "Banner|%s|%d|%d|1|%s\n", fname, NR, NR, $0 >> "'"$RESULTS_FILE"'"
        }
        /^[ \t]*\/\/\/[ \t]*<summary>[ \t]*$/ {
            sum_start = NR
            sum_preview = ""
            next
        }
        sum_start > 0 && /^[ \t]*\/\/\/[ \t]*<\/summary>[ \t]*$/ {
            count = NR - sum_start + 1
            if (count >= minlines) {
                printf "VerboseSummary|%s|%d|%d|%d|%s\n", fname, sum_start, NR, count, substr(sum_preview, 1, 100) >> "'"$RESULTS_FILE"'"
            }
            sum_start = 0
            next
        }
        sum_start > 0 {
            line = $0
            sub(/^[ \t]*\/\/\/[ \t]?/, "", line)
            sum_preview = sum_preview " " line
            next
        }
        /^[ \t]*\/\/\// { flush_line_block(); next }
        /^[ \t]*\/\/[^\/]/ || /^[ \t]*\/\/$/ {
            if (lb_start == 0) { lb_start = NR }
            line = $0
            sub(/^[ \t]*\/\/[ \t]?/, "", line)
            lb_preview = lb_preview " " line
            next
        }
        { flush_line_block() }
        END { flush_line_block() }
    ' "$file"
}

# --- csproj/props/targets scanning: verbose <!-- --> comments ---
# One comment per line is assumed (true for every file in this repo); a line that both
# opens and closes a comment is handled, multiple separate comments packed onto one
# physical line are not (does not occur in hand-written MSBuild XML here).
scan_xml_file() {
    local file="$1"
    awk -v minlines="$MIN_LINES" -v maxlen="$MAX_LINE_LENGTH" -v fname="$file" '
        function emit(startline, endline, cnt, body) {
            gsub(/^[ \t]+|[ \t]+$/, "", body)
            gsub(/[ \t]+/, " ", body)
            if (cnt >= minlines || (cnt == 1 && length(body) > maxlen)) {
                printf "VerboseXmlComment|%s|%d|%d|%d|%s\n", fname, startline, endline, cnt, substr(body, 1, 100) >> "'"$RESULTS_FILE"'"
            }
        }
        {
            line = $0
            if (in_comment == 0) {
                open_idx = index(line, "<!--")
                if (open_idx == 0) { next }
                rest = substr(line, open_idx + 4)
                close_idx = index(rest, "-->")
                if (close_idx > 0) {
                    emit(NR, NR, 1, substr(rest, 1, close_idx - 1))
                } else {
                    in_comment = 1
                    c_start = NR
                    c_preview = rest
                }
            } else {
                close_idx = index(line, "-->")
                if (close_idx > 0) {
                    c_preview = c_preview " " substr(line, 1, close_idx - 1)
                    emit(c_start, NR, NR - c_start + 1, c_preview)
                    in_comment = 0
                    c_preview = ""
                } else {
                    c_preview = c_preview " " line
                }
            }
        }
    ' "$file"
}

while IFS= read -r f; do scan_cs_file "$f"; done < <(cs_files)
while IFS= read -r f; do scan_xml_file "$f"; done < <(xml_project_files)

sort -t'|' -k2,2 -k3,3n "$RESULTS_FILE" -o "$RESULTS_FILE"

if [[ $SWEEP -eq 1 ]]; then
    # Only Banner is auto-swept: pure decoration, always zero-content by construction.
    grep '^Banner|' "$RESULTS_FILE" | sort -t'|' -k2,2 -k3,3rn | while IFS='|' read -r _cat file start _end _cnt _preview; do
        sed -i.bak "${start}d" "$file" && rm -f "${file}.bak"
        echo "==> Swept banner line $start from ${file#"$REPO_ROOT"/}"
    done
    remaining=$(grep -vc '^Banner|' "$RESULTS_FILE" || true)
    echo ""
    echo "Auto-swept only the 'Banner' category (zero-content ASCII separators)."
    echo "$remaining remaining finding(s) need manual review — re-run without --sweep to list them."
    exit 0
fi

count=$(wc -l < "$RESULTS_FILE" | tr -d ' ')

if [[ "$count" -eq 0 ]]; then
    echo "No verbose/low-value comments found (min-lines=$MIN_LINES, max-line-length=$MAX_LINE_LENGTH)."
    exit 0
fi

if [[ $JSON -eq 1 ]]; then
    echo "["
    first=1
    while IFS='|' read -r cat file start end cnt preview; do
        [[ $first -eq 0 ]] && echo ","
        first=0
        esc_preview=$(printf '%s' "$preview" | sed 's/\\/\\\\/g; s/"/\\"/g')
        printf '  {"category":"%s","file":"%s","startLine":%s,"endLine":%s,"lineCount":%s,"preview":"%s"}' \
            "$cat" "${file#"$REPO_ROOT"/}" "$start" "$end" "$cnt" "$esc_preview"
    done < "$RESULTS_FILE"
    echo ""
    echo "]"
    exit 0
fi

files_count=$(cut -d'|' -f2 "$RESULTS_FILE" | sort -u | wc -l | tr -d ' ')
echo "Found $count candidate(s) across $files_count file(s):"
echo ""
{
    echo "CATEGORY|FILE|LINES|#|WHY?|PREVIEW"
    while IFS='|' read -r cat file start end cnt preview; do
        why=""
        if printf '%s' "$preview" | grep -qE "$WHY_HINTS"; then why="yes"; fi
        printf '%s|%s|%s-%s|%s|%s|%s\n' "$cat" "${file#"$REPO_ROOT"/}" "$start" "$end" "$cnt" "$why" "$preview"
    done < "$RESULTS_FILE"
} | column -t -s'|'

why_count=$(awk -F'|' -v hints="$WHY_HINTS" '$0 ~ hints' "$RESULTS_FILE" | wc -l | tr -d ' ')
if [[ "$why_count" -gt 0 ]]; then
    echo ""
    echo "$why_count finding(s) contain a WHY hint (GHSA/ADR/workaround/quirk/bug/vulnerability) — read before trimming, these are the kind of comment PRESERVED in prior cleanup rounds."
fi
