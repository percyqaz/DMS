const SYMBOLS = "0123456789'.%[]-+!?_@*:<>^v/|\\;";
const MEMORY_TAPE_SIZE = 1000
const LOOP_TIMEOUT_MS = 3000

function mod(n, m) {
  return ((n % m) + m) % m;
}

function parse_number(peek, read) {
	var next = peek();
	var value = 0;
	
	while ("0123456789".includes(next)) {
		value = value * 10;
		value = value + parseInt(read());
		next = peek();
	}
	
	return value;
}

function parse_command(peek, read) {
	var next = peek();
	if ("0123456789".includes(next)) {
		return { type: "NUMBER", value: parse_number(peek, read) };
	} 
	
	else if (next === '.') {
		read();
		return { type: "CELL" };
	} else if (next === '\'') {
		read();
		return { type: "NUMBER", value: read().charCodeAt(0) };
	} else if (next === '%') {
		read();
		return { type: "COMMAND_POINTER" };
	} else if (next === '[') {
		read();
		return { type: "CELL_POSITION_X" };
	} else if (next === ']') {
		read();
		return { type: "CELL_POSITION_X" };
	}
	
	else if (next === '-') {
		read();
		return { type: "NEGATE", inner: parse_command(peek, read) };
	} else if (next === '+') {
		read();
		return { type: "SIGN", inner: parse_command(peek, read) };
	} else if (next === '!') {
		read();
		return { type: "COMPLEMENT", inner: parse_command(peek, read) };
	} else if (next === '?') {
		read();
		return { type: "CONDITION", inner: parse_command(peek, read) };
	} else if (next === '_') {
		read();
		return { type: "DISCARD", inner: parse_command(peek, read) };
	} else if (next === '@') {
		read();
		return { type: "CHAR_OUTPUT", inner: parse_command(peek, read) };
	} else if (next === '*') {
		read();
		return { type: "INT_OUTPUT", inner: parse_command(peek, read) };
	} else if (next === ':') {
		read();
		return { type: "JUMP", inner: parse_command(peek, read) };
	} else if (next === '<') {
		read();
		return { type: "LEFT", inner: parse_command(peek, read) };
	} else if (next === '>') {
		read();
		return { type: "RIGHT", inner: parse_command(peek, read) };
	} else if (next === '^') {
		read();
		return { type: "UP", inner: parse_command(peek, read) };
	} else if (next === 'v') {
		read();
		return { type: "DOWN", inner: parse_command(peek, read) };
	} else if (next === '/') {
		read();
		return { type: "PUSH", inner: parse_command(peek, read) };
	} else if (next === '|') {
		read();
		return { type: "READ", inner: parse_command(peek, read) };
	} else if (next === '\\') {
		read();
		return { type: "POP", inner: parse_command(peek, read) };
	} else if (next === ';') {
		read();
		return { type: "DEBUG", inner: parse_command(peek, read) };
	}
	
	else if (next === undefined) {
		throw new Error("Unexpected end of input while parsing a command");
	}
	else {
		throw new Error("Unexpected character '" + next + "' while parsing a command");
	}
}

function parse(source_code) {
	var commands = [];
	
	var i = 0;
	
	function peek() {
		return source_code[i];
	}
	
	function read() { 
		c = source_code[i];
		i = i + 1;
		return c;
	}
	
	var eof = false;
	while (!eof) {
		const next = peek();
		if (next === undefined) {
			eof = true;
		} else if (next === '#') {
			var comment_char = read();
			do {
				comment_char = read();
			} while (comment_char !== '\n' && comment_char !== undefined);
		} else if (SYMBOLS.includes(next)) {
			var command = parse_command(peek, read);
			commands.push(command);
		} else {
			read();
		}
	}
	
	return commands;
}

function evaluate_command(state, command) {
	switch (command.type) {
		case "NUMBER":
			return command.value;
		case "CELL":
			return state.cells[state.cellPointerX][state.cellPointerY];
		case "COMMAND_POINTER":
			return state.commandPointer;
		case "CELL_POSITION_X":
			return state.cellPointerX;
		case "CELL_POSITION_Y":
			return state.cellPointerY;
		case "NEGATE":
			return -evaluate_command(state, command.inner);
		case "SIGN":
			var x = evaluate_command(state, command.inner);
			if (x > 0) {
				return 1;
			} else if (x < 0) {
				return -1;
			} else {
				return 0;
			}
		case "PUSH":
			state.stack.unshift(evaluate_command(state, command.inner));
			return state.stack.length;
		case "READ":
			var x = evaluate_command(state, command.inner); 
			return state.stack[mod(x, state.stack.length)];
		case "POP":
			var x = evaluate_command(state, command.inner); 
			var v = state.stack[mod(x, state.stack.length)];
			state.stack.splice(mod(x, state.stack.length), 1);
			return v;
		case "LEFT":
			var x = evaluate_command(state, command.inner); 
			state.cellPointerX = mod(state.cellPointerX - x, state.cells.length);
			return x;
		case "RIGHT":
			var x = evaluate_command(state, command.inner); 
			state.cellPointerX = mod(state.cellPointerX + x, state.cells.length);
			return x;
		case "UP":
			var y = evaluate_command(state, command.inner); 
			state.cellPointerY = mod(state.cellPointerY - y, state.cells[0].length);
			return y;
		case "DOWN":
			var y = evaluate_command(state, command.inner); 
			state.cellPointerY = mod(state.cellPointerY + y, state.cells[0].length);
			return y;
		case "CONDITION":
			var x = evaluate_command(state, command.inner);
			if (state.cells[state.cellPointerX][state.cellPointerY] > 0) {
				return x;
			} else {
				return 0;
			}
		case "COMPLEMENT":
			return 1 - evaluate_command(state, command.inner);
		case "CHAR_OUTPUT":
			var x = evaluate_command(state, command.inner);
			if (x === 0) {
				state.exit = true;
			} else {
				state.output += String.fromCharCode(x);
			}
			return x;
		case "INT_OUTPUT":
			var x = evaluate_command(state, command.inner);
			state.output += x.toString();
			return x;
		case "DISCARD":
			var _ = evaluate_command(state, command.inner);
			return 0;
		case "JUMP":
			var x = evaluate_command(state, command.inner);
			state.commandPointer = mod(state.commandPointer + x, state.commands.length);
			return x;
		case "DEBUG":
			var x = evaluate_command(state, command.inner);
			state.output += "\nDEBUG BREAKPOINT HIT";
			state.output += "\nArgument to breakpoint: " + x.toString();
			state.output += "\nYou are at: [" + state.cellPointerX.toString() + ", " + state.cellPointerY.toString() + "]";
			state.output += "\nCell at this position has value: " + state.cells[state.cellPointerX][state.cellPointerY].toString();
			state.output += "\nCurrent stack (top -> bottom): " + state.stack.toString();
			return x;
	}
}

function run() {
	const source = document.getElementById("source").value;
	
	var commands = [];
	try {
		commands = parse(source);
	} catch (e) {
		document.getElementById("output").value = "<Syntax error>\n" + e.message;
		return;
	}
	
	var memory_cells = Array(MEMORY_TAPE_SIZE).fill().map(() => Array(MEMORY_TAPE_SIZE).fill(0));
	
	var state = {
		stack: [],
		commands: commands,
		commandPointer: 0,
		cellPointerX: 0,
		cellPointerY: 0,
		cells: memory_cells,
		output: "",
		exit: commands.length === 0
	};
	
	const data = document.getElementById("data").value;
	
	var x = 0;
	var y = 0;
	var i = 0;
	while (i < data.length) {
		if (data[i] === '\r' && data[i + 1] === '\n') {
			i++;
			y++;
			x = 0;
		} else if (data[i] === '\n') {
			y++;
			x = 0;
		} else {
			memory_cells[mod(x, memory_cells.length)][mod(y, memory_cells[0].length)] = data.charCodeAt(i);
			x++;
		}
		i++;
	}
	
	var startTime = Date.now();
	
	while (!state.exit) {
		var result = evaluate_command(state, state.commands[state.commandPointer]);
		state.cells[state.cellPointerX][state.cellPointerY] += result;
		state.commandPointer = mod(state.commandPointer + 1, state.commands.length);
		
		if (Date.now() - startTime > LOOP_TIMEOUT_MS) {
			state.output += "\n<Program terminated due to running for too long, likely an infinite loop>";
			break;
		}
	}
	
	document.getElementById("output").value = state.output;
}

const SAMPLE_CODE_1 =
`# this program outputs the input data contents
_@.  # print current cell
_>1  # move to the right 1 
_:?2 # if at a non-empty cell skip the next 2 instructions
# next two instructions are: output a line feed, move to beginning of next line
_@'
_v+<[
# 😃 program loops to top, quits when printing a nul byte
`;

function load_example_1() {
	document.getElementById("source").value = SAMPLE_CODE_1;
	document.getElementById("data").value = SAMPLE_CODE_1;
}

window.onload = load_example_1;

const SAMPLE_CODE_2 =
`# this program solves Advent of Code 2023 puzzle 4, part 1
# https://adventofcode.com/2023/day/4
_/1

# find index of :, push it to the stack
-':_/0\\_:|+._:3':_>1_:-7':/<[

# find index of |, push it to the stack
-'|_/0\\_:|+._:3'|_>1_:-7'|/<[

_>|1_<1 # start at :
_>3 # work through numbers left to right

# if number is null (having gone through every card) jump to end to print output
_:?1_:52

# if we are looking at |, we have checked every number for this card, go next card
-'|_/1_/0_\\\\_:!|+._:4'|_v1_<[_:-14

# write working position + number to compare to the stack, then go to | to start search
'|_/[_/._>1_/._<[_>|3_<1
_>3 # work through numbers left to right

# if number is null we have reached the end of the search on this row, try the next number
_:?6_<[_>|2_\\0_\\0_\\0_:-28

# compare first digit against number we are searching for
-|1_/1_/0_\\\\_:|+._:2
|1_:-15 # no match, go next
|1_>1-|0_/1_/0_\\\\_:|+._:3 # first digit matches, try second digit
|0_<1_:-11 # no match, go next

# match found! add 1 to cell left of row if 0, else double it. then go to next number
|0_<!-[_:?1:1._>!-|2_\\0_\\0_\\0_:-28

# sum scores for each card, output the result
_/0_<!-[-1_^]_:1-1\\0_/._^[1_:?-6_^1*\\0@0
`;

const SAMPLE_INPUT_2 =
`Card 1: 41 48 83 86 17 | 83 86  6 31 17  9 48 53
Card 2: 13 32 20 16 61 | 61 30 68 82 17 32 24 19
Card 3:  1 21 53 59 44 | 69 82 63 72 16 21 14  1
Card 4: 41 92 73 84 69 | 59 84 76 51 58  5 54 83
Card 5: 87 83 26 28 32 | 88 30 70 12 93 22 82 36
Card 6: 31 18 13 56 72 | 74 77 10 23 35 67 36 11
`

function load_example_2() {
	document.getElementById("source").value = SAMPLE_CODE_2;
	document.getElementById("data").value = SAMPLE_INPUT_2;
}

function load_example_3() {
	document.getElementById("source").value = "I don't exist yet";
	document.getElementById("data").value = "I don't exist yet";
}