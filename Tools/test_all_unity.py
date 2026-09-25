"""Runs every protected Unity QA suite in order and prints a summary (Unity open, outside Play, lock taken).

Usage: python Tools/test_all_unity.py [suite ...]   (default: all, in the order below)
Each suite keeps its own save protection; this script only sequences them and collects the exit codes."""
import subprocess
import sys
import time
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
SUITES = ['controls', 'modules', 'interface', 'lighting', 'hd', 'duel']


def main():
    wanted = sys.argv[1:] or SUITES
    unknown = [s for s in wanted if s not in SUITES]
    if unknown:
        raise SystemExit(f'Unknown suites: {unknown}; choose from {SUITES}')
    results = []
    for suite in wanted:
        start = time.monotonic()
        print(f'=== {suite} ===', flush=True)
        code = subprocess.run([sys.executable, str(TOOLS / f'test_{suite}_unity.py')]).returncode
        results.append((suite, code, time.monotonic() - start))
    print('\nResumen:', flush=True)
    for suite, code, seconds in results:
        print(f'  {suite:<10} {"PASA" if code == 0 else "FALLA"}  ({seconds:.0f} s)', flush=True)
    sys.exit(0 if all(code == 0 for _, code, _ in results) else 1)


if __name__ == '__main__':
    main()
