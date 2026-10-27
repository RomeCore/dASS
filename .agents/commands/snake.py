"""
name: snake
title: Snake
description: Runs the ANSI snake game in an interactive terminal attached to this message and waits for it to exit. Arrows or WASD turn, P pauses, R restarts, Q quits. The top 5 runs per mode survive restarts - the optional 'wrap' argument makes walls passable and is scored on its own board.
category: Chat
run-terminal: true
generate: false
argument-schema: '[mode:$classic|wrap]'
"""

#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ANSI Snake for the dASS embedded terminal.

Controls:
    arrows / WASD / HJKL  - turn
    P                     - pause
    R                     - restart
    Q                     - quit

Usage:
    /snake          - classic: the walls kill
    /snake wrap     - the snake leaves one edge and enters the opposite one

No curses, no third-party deps: ANSI true/false-color rendering + msvcrt
non-blocking keyboard polling, so the game loop keeps ticking.
"""

import os
import re
import sys
import time
import json
import random
import ctypes
import datetime

try:
    import msvcrt
    HAS_MSVCRT = True
except ImportError:
    HAS_MSVCRT = False

RESET = "\x1b[0m"
BOLD = "\x1b[1m"

WIDTH, HEIGHT = 34, 20

# The frame plus its message block has to stay under the terminal height. A taller frame makes
# the terminal scroll, and \x1b[H then redraws into shifted content - frames stack instead of
# replacing each other. So nothing below the frame is padded: the block is exactly as tall as it
# needs to be, and \x1b[J erases whatever a taller previous frame left behind.
FRAME_WIDTH = 2 + 1 + WIDTH * 2 + 1
MESSAGE_WIDTH = FRAME_WIDTH - 2

# The speed curve is linear: START_INTERVAL at score 0, MIN_INTERVAL at FULL_SPEED_SCORE
# and flat afterwards. Stretching the ramp over 40 points keeps the late game readable
# instead of pinning the snake to terminal velocity at 21.
START_INTERVAL = 0.13
MIN_INTERVAL = 0.055
FULL_SPEED_SCORE = 40
SPEED_STEP = (START_INTERVAL - MIN_INTERVAL) / FULL_SPEED_SCORE

MODE_CLASSIC = "classic"
MODE_WRAP = "wrap"

# The top 5 per mode, with dates. The board cannot live next to this script: the command
# runs a temporary copy that is deleted afterwards, so it goes to the .llmassist of the
# chat's working directory instead - which means the record follows the directory.
BOARD_SIZE = 5
BOARD_FILE = os.path.join(os.getcwd(), ".llmassist", "snake_scores.json")
LEGACY_FILE = os.path.join(os.getcwd(), ".llmassist", "snake_best.txt")

UP, DOWN, LEFT, RIGHT = (0, -1), (0, 1), (-1, 0), (1, 0)

KEYMAP = {
    "w": UP, "i": UP,
    "s": DOWN, "k": DOWN,
    "a": LEFT, "j": LEFT,
    "d": RIGHT, "l": RIGHT,
}

ARROWS = {
    b"H": UP,
    b"P": DOWN,
    b"K": LEFT,
    b"M": RIGHT,
}


# --------------------------------------------------------------------------- #
#  platform glue
# --------------------------------------------------------------------------- #
def enable_ansi() -> None:
    """Windows legacy console does not process ANSI escapes unless told to."""
    if os.name != "nt":
        return
    try:
        from ctypes import wintypes
        kernel32 = ctypes.windll.kernel32
        handle = kernel32.GetStdHandle(-11)  # STD_OUTPUT_HANDLE
        mode = wintypes.DWORD()
        if kernel32.GetConsoleMode(handle, ctypes.byref(mode)):
            ENABLE_PROCESSED_OUTPUT = 0x0001
            ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004
            kernel32.SetConsoleMode(
                handle, mode.value | ENABLE_PROCESSED_OUTPUT | ENABLE_VIRTUAL_TERMINAL_PROCESSING
            )
    except Exception:
        pass


def poll_input():
    """Returns a list of ('dir', d) / ('char', c) events, never blocking."""
    events = []
    if HAS_MSVCRT:
        while msvcrt.kbhit():
            ch = msvcrt.getch()
            if ch in (b"\x00", b"\xe0"):
                if msvcrt.kbhit():
                    direction = ARROWS.get(msvcrt.getch())
                    if direction:
                        events.append(("dir", direction))
                continue
            try:
                text = ch.decode("utf-8", "ignore")
            except Exception:
                continue
            if not text:
                continue
            low = text.lower()
            if low in KEYMAP:
                events.append(("dir", KEYMAP[low]))
            else:
                events.append(("char", low))
    else:  # minimal POSIX fallback (unbuffered, non-blocking)
        import select
        try:
            while select.select([sys.stdin], [], [], 0)[0]:
                low = sys.stdin.read(1).lower()
                if low in KEYMAP:
                    events.append(("dir", KEYMAP[low]))
                else:
                    events.append(("char", low))
        except Exception:
            pass
    return events


# --------------------------------------------------------------------------- #
#  arguments
# --------------------------------------------------------------------------- #
def requested_mode():
    """Reads the optional 'classic' | 'wrap' positional - absent or unknown means classic."""
    try:
        args = command_args
    except NameError:
        return MODE_CLASSIC
    positionals = (args or {}).get("positionals") or []
    if not positionals:
        return MODE_CLASSIC
    return MODE_WRAP if str(positionals[0]).strip().lower() == MODE_WRAP else MODE_CLASSIC


# --------------------------------------------------------------------------- #
#  scores
# --------------------------------------------------------------------------- #
def parse_entry(raw):
    """One stored board row, or None when the record cannot be read."""
    if not isinstance(raw, dict):
        return None
    try:
        score = int(raw.get("score"))
    except (TypeError, ValueError):
        return None
    if score <= 0:
        return None
    date = raw.get("date")
    return {"score": score, "date": str(date) if date else ""}


def load_legacy_entry():
    """The single-score file used before the board existed, stamped with its own file date."""
    try:
        with open(LEGACY_FILE, "r", encoding="utf-8") as handle:
            score = int(handle.read().strip() or 0)
        if score <= 0:
            return None
        stamp = datetime.date.fromtimestamp(os.path.getmtime(LEGACY_FILE)).isoformat()
        return {"score": score, "date": stamp}
    except Exception:
        return None


def load_board():
    """Reads the per-mode top-5 board, seeding classic from the legacy file on the first run."""
    board = {MODE_CLASSIC: [], MODE_WRAP: []}

    raw = None
    try:
        with open(BOARD_FILE, "r", encoding="utf-8") as handle:
            raw = json.load(handle)
    except Exception:
        raw = None

    if isinstance(raw, dict):
        for mode in (MODE_CLASSIC, MODE_WRAP):
            stored = raw.get(mode)
            if not isinstance(stored, list):
                continue
            for candidate in stored:
                entry = parse_entry(candidate)
                if entry is not None:
                    board[mode].append(entry)
            board[mode].sort(key=lambda item: -item["score"])
            del board[mode][BOARD_SIZE:]
        return board

    legacy = load_legacy_entry()
    if legacy is not None:
        board[MODE_CLASSIC] = [legacy]
    return board


def save_board(board):
    """Writes the board, silently: a read-only working directory must not kill the game."""
    try:
        directory = os.path.dirname(BOARD_FILE)
        if directory:
            os.makedirs(directory, exist_ok=True)
        with open(BOARD_FILE, "w", encoding="utf-8") as handle:
            json.dump(board, handle, ensure_ascii=False, indent=2)
    except Exception:
        pass


def record_score(board, mode, score):
    """Puts a finished run on its mode's board; None when it scored nothing or missed the cut."""
    if score <= 0:
        return None
    entry = {"score": score, "date": datetime.date.today().isoformat()}
    entries = board.setdefault(mode, [])
    entries.append(entry)
    entries.sort(key=lambda item: -item["score"])
    del entries[BOARD_SIZE:]
    return entry if any(item is entry for item in entries) else None


def best_of(board, mode):
    """The highest score on one mode's board, 0 when it is still empty."""
    entries = board.get(mode) or []
    return entries[0]["score"] if entries else 0


def board_lines(board, mode, highlight=None):
    """The top-5 table for one mode, with the run that just ended tagged 'new'."""
    entries = board.get(mode) or []
    label = "wrap" if mode == MODE_WRAP else "classic"
    lines = ["\x1b[38;5;244mtop 5 - {label}{R}".format(label=label, R=RESET)]
    if not entries:
        lines.append("  \x1b[38;5;238m(no scores yet){R}".format(R=RESET))
        return lines
    for index, entry in enumerate(entries, start=1):
        tag = ""
        if highlight is not None and entry is highlight:
            tag = "  \x1b[1m\x1b[38;5;46mnew{R}".format(R=RESET)
        lines.append(
            "  \x1b[38;5;244m{n}.{R} \x1b[1m{score:>3}{R}  \x1b[38;5;238m{date}{R}{tag}".format(
                n=index, score=entry["score"], date=entry["date"] or "-", R=RESET, tag=tag
            )
        )
    return lines


def board_summary(board, mode):
    """The top-5 scores on one line - the in-place screen has no rows to spare for the table."""
    entries = board.get(mode) or []
    if not entries:
        return "top 5  -"
    return "top 5  " + ", ".join(str(entry["score"]) for entry in entries)


# --------------------------------------------------------------------------- #
#  game model
# --------------------------------------------------------------------------- #
class Snake:
    def __init__(self, wrap=False):
        cx, cy = WIDTH // 2, HEIGHT // 2
        self.body = [(cx, cy), (cx - 1, cy), (cx - 2, cy)]
        self.direction = RIGHT
        self.queue = []
        self.score = 0
        self.wrap = wrap

    def head(self):
        return self.body[0]

    def queue_turn(self, direction):
        last = self.queue[-1] if self.queue else self.direction
        if direction == last:
            return
        if (direction[0] + last[0], direction[1] + last[1]) == (0, 0):
            return  # no instant 180
        if len(self.queue) < 2:
            self.queue.append(direction)

    def step(self, food):
        if self.queue:
            self.direction = self.queue.pop(0)
        dx, dy = self.direction
        hx, hy = self.body[0]
        target = (hx + dx, hy + dy)

        if self.wrap:
            target = (target[0] % WIDTH, target[1] % HEIGHT)
        elif not (0 <= target[0] < WIDTH and 0 <= target[1] < HEIGHT):
            return "wall"
        # the tail cell is vacated this tick unless we are about to grow
        if target in self.body[:-1]:
            return "self"

        self.body.insert(0, target)
        if target == food:
            self.score += 1
            return "food"
        self.body.pop()
        return "ok"


def spawn_food(snake):
    taken = set(snake.body)
    free = [(x, y) for y in range(HEIGHT) for x in range(WIDTH) if (x, y) not in taken]
    return random.choice(free) if free else None


def step_interval_for(score):
    """Seconds per cell at this score - linear down to MIN_INTERVAL, flat after that."""
    return max(MIN_INTERVAL, START_INTERVAL - SPEED_STEP * score)


# --------------------------------------------------------------------------- #
#  text fitting
# --------------------------------------------------------------------------- #
ANSI_RE = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")


def visible_width(text):
    """How many columns a string really occupies - escape sequences take none."""
    return len(ANSI_RE.sub("", text))


def fit(text, limit):
    """Cuts a line to the visible limit, ANSI-aware. A wrapped line costs an extra row, and an
    extra row is exactly what turns an in-place redraw into a scrolling one."""
    if visible_width(text) <= limit:
        return text
    out = []
    visible = 0
    index = 0
    while index < len(text):
        match = ANSI_RE.match(text, index)
        if match is not None:
            out.append(match.group(0))
            index = match.end()
            continue
        if visible >= limit - 3:
            out.append("...")
            break
        out.append(text[index])
        visible += 1
        index += 1
    return "".join(out) + RESET


# --------------------------------------------------------------------------- #
#  rendering
# --------------------------------------------------------------------------- #
def render(snake, food, best, cells_per_second, message_lines, mode):
    body = snake.body
    head = body[0]
    total = max(1, len(body) - 1)

    cells = {}
    for index, point in enumerate(body[1:], start=1):
        t = index / total
        color = 22 + int(24 * (1.0 - t))  # bright tail -> deep green head side
        cells[point] = "\x1b[38;5;%dm██" % color

    if mode == MODE_WRAP:
        chip = "\x1b[1m\x1b[38;5;51mWRAP" + RESET
    else:
        chip = "\x1b[38;5;238mCLASSIC" + RESET

    out = ["\x1b[H"]
    out.append(
        "\x1b[K  {b}\x1b[38;5;214mSNAKE{R}  {chip}   \x1b[38;5;244mscore{R} {b}\x1b[38;5;46m{s}{R}"
        "   \x1b[38;5;244mbest{R} {b}\x1b[38;5;220m{b0}{R}"
        "   \x1b[38;5;244m{speed:.1f} cell/s{R}\n".format(
            b=BOLD, R=RESET, chip=chip, s=snake.score, b0=best, speed=cells_per_second
        )
    )

    corner = "\x1b[38;5;238m"
    out.append("  " + corner + "╔" + "══" * WIDTH + "╗" + RESET + "\x1b[K\n")

    for y in range(HEIGHT):
        row = ["  " + corner + "║" + RESET]
        for x in range(WIDTH):
            point = (x, y)
            if point == head:
                row.append(BOLD + "\x1b[38;5;51m██" + RESET)
            elif point in cells:
                row.append(cells[point])
            elif point == food:
                row.append(BOLD + "\x1b[38;5;196m██" + RESET)
            else:
                row.append("  ")
        row.append(corner + "║" + RESET + "\x1b[K")
        out.append("".join(row) + "\n")

    out.append("  " + corner + "╚" + "══" * WIDTH + "╝" + RESET + "\x1b[K\n")

    # Exactly the lines asked for, never padded: the frame plus this block is what has to fit.
    for line in message_lines:
        out.append("\x1b[K  " + fit(line, MESSAGE_WIDTH) + "\n")

    out.append(RESET + "\x1b[J")  # wipe whatever a taller previous frame left below

    sys.stdout.write("".join(out))
    sys.stdout.flush()


# --------------------------------------------------------------------------- #
#  main loop
# --------------------------------------------------------------------------- #
def play_session(board, mode):
    """One run of the game. Returns (action, entry): the action is 'quit' | 'restart', the entry
    is the board row this run produced (or None). The board itself is updated in place."""
    snake = Snake(wrap=(mode == MODE_WRAP))
    food = spawn_food(snake)
    step_interval = START_INTERVAL
    accumulator = 0.0
    paused = False
    started = False
    over = False
    over_reason = ""
    recorded = False
    new_entry = None
    dirty = True

    hint = "  \x1b[38;5;214mP{R} pause   \x1b[38;5;214mR{R} restart   \x1b[38;5;214mQ{R} quit".format(R=RESET)

    def best_now():
        return max(best_of(board, mode), snake.score)

    def messages():
        if over:
            is_top = new_entry is not None and bool(board.get(mode)) and board[mode][0] is new_entry
            rec = "\x1b[1m\x1b[38;5;46m   new record{R}".format(R=RESET) if is_top else ""
            # Three lines, not the whole table: the table would push the frame past the screen.
            return [
                "\x1b[1m\x1b[38;5;196mGAME OVER{R}  \x1b[38;5;250m{why}{R}".format(R=RESET, why=over_reason),
                "\x1b[38;5;250mscore \x1b[1m{s}{R}\x1b[38;5;250m   best \x1b[1m{b}{R}{rec}".format(
                    R=RESET, s=snake.score, b=best_now(), rec=rec),
                "\x1b[38;5;244m{board}{R}   \x1b[38;5;214mR{R} restart   \x1b[38;5;214mQ{R} quit".format(
                    board=board_summary(board, mode), R=RESET),
            ]
        if not started:
            return ["\x1b[38;5;46mpress an arrow / WASD to start{R}".format(R=RESET), hint]
        if paused:
            return ["\x1b[1m\x1b[38;5;220mPAUSED{R}   \x1b[38;5;244mpress P to continue{R}".format(R=RESET), hint]
        return [hint, ""]

    def end_run():
        """Records the run once - a quit after a death must not put the same score up twice."""
        nonlocal recorded, new_entry
        if not recorded:
            recorded = True
            new_entry = record_score(board, mode, snake.score)

    last_time = time.perf_counter()

    while True:
        now = time.perf_counter()
        dt = min(now - last_time, 0.25)
        last_time = now

        for kind, value in poll_input():
            if kind == "char":
                if value in ("q", "\x03"):
                    end_run()
                    return "quit", new_entry
                if value == "r":
                    end_run()
                    return "restart", new_entry
                if value == "p" and not over:
                    paused = not paused
                    dirty = True
                continue
            if over or paused:
                continue
            started = True
            snake.queue_turn(value)
            dirty = True

        if started and not paused and not over:
            accumulator += dt
            while accumulator >= step_interval:
                accumulator -= step_interval
                result = snake.step(food)
                dirty = True
                if result == "wall":
                    over, over_reason = True, "snake hit the wall"
                    end_run()
                    break
                if result == "self":
                    over, over_reason = True, "snake bit itself"
                    end_run()
                    break
                if result == "food":
                    food = spawn_food(snake)
                    step_interval = step_interval_for(snake.score)
                    if food is None:
                        over, over_reason = True, "field cleared, you monster"
                        end_run()
                        break

        if dirty:
            render(snake, food, best_now(), 1.0 / step_interval, messages(), mode)
            dirty = False

        time.sleep(0.008)


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace", newline="")
    except Exception:
        pass

    enable_ansi()
    sys.stdout.write("\x1b[2J\x1b[?25l")  # clear screen + hide cursor
    sys.stdout.flush()

    board = load_board()
    mode = requested_mode()
    save_board(board)  # persist the legacy migration even if nobody plays

    last_mode = None
    last_entry = None
    try:
        while True:
            action, entry = play_session(board, mode)
            save_board(board)
            if entry is not None:
                last_mode, last_entry = mode, entry
            if action == "quit":
                break
    except KeyboardInterrupt:
        save_board(board)
    finally:
        sys.stdout.write("\x1b[0m\x1b[?25h\n")
        sys.stdout.flush()

    classic = best_of(board, MODE_CLASSIC)
    wrap = best_of(board, MODE_WRAP)
    report = [
        "\x1b[38;5;244mbye. best - classic \x1b[0m\x1b[1m%d\x1b[0m\x1b[38;5;244m   wrap \x1b[0m\x1b[1m%s\x1b[0m"
        % (classic, wrap if wrap else "-"),
    ]
    # The full table with dates lives here, in ordinary scrollback: the in-place screen has no
    # rows to spare for it.
    for book in (MODE_CLASSIC, MODE_WRAP):
        report.append("")
        report.extend(board_lines(board, book, last_entry if last_mode == book else None))
    sys.stdout.write("\n".join(report) + "\n")
    sys.stdout.flush()


# The body is embedded after the generated preamble and run as the entry script, so it is
# called directly instead of through the usual `if __name__ == "__main__"` guard.
main()
