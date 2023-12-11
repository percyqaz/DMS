const SYMBOLS = "0123456789'.%[]-+!?_@*:<>^v/|\\;";

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
			var x = evaluate_command(state, command.inner); 
			state.cellPointerY = mod(state.cellPointerY - x, state.cells[0].length);
			return x;
		case "DOWN":
			var x = evaluate_command(state, command.inner); 
			state.cellPointerY = mod(state.cellPointerY + x, state.cells[0].length);
			return x;
		case "CONDITION":
			var x = evaluate_command(state, command.inner);
			if (state.cells[state.cellPointerX, state.cellPointerY] > 0) {
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
		document.getElementById("output").value = e.message;
		return;
	}
	
	var memory_cells = Array(1000).fill().map(() => Array(1000).fill(0));
	
	var state = {
		stack: [],
		commands: commands,
		commandPointer: 0,
		cellPointerX: 0,
		cellPointerY: 0,
		cells: memory_cells,
		output: "",
		exit: commands.length === 0
	}
	
	// todo: negative cell positions
	// todo: initialise the cells
	
	while (!state.exit) {
		var result = evaluate_command(state, state.commands[state.commandPointer]);
		state.cells[state.cellPointerX][state.cellPointerY] += result;
		state.commandPointer = mod(state.commandPointer + 1, state.commands.length);
	}
	
	document.getElementById("output").value = state.output;
}