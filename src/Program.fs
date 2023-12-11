type Expression =
    | NUMBER of int
    | CELL
    | COMMAND_POINTER
    | CELL_POSITION_X
    | CELL_POSITION_Y
    override this.ToString() =
        match this with
        | NUMBER i -> i.ToString()
        | CELL -> "."
        | COMMAND_POINTER -> "%"
        | CELL_POSITION_X -> "["
        | CELL_POSITION_Y -> "]"

type Command =
    | Ex of Expression
    | NEGATE of Command
    | SIGN of Command
    | COMPLEMENT of Command
    | CONDITION of Command
    | DISCARD of Command
    | CHAR_OUTPUT of Command
    | INT_OUTPUT of Command
    | JUMP of Command
    | LEFT of Command
    | RIGHT of Command
    | UP of Command
    | DOWN of Command
    | PUSH of Command
    | READ of Command
    | POP of Command
    | DEBUG of Command
    override this.ToString() =
        match this with
        | Ex ex -> ex.ToString()
        | NEGATE inner -> sprintf "-%O" inner
        | SIGN inner -> sprintf "+%O" inner
        | PUSH inner -> sprintf "/%O" inner
        | READ inner -> sprintf "|%O" inner
        | POP inner -> sprintf "\%O" inner
        | LEFT inner -> sprintf "<%O" inner
        | RIGHT inner -> sprintf ">%O" inner
        | DOWN inner -> sprintf "v%O" inner
        | UP inner -> sprintf "^%O" inner
        | CONDITION inner -> sprintf "?%O" inner
        | COMPLEMENT inner -> sprintf "!%O" inner
        | CHAR_OUTPUT inner -> sprintf "@%O" inner
        | INT_OUTPUT inner -> sprintf "*%O" inner
        | DISCARD inner -> sprintf "_%O" inner
        | JUMP inner -> sprintf ":%O" inner
        | DEBUG inner -> sprintf ";%O" inner

type Ctx =
    {
        Stack: ResizeArray<int>
        Commands: Command array
        mutable CommandPosition: int
        mutable CellPositionX: int
        mutable CellPositionY: int
        Cells: int[,]
    }
    
open System.IO
open System.Text

module Parser =

    let SYMBOLS = @"0123456789'.%[]-+!?_@*:<>^v/|\;" |> Seq.map int |> Set.ofSeq
    let COMMENT = int '#'

    let number (sr: StreamReader) =
        let mutable next_digit = sr.Peek()
        let mutable value = 0

        while next_digit >= int '0' && next_digit <= int '9' do
            value <- value * 10
            value <- value + sr.Read() - int '0'
            next_digit <- sr.Peek()

        value

    let rec command (sr: StreamReader) =
        let mutable c = sr.Peek()

        match c with
        | 48
        | 49
        | 50
        | 51
        | 52
        | 53
        | 54
        | 55
        | 56
        | 57 -> number sr |> NUMBER |> Ex
        | 46 -> sr.Read() |> fun _ -> Ex CELL
        | 39 -> sr.Read() |> fun _ -> sr.Read() |> NUMBER |> Ex
        | 37 -> sr.Read() |> fun _ -> Ex COMMAND_POINTER
        | 91 -> sr.Read() |> fun _ -> Ex CELL_POSITION_X
        | 93 -> sr.Read() |> fun _ -> Ex CELL_POSITION_Y

        | 45 -> sr.Read() |> fun _ -> command sr |> NEGATE
        | 43 -> sr.Read() |> fun _ -> command sr |> SIGN
        | 33 -> sr.Read() |> fun _ -> command sr |> COMPLEMENT
        | 47 -> sr.Read() |> fun _ -> command sr |> PUSH
        | 124 -> sr.Read() |> fun _ -> command sr |> READ
        | 92 -> sr.Read() |> fun _ -> command sr |> POP
        | 60 -> sr.Read() |> fun _ -> command sr |> LEFT
        | 62 -> sr.Read() |> fun _ -> command sr |> RIGHT
        | 118 -> sr.Read() |> fun _ -> command sr |> DOWN
        | 94 -> sr.Read() |> fun _ -> command sr |> UP
        | 63 -> sr.Read() |> fun _ -> command sr |> CONDITION
        | 64 -> sr.Read() |> fun _ -> command sr |> CHAR_OUTPUT
        | 42 -> sr.Read() |> fun _ -> command sr |> INT_OUTPUT
        | 95 -> sr.Read() |> fun _ -> command sr |> DISCARD
        | 58 -> sr.Read() |> fun _ -> command sr |> JUMP
        | 59 -> sr.Read() |> fun _ -> command sr |> DEBUG
        | -1 -> failwith "Unexpected EOF"
        | s -> failwithf "Unexpected character '%c' at position %i" (char s) sr.BaseStream.Position

    let parse (sr: StreamReader) : Command array =
        seq {
            let mutable eof = false

            while not eof do
                let next = sr.Peek()

                match next with
                | -1 -> eof <- true
                | 35 ->
                    while let r = sr.Read() in r <> 10 && r <> -1 do
                        ()
                | x when SYMBOLS.Contains x -> yield command sr
                | _ -> sr.Read() |> ignore
        }
        |> Array.ofSeq

let inline (%%) (a: 'T) (b: 'T) = ((a % b) + b) % b

[<EntryPoint>]
let main (argv: string array) =
    let mutable i = 0
    let mutable show_help = argv.Length = 0
    let mutable data_file = None
    let mutable mem_lower_bound = -32767
    let mutable mem_upper_bound = 32767
    let mutable code_file = None

    let wrap_cell_pos(x) =
        (x - mem_lower_bound) %% (mem_upper_bound + 1 - mem_lower_bound) + mem_lower_bound

    while i < argv.Length do
        match argv.[i] with
        | "--help"
        | "-h" -> show_help <- true
        | "--data"
        | "-d" when i + 1 < argv.Length ->
            data_file <- Some argv.[i + 1]
            i <- i + 1
        | "--mem"
        | "-m" when i + 1 < argv.Length ->
            let mem_request = argv.[i + 1]

            if mem_request.Contains(":") then
                try
                    let s = mem_request.Split(":")
                    mem_upper_bound <- int s.[1]
                    mem_lower_bound <- int s.[0]
                with err ->
                    printfn "Typo in memory size request, should for example 1024 or -1024:1024"
            else
                try
                    mem_lower_bound <- 0
                    mem_upper_bound <- int mem_request
                with err ->
                    printfn "Typo in memory size request, should for example 1024 or -1024:1024"

            i <- i + 1
        | f when i + 1 = argv.Length -> code_file <- Some f
        | unknown -> printfn "Unknown argument '%s'" unknown

        i <- i + 1

    if show_help || mem_lower_bound > mem_upper_bound then
        printfn "Usage: dms <source file>\n\nFlags:"
        printfn " --help -h\t\t\tShows this help message"
        printfn " --mem -m <size>\t\tSets the size of the memory cell space"
        printfn " --data -d <text file>\t\tInitialises the memory cells to the contents of a text file"
        0
    else

    match code_file with
    | None ->
        printfn "No source file specified"
        1
    | Some code_file ->


    let ctx =
        {
            Stack = ResizeArray()
            Commands =
                try
                    use fs = File.OpenRead(code_file)
                    use sr = new StreamReader(fs, Encoding.UTF8)
                    Parser.parse (sr)
                with err ->
                    printfn "Failed to read file '%s' (%s)" code_file err.Message
                    [||]
            CommandPosition = 0
            CellPositionX = wrap_cell_pos 0
            CellPositionY = wrap_cell_pos 0
            Cells =
                Array2D.zeroCreateBased
                    mem_lower_bound
                    mem_lower_bound
                    (mem_upper_bound + 1 - mem_lower_bound)
                    (mem_upper_bound + 1 - mem_lower_bound)
        }

    match data_file with
    | None -> ()
    | Some data_file ->
        try
            let lines = File.ReadAllLines(data_file)
            let mutable y = wrap_cell_pos 0

            for line in lines do
                let mutable x = wrap_cell_pos 0

                for char in line do
                    ctx.Cells.[x, y] <- int char
                    x <- wrap_cell_pos (x + 1)

                y <- wrap_cell_pos (y + 1)
        with err ->
            printfn "Failed to read file '%s'" data_file

    let mutable exit = ctx.Commands.Length = 0

    let rec eval (command: Command) =
        match command with
        | Ex ex ->
            match ex with
            | NUMBER i -> i
            | CELL -> ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY]
            | COMMAND_POINTER -> ctx.CommandPosition
            | CELL_POSITION_X -> ctx.CellPositionX
            | CELL_POSITION_Y -> ctx.CellPositionY
        | NEGATE cmd -> -(eval cmd)
        | SIGN cmd ->
            let x = eval cmd

            if x < 0 then -1
            elif x = 0 then 0
            else 1
        | PUSH cmd ->
            ctx.Stack.Insert(0, eval cmd)
            ctx.Stack.Count
        | READ cmd ->
            if ctx.Stack.Count = 0 then
                ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY]
            else
                let idx = eval cmd %% ctx.Stack.Count
                ctx.Stack.[idx]
        | POP cmd ->
            if ctx.Stack.Count = 0 then
                ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY]
            else
                let idx = eval cmd %% ctx.Stack.Count
                let res = ctx.Stack.[idx]
                ctx.Stack.RemoveAt(idx)
                res
        | LEFT cmd ->
            let x = eval cmd

            ctx.CellPositionX <- wrap_cell_pos (ctx.CellPositionX - x)

            x
        | RIGHT cmd ->
            let x = eval cmd

            ctx.CellPositionX <- wrap_cell_pos (ctx.CellPositionX + x)

            x
        | UP cmd ->
            let x = eval cmd

            ctx.CellPositionY <- wrap_cell_pos (ctx.CellPositionY - x)

            x
        | DOWN cmd ->
            let x = eval cmd

            ctx.CellPositionY <- wrap_cell_pos (ctx.CellPositionY + x)

            x
        | CONDITION cmd ->
            let x = eval cmd

            if ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY] > 0 then
                x
            else
                0
        | COMPLEMENT cmd -> 1 - eval cmd
        | CHAR_OUTPUT cmd ->
            let x = eval cmd
            if x <> 0 then printf "%c" (char x) else exit <- true
            x
        | INT_OUTPUT cmd ->
            let x = eval cmd
            printf "%i" x
            x
        | DISCARD cmd -> eval cmd |> fun _ -> 0
        | JUMP cmd ->
            let x = eval cmd
            ctx.CommandPosition <- (ctx.CommandPosition + x) %% ctx.Commands.Length
            x
        | DEBUG cmd ->
            let fmt_int i =
                if i > 32 && i < 128 then
                    sprintf "%i (%c)" i (char i)
                else
                    sprintf "%i" i

            let x = eval cmd

            printfn
                "BREAKPOINT INFORMATION\nCommand %O evaluated to %s\nYou are at [%i, %i] (%s)\nStack top -> bottom: %s"
                cmd
                (fmt_int x)
                ctx.CellPositionX
                ctx.CellPositionY
                (fmt_int ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY])
                (List.ofSeq ctx.Stack |> List.map fmt_int |> String.concat "  ")

            System.Console.ReadLine() |> ignore
            x

    while not exit do
        let result = eval ctx.Commands.[ctx.CommandPosition]
        ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY] <- ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY] + result
        ctx.CommandPosition <- (ctx.CommandPosition + 1) %% ctx.Commands.Length

    0
