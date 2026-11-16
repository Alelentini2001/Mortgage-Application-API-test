#!/usr/bin/env bash
# PreToolUse (Bash): block destructive or verification-skipping commands.
# Exit 2 blocks the command; stderr is shown to Claude as the reason.
#
# This is a best-effort speed bump, not a sandbox: no text parser can catch every
# shell trick. Permission rules in .claude/settings.json are the second layer.
# Regression cases: .claude/hooks/tests/test-hooks.sh
set -euo pipefail

command -v python3 >/dev/null || { echo "block-dangerous-bash: python3 is required" >&2; exit 2; }

PROTECTED_REGEX="$(cat "$(dirname "$0")/protected-paths.regex")" ||
  { echo "block-dangerous-bash: protected-paths.regex missing" >&2; exit 2; }
export PROTECTED_REGEX

exec python3 -I -c '
import json, os, re, shlex, sys

def block(reason):
    print(f"Blocked by block-dangerous-bash.sh: {reason}", file=sys.stderr)
    sys.exit(2)

try:
    cmd = (json.load(sys.stdin).get("tool_input") or {}).get("command") or ""
except Exception:
    block("could not parse hook input")

SEPARATORS = {";", "&&", "||", "|", "&", "|&", "(", ")", "{", "}", ";;", "\n"}
REDIRECTS = {">", ">>", ">|", "&>", "&>>"}
WRAPPERS = {"sudo", "command", "builtin", "exec", "nohup", "time", "nice", "timeout",
            "stdbuf", "xargs", "noglob", "env", "eval"}
WRAPPER_ARG_FLAGS = {"-n", "-I", "-L", "-P", "-d", "-E", "-s", "-u", "-g", "-C", "-k", "-o", "-e", "-i"}
DOWNLOADERS = r"(curl|wget)"
INTERPRETERS = r"((ba|z|da|k)?sh|python[0-9.]*|node|perl|ruby)"
# Shared with protect-files.sh: one list of protected paths, plus .claude/hooks/** and .claude/settings*.json
# for shell writes (P8-D6 A, H1 review R4; Edit/Write still use the ask rule in .claude/settings.json).
PROTECTED = re.compile(os.environ["PROTECTED_REGEX"] + r"|(^|/)\.claude/(hooks/|settings[^/]*\.json$)")

def check_whole(text):
    if re.search(DOWNLOADERS + r"\b.*\|\s*(sudo\s+)?(env\s+)?" + INTERPRETERS + r"\b", text):
        block("piping a download into an interpreter is not allowed")
    if re.search(r"<\(\s*" + DOWNLOADERS + r"\b", text):
        block("executing a download via process substitution is not allowed")
    if re.search(r"\b(eval|(ba|z)?sh\s+-c)\s+[\"\x27]?\$\(\s*" + DOWNLOADERS + r"\b", text):
        block("executing a download via command substitution is not allowed")

def tokenize(text):
    try:
        lexer = shlex.shlex(text, posix=True, punctuation_chars=";&|()<>")
        lexer.whitespace_split = True
        lexer.commenters = ""
        return list(lexer)
    except ValueError:
        # Unbalanced quotes (e.g. heredoc text): fall back to a plain split.
        return re.sub(r"([;&|(){}<>]+)", r" \1 ", text).split()

def segments(tokens):
    current = []
    for tok in tokens:
        if tok in SEPARATORS:
            if current:
                yield current
            current = []
        else:
            current.append(tok)
    if current:
        yield current

def strip_prefix(words):
    i = 0
    while i < len(words):
        w = words[i]
        if re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", w):
            i += 1
            continue
        name = os.path.basename(w)
        if name in WRAPPERS:
            i += 1
            while i < len(words) and (words[i].startswith("-") or re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", words[i])):
                i += 2 if words[i] in WRAPPER_ARG_FLAGS else 1
            if name == "timeout" and i < len(words) and re.match(r"^[0-9.]+[smhd]?$", words[i]):
                i += 1
            continue
        break
    return words[i:]

def is_prefix(arg, option, minimum):
    return len(arg) >= minimum and option.startswith(arg)

def check_git(args):
    i = 0
    while i < len(args) and args[i].startswith("-"):
        opt = args[i]
        if opt in ("-c", "-C") and i + 1 < len(args):
            if opt == "-c" and "hookspath" in args[i + 1].lower():
                block("overriding core.hooksPath disables the gitleaks pre-commit hook")
            i += 2
        else:
            if "hookspath" in opt.lower():
                block("overriding core.hooksPath disables the gitleaks pre-commit hook")
            i += 1
    if i >= len(args):
        return
    sub, rest = args[i], args[i + 1:]

    # Drop the values of message/file options so their text never matches a rule.
    cleaned, skip = [], False
    for a in rest:
        if skip:
            skip = False
            continue
        if a in ("-m", "-F", "--message", "--file"):
            skip = True
            continue
        if a.startswith(("--message=", "--file=")) or (a.startswith("-m") and sub == "commit" and len(a) > 2 and not a.startswith("--")):
            continue
        cleaned.append(a)

    for a in cleaned:
        if is_prefix(a, "--no-verify", 6):
            block("--no-verify skips the safety hooks")

    if sub == "config":
        if any("hookspath" in a.lower() for a in cleaned) and cleaned[-1:] != [".githooks"]:
            block("core.hooksPath may only point at .githooks")
    elif sub == "commit":
        for a in cleaned:
            if re.match(r"^-[a-zA-Z]*n[a-zA-Z]*$", a):
                block("git commit -n skips the gitleaks pre-commit hook (same as --no-verify)")
    elif sub == "push":
        for a in cleaned:
            if a.startswith("--force-with-lease") or a.startswith("--force-if-includes"):
                continue
            if a in ("--mirror",) or is_prefix(a, "--force", 4) or a.startswith("+") or re.match(r"^-[a-zA-Z]*f[a-zA-Z]*$", a):
                block("force push is not allowed (--force-with-lease only with Alessandro\x27s approval)")
    elif sub == "reset":
        if any(is_prefix(a, "--hard", 4) for a in cleaned):
            block("git reset --hard discards work")
    elif sub in ("switch", "checkout"):
        if any(a in ("-f", "--force") or is_prefix(a, "--discard-changes", 5) for a in cleaned):
            block(f"git {sub} --force discards local changes")
    elif sub == "branch":
        short = "".join(a[1:] for a in cleaned if re.match(r"^-[a-zA-Z]+$", a))
        delete = "d" in short or "D" in short or "--delete" in cleaned
        force = "D" in short or "f" in short or "--force" in cleaned
        if delete and force:
            block("force-deleting a branch can lose unmerged work")
    elif sub == "worktree":
        if "remove" in cleaned and any(a in ("-f", "--force") for a in cleaned):
            block("git worktree remove --force can lose uncommitted work")
    elif sub == "clean":
        if any(re.match(r"^-[a-zA-Z]*f[a-zA-Z]*$", a) or a == "--force" for a in cleaned):
            block("git clean -f deletes untracked files")

def check_rm(args):
    recursive = force = False
    for a in args:
        if a == "--":
            break
        if a == "--recursive":
            recursive = True
        elif a == "--force":
            force = True
        elif re.match(r"^-[a-zA-Z]+$", a):
            recursive |= ("r" in a or "R" in a)
            force |= "f" in a
    if recursive and force:
        block("rm -rf is not allowed; delete specific paths or use \x27dotnet clean\x27")

def check_writes(words, prog, args):
    targets = []
    for i, w in enumerate(words):
        if w in REDIRECTS and i + 1 < len(words):
            targets.append(words[i + 1])
    if prog == "tee":
        targets += [a for a in args if not a.startswith("-")]
    if prog == "sed" and any(a.startswith("-i") or a == "--in-place" for a in args):
        targets += [a for a in args[1:] if not a.startswith("-")]
    if prog in ("cp", "mv", "ln") and args:
        targets.append(args[-1])
    for t in targets:
        if PROTECTED.search(t):
            block(f"writing to a protected path via the shell is not allowed ({t})")
    if re.fullmatch(r"python[0-9.]*|node|perl|ruby", prog):
        joined = " ".join(args)
        if re.search(r"(^|[\s\"\x27/])\.claude/(hooks/|settings[^/\s\"\x27]*\.json)", joined):
            block("accessing .claude/hooks/ or .claude/settings*.json via an inline interpreter is not allowed")

def check(text, depth=0):
    if depth > 3:
        block("command nesting too deep to check")
    check_whole(text)
    for seg in segments(tokenize(text)):
        words = strip_prefix(seg)
        if not words:
            continue
        prog = os.path.basename(words[0])
        args = words[1:]
        check_writes(words, prog, args)
        if re.fullmatch(r"(ba|z|da|k)?sh", prog):
            # -c may be combined with other flags (bash -lc, sh -ec).
            for idx, a in enumerate(args):
                if re.match(r"^-[a-zA-Z]*c[a-zA-Z]*$", a) and idx + 1 < len(args):
                    check(args[idx + 1], depth + 1)
                    break
        elif prog == "git":
            check_git(args)
        elif prog == "rm":
            check_rm(args)
        elif prog == "find" and ("-delete" in args or any(a == "rm" for a in args)):
            block("find -delete / -exec rm is not allowed; delete specific paths")
    # eval and wrappers were stripped; also re-check eval payloads as a whole.
    for m in re.finditer(r"\beval\s+([\"\x27])(.*?)\1", text):
        check(m.group(2), depth + 1)

check(cmd)
sys.exit(0)
'
