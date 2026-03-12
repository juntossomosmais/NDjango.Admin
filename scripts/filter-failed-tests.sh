#!/bin/bash
# Usage:
#   dotnet test | bash filter-failed-tests.sh
#   bash filter-failed-tests.sh < test-output.txt

awk '
BEGIN {
    printing = 0
    fail_count = 0
    test_summary = ""
    total_failed = 0
    total_passed = 0
    total_skipped = 0
    total_total = 0
    project_count = 0
}

/^Test summary:/ { test_summary = $0; next }

/^(Passed!|Failed!)/ {
    line = $0
    gsub(/,/, "", line)
    n = split(line, fields)
    for (i = 1; i <= n; i++) {
        if (fields[i] == "Failed:") total_failed += fields[i+1]+0
        else if (fields[i] == "Passed:") total_passed += fields[i+1]+0
        else if (fields[i] == "Skipped:") total_skipped += fields[i+1]+0
        else if (fields[i] == "Total:") total_total += fields[i+1]+0
    }
    project_count++
    next
}

/^  Failed .+\[[0-9]/ {
    if (fail_count > 0) print ""
    print "------------------------------------------------------------"
    fail_count++
    printf "[FAILED #%d] %s\n", fail_count, $0
    printing = 1
    next
}

printing && /^  Passed /          { printing = 0; next }
printing && /^Results File:/      { printing = 0; next }
printing && /^Test Run /          { printing = 0; next }
printing && /^Total tests:/       { printing = 0; next }
printing && /^ {4,}Passed: /      { printing = 0; next }
printing && /^ {4,}Failed: /      { printing = 0; next }
printing && /^ {4,}Total time: /  { printing = 0; next }
printing && /^\[xUnit\.net/       { printing = 0; next }

printing { print }

END {
    print ""
    print "------------------------------------------------------------"
    if (test_summary != "") {
        print test_summary
    } else if (project_count > 0) {
        if (total_failed > 0) status = "Failed!"
        else status = "Passed!"
        printf "%s - Failed: %5d, Passed: %5d, Skipped: %5d, Total: %5d\n", status, total_failed, total_passed, total_skipped, total_total
    }
    if (fail_count == 0) {
        exit 0
    } else {
        printf "FAILED: %d test(s)\n", fail_count
        exit 1
    }
}
' "$@"
