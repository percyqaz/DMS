# DMS
Esoteric programming language built for Advent of Code 2023

## Summary

For further details you should [read the full specification](https://github.com/percyqaz/DMS/blob/main/docs/spec.md), this is a simplified cheat sheet

DMS code is broken up into commands, each command evaluates to an integer result, and can change the state of the machine. The resulting integer is added to the current cell.

A command is either an **expression**, or a unary **operator** applied to another command. See below for details.

## Expressions

Are the simplest building block of commands, representing a single value

| Symbol | Name | Returns | Notes |
| ------ | ---- | ------- | ----- |
| Any sequence of digits 0-9 | NUMBER | The base-10 interpretation of those digits as an integer i.e. `123` returns 123 | Up to implementation what to do if the number is too large for a 32-bit signed int. F# implementation overflows
| `'` followed by any character C | CHAR | The UTF-8 value of the character C as an int, i.e `'a` returns 97 | F# implementation is bugged, wrong conversion in certain character ranges
| `.` | CELL | Value stored in the current cell
| `%` | COMMAND_POINTER | The command pointer
| `[` | CELL_POSITION_X | The X coordinate of the cell pointer |
| `]` | CELL_POSITION_Y | The Y coordinate of the cell pointer |

## Operators

Let **I** denote the input to each operator for the descriptions of effects and return values.

| Symbol | Name | Effect | Returns | Notes |
| ------ | ---- | ------ | ------- | ----- |
| `-` | NEGATE | No effect on state | -I | `-123` is `-` applied to `123`, returning -123
| `+` | SIGN | No effect on state | The sign of I -- 1 if  I is positive, -1 if I is negative, 0 otherwise
| `!` | COMPLEMENT | No effect on state | 1 - I | `!1` returns 0, `!0` returns 1, `!6` returns -5
| `?` | COMPARE | No effect on state | I if the current cell value is positive. 0 otherwise
| `_` | DISCARD | No effect on state | Always returns 0, ignoring its input | Useful for creating a command that updates the state of the machine but doesn't write anything to the current cell.
| `@` | CHAR_OUTPUT | Writes I, interpreted as a UTF-8 character, to standard output. If I is 0, terminates the program instead | I (unchanged) | Outputting the NUL char (0) is the only way to terminate a DMS program
| `*` | INT_OUTPUT | Writes I, formatted as a base-10 integer, to standard output. | I (unchanged)
| `:` | JUMP | Increases the command pointer by I | I (unchanged) | Jumping past the last command or before the first causes the command pointer to wrap around. Jumping by -1 (`:-1`) will result in the same command being executed in a loop.
| `<` | LEFT | Decreases cell pointer's X value by I | I (unchanged) | This moves the current cell left by I. Cell positions wrap at the edges of the tape
| `>` | RIGHT | Increases cell pointer's X value by I | I (unchanged) | This moves the current cell right by I. Cell positions wrap at the edges of the tape
| `^` | UP | Decreases cell pointer's Y value by I | I (unchanged) | This moves the current cell up by I. Cell positions wrap at the edges of the tape
| `v` | DOWN | Increases cell pointer's Y value by I | I (unchanged) | This moves the current cell down by I. Cell positions wrap at the edges of the tape
| `/` | PUSH | Pushes I to the top of the stack | The new stack size
| `\|` | READ | No effect on state | Returns the value on the stack I positions deep. If the stack is empty, returns the current cell value instead | `\|0` returns the topmost stack element. Stack positions wrap around, so `\|-1` returns the bottom element of the stack.
| `\` | POP | Removes the value on the stack I positions deep. If the stack is empty, there is no effect. | Returns the removed value. If the stack is empty, returns the current cell value instead | `\0` removes and returns the topmost stack element. Stack positions  wrap around, so `\-1` removes and returns the bottom element of the stack.
| `;` | DEBUG | Writes debug information about the current state to standard output. No other effect on state | I (unchanged) | What is output is up to the implementation. F# implementation outputs value of various state variables, the command fragment passed as an argument, and then waits for `Console.ReadLine()` before proceeding

So the command `<:?\0`:
- Pops the top value from the stack (or returns the cell value if the stack is empty)
- Jumps ahead by that many commands IF the current cell value is positive, else 0
- Move the cell pointer to the left by the amount of commands jumped over
- Returns the amount of commands jumped over

After executing the command, that returned number is added to the cell that the cell pointer is now pointing to.