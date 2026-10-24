"""
name: snake
title: Snake
description: Runs the ANSI snake game in an interactive terminal attached to this message and waits for it to exit. Arrows or WASD turn, P pauses, R restarts, Q quits; the best score survives restarts.
category: Chat
run-terminal: true
generate: false
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

No curses, no third-party deps: ANSI true/false-color rendering + msvcrt
non-blocking keyboard polling, so the game loop keeps ticking.
"""

import os
import sys
import time
import random
import ctypes

try:
    import msvcrt
    HAS_MSVCRT = True
except ImportError:
    HAS_MSVCRT = False

RESET = "\x1b[0m"
BOLD = "\x1b[1m"

WIDTH, HEIGHT = 34, 20

# The best score survives restarts and separate runs. It cannot live next to this file:
# the script is a temporary copy that is deleted when the command ends, so the record
# goes to .llmassist of the chat's working directory instead.
HIGHSCORE_FILE = os.path.join(os.getcwd(), ".llmassist", "snake_best.txt")
DEFAULT_BEST = 32  # record set on 2026-10-20


def load_best():
    try:
        with open(HIGHSCORE_FILE, "r", encoding="utf-8") as handle:
            return max(0, int(handle.read().strip() or 0))
    except Exception:
        return DEFAULT_BEST


def save_best(value):
    try:
        directory = os.path.dirname(HIGHSCORE_FILE)
        if directory:
            os.makedirs(directory, exist_ok=True)
        with open(HIGHSCORE_FILE, "w", encoding="utf-8") as handle:
            handle.write(str(value))
    except Exception:
        pass

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
#  game model
# --------------------------------------------------------------------------- #
class Snake:
    def __init__(self):
        cx, cy = WIDTH // 2, HEIGHT // 2
        self.body = [(cx, cy), (cx - 1, cy), (cx - 2, cy)]
        self.direction = RIGHT
        self.queue = []
        self.score = 0

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

        if not (0 <= target[0] < WIDTH and 0 <= target[1] < HEIGHT):
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


# --------------------------------------------------------------------------- #
#  rendering
# --------------------------------------------------------------------------- #
def render(snake, food, best, cells_per_second, message_lines):
    body = snake.body
    head = body[0]
    total = max(1, len(body) - 1)

    cells = {}
    for index, point in enumerate(body[1:], start=1):
        t = index / total
        color = 22 + int(24 * (1.0 - t))  # bright tail -> deep green head side
        cells[point] = "\x1b[38;5;%dm██" % color

    out = ["\x1b[H"]
    out.append(
        "\x1b[K  {b}\x1b[38;5;214mSNAKE{R}   \x1b[38;5;244mscore{R} {b}\x1b[38;5;46m{s}{R}"
        "   \x1b[38;5;244mbest{R} {b}\x1b[38;5;220m{b0}{R}"
        "   \x1b[38;5;244m{speed:4.1f} cell/s{R}\n".format(
            b=BOLD, R=RESET, s=snake.score, b0=best, speed=cells_per_second
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

    for i in range(2):
        line = message_lines[i] if i < len(message_lines) else ""
        out.append("\x1b[K  " + line + "\n")

    sys.stdout.write("".join(out))
    sys.stdout.flush()


# --------------------------------------------------------------------------- #
#  main loop
# --------------------------------------------------------------------------- #
def play_session(best):
    """One run of the game. Returns (best, action) with action 'quit' | 'restart'."""
    snake = Snake()
    food = spawn_food(snake)
    step_interval = 0.13
    accumulator = 0.0
    paused = False
    started = False
    over = False
    over_reason = ""
    dirty = True

    def messages():
        hint = "  \x1b[38;5;214mP{R} pause   \x1b[38;5;214mR{R} restart   \x1b[38;5;214mQ{R} quit".format(R=RESET)
        if over:
            return [
                "\x1b[1m\x1b[38;5;196mGAME OVER{R}  \x1b[38;5;250m{why}{R}".format(R=RESET, why=over_reason),
                "\x1b[38;5;250mscore \x1b[1m{s}{R}\x1b[38;5;250m   best \x1b[1m{b}{R}".format(
                    R=RESET, s=snake.score, b=best),
            ]
        if not started:
            return ["\x1b[38;5;46mpress an arrow / WASD to start{R}".format(R=RESET), hint]
        if paused:
            return ["\x1b[1m\x1b[38;5;220mPAUSED{R}   \x1b[38;5;244mpress P to continue{R}".format(R=RESET), hint]
        return [hint, ""]

    last_time = time.perf_counter()

    while True:
        now = time.perf_counter()
        dt = min(now - last_time, 0.25)
        last_time = now

        for kind, value in poll_input():
            if kind == "char":
                if value in ("q", "\x03"):
                    return best, "quit"
                if value == "r":
                    return best, "restart"
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
                    best = max(best, snake.score)
                    break
                if result == "self":
                    over, over_reason = True, "snake bit itself"
                    best = max(best, snake.score)
                    break
                if result == "food":
                    food = spawn_food(snake)
                    step_interval = max(0.055, 0.13 - 0.0035 * snake.score)
                    if food is None:
                        over, over_reason = True, "field cleared, you monster"
                        best = max(best, snake.score)
                        break

        if dirty:
            render(snake, food, best, 1.0 / step_interval, messages())
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

    best = load_best()
    try:
        while True:
            best, action = play_session(best)
            save_best(best)
            if action == "quit":
                break
    except KeyboardInterrupt:
        pass
    finally:
        sys.stdout.write("\x1b[0m\x1b[?25h\n")
        sys.stdout.flush()

    sys.stdout.write("\x1b[38;5;244mbye. best score: \x1b[0m\x1b[1m%d\x1b[0m\n" % best)
    sys.stdout.flush()


# The body is embedded after the generated preamble and run as the entry script, so it is
# called directly instead of through the usual `if __name__ == "__main__"` guard.
main()
