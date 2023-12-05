# DMS
Esoteric programming language built for Advent of Code 2023

## Specification

The DMS machine works only with 32-bit signed integers. There are no other types. All indices are 0-based.
It consists of:
- A 2D tape (with a finite size of your choosing) with the cell pointer starting at 0,0
- A stack where numbers can be pushed or popped. Implementations can optionally have a maximum size, mine does not until OOM.
- A command pointer, representing the current executing command

The memory tape is initialised to all 0s, but can be initialised to the contents of a text file, more on this below

DMS code is broken up into commands, each command evaluates to a number and may have side effects that change the stack, memory tape and cursor position

A command is either an expression, or a unary operator applied to another command. Unary operators apply to what is on their right.

### Expressions
Any sequence of base-10 digits i.e `31415` is an expression evaluating to the base-10 number written.  
Implementations may optionally output a parser error when trying to enter a number too big for a signed 32-bit int, my implementation overflows while parsing instead.

`'` will express the character immediately following as a number, by converting it to its UTF-8 representation, for example `'a` is completely equivalent to `97`.  
Used to conveniently enter constants for certain characters  
My implementation currently performs a UTF-16 conversion, which is a bug
  
`.` evaluates to the value of the cell under the cell pointer

`%` evaluates to the value of the command pointer, for example if this is the second command in the program, this will be `1`.

`[` evaluates to the x coordinate of the cell pointer, initially 0.

`]` evaluates to the y coordinate of the cell pointer, initially 0.

### Commands
The **current cell** refers to the cell in the 2D tape that the cell pointer is over

Commands apply to their right operand, and can be chained
The final value of the command is added to the current cell. No overflow checking should be implemented.

`-` negates its argument, hence the full command `-17` would subtract 17 from the current cell.

`+` returns the 'sign' of its argument - this is -1 if the argument was negative, 1 if positive, 0 if 0. `+-17` would subtract 1 from the current cell.

`!` returns the 'complement' of its argument - this is 1 - the argument. `!!0` evaluates to 0. `!2` evaluates to -1.

`/` pushes its argument to the stack, and returns the new count of items currently on the stack.

`|` reads the stack at the position described by its argument, and returns that value.  
The argument should wrap around if too small or too big, for example if the top of the stack is 0 and the bottom is 1, `|-1` will return 1.  
If the stack is empty this should return the current cell value instead.

`\` pops the value from the stack at the position described by its argument, and returns that value.  
Same wrapping behaviour as `|`  
If the stack is empty this should return the current cell value instead.

`<` moves the cell pointer left N steps, where N is its argument. It then returns N, unchanged.  
If this moves the cell pointer out of bounds, it should wrap, so if the lowest available cell coordinate was -1 and the highest was 10, moving 5 left from position 0 will put you at 7.

`>` moves the cell pointer right N steps, where N is its argument. It then returns N, unchanged.  
Same wrapping behaviour same as `<`

`^` moves the cell pointer up N steps, where N is its argument. It then returns N, unchanged.  
Same wrapping behaviour same as `<`

`v` moves the cell pointer down N steps, where N is its argument. It then returns N, unchanged.  
Same wrapping behaviour same as `<`  
For example, `v+<5` would move the cell pointer 5 cells to the left, then one cell down, and then increment the cell at this new position by 1
