# DMS Specification

- The DMS machine works only with 32-bit signed integers
- All indices are 0-based
- All text characters are parsed and output using UTF-8

## State the machine stores:

- A 2-dimensional memory tape of some non-zero finite size. Each individual position in the tape represents a **cell** and stores an integer.
- A **cell pointer** which has an x coordinate and a y coordinate
- A **stack** where where numbers can be pushed or popped. Implementations can optionally have a maximum size, mine does not until OOM.
- A **command pointer**, an integer representing which command is currently executing
- The commands representing the instructions of the executing DMS program

The **current cell** is the cell with coordinates matching the cell pointer
The **stack size** is the count of integers it currently contains

It is up to the implementation to optionally impose a maximum stack size. The F# implementation does not and will simply crash if out of memory.

It is up to the implementation to optionally impose a maximum number of commands in a DMS program. The F# implementation will crash if it runs into size issues.

## Initial state:

- All cells are initialised to 0
- The cell pointer X and Y coordinates are 0
- The stack is empty
- The command pointer is 0

Memory cells can also be initialised to the character contents of a text file, more on this below

DMS code is broken up into commands, each command evaluates to an integer result, and may have side effects that change the state of the machine. The integer result is added to the current cell.

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

## Execution

If a DMS program consists of no commands, it will exit immediately.

Until termination, the DMS machine will:
- Execute the current command, resulting in an integer value
- Add that integer value to the current cell (overflowing if needed)
- Increment the command pointer by 1, wrapping around to 0 if the end of the program reached

As mentioned in the description of `@`, attempting to output a NUL character (0) will terminate execution. This is the only way to terminate the program.

## Initialising the machine

### Tape size

Implementations can optionally support requests for a maximum tape size. How the user provides this and the default size is up to the implementation.  
My implementation allows you to provide the bounds as a range, including allowing negative coordinates

If the cell coordinate 0, 0 is not within the range requested, the initial coordinate value should wrap to fall within the range.

The tape must always contain at least 1 cell, it cannot be 0x0.

### Initialising the tape with file contents

Interpreter implementations should support a method for using UTF-8 encoded text to populate the memory cells.  
A chosen piece of text should be read line by line - a line ends with CRLF, LF or the end of the text.  

Each line is then inserted as a row of UTF-8 characters, converted to integers, into the tape, starting at (0,0) for the first line of the file, (0,1) for the second, etc.  

The trailing CRLF or LF at the end of a line should not be written to the tape. In the case of CRCRLF, it is up to the implementation if *all* trailing CR characters should be stripped, or just the last one

The rest of the tape remains initialised to 0.

Written cell positions should wrap to fit the max size of the tape

## Parsing a DMS program

Parsing should operate character-by-character (in UTF-8 encoding)

A command character is any character that could be part of a command, obtainable from the tables above: `0123456789'.%[]-+!?_@*:<>^v/|\;`

Parsing should work by:

#### State 1: Not currently parsing a command
- EOF -> finish parsing
- `#` is seen -> discard everything until the next LF. This allows for comments
- Any other non-command character -> Discard it
- Any command character -> Enter State 2

Repeat until EOF

#### State 2: Parsing a command
- Any digit -> Read digits until the next non-digit to parse a NUMBER; return to State 1
- `'` -> Read the next char to parse a CHAR; return to State 1
- Any other expression character -> parse as that expression; return to State 1
- Any operator character -> parse that char, then continue in State 2 to find the rest of the command
- Anything else including EOF -> This is a syntax error. Implementations should output a sensible error message.

Some examples:

The file containing only `>` would not parse due to `>` being an operation with no operand on its right.  
`>#` would not parse due to `#` not being a valid expression or operator.

It is not ambiguous to have multiple commands on the same line in your source file:
`_>3_<+5_^>>-+9` is 3 valid commands that will parse back to back

You can separate your commands by any characters that doesn't make up part of a command or expression (such as whitespace), or by using a # to have the parser ignore the rest of the line