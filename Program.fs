type Expression =
    | K of int
    | Cell
    | StackPosition
    | CommandPosition
    | CellPositionX
    | CellPositionY

type Command =
    | Ex of Expression
    | Neg of Command
    | Sign of Command
    | StackPush of Command
    | StackRead of Command
    | StackPop of Command
    | Left of Command
    | Right of Command
    | Down of Command
    | Up of Command
    | Cond of Command
    | Complement of Command
    | Output of Command
    | Discard of Command
    | Jump of Command

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

    let SYMBOLS = @"0123456789.'$%[]-+!/|\><v^?@_:" |> Seq.map int |> Set.ofSeq
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
        | 48 | 49 | 50 | 51 | 52 | 53 | 54 | 55 | 56 | 57 -> number sr |> K |> Ex
        | 46 -> sr.Read() |> fun _ -> Ex Cell
        | 39 -> sr.Read() |> fun _ -> sr.Read() |> K |> Ex
        | 36 -> sr.Read() |> fun _ -> Ex StackPosition
        | 37 -> sr.Read() |> fun _ -> Ex CommandPosition
        | 91 -> sr.Read() |> fun _ -> Ex CellPositionX
        | 93 -> sr.Read() |> fun _ -> Ex CellPositionY

        | 45 -> sr.Read() |> fun _ -> command sr |> Neg
        | 43 -> sr.Read() |> fun _ -> command sr |> Sign
        | 33 -> sr.Read() |> fun _ -> command sr |> Complement
        | 47 -> sr.Read() |> fun _ -> command sr |> StackPush
        | 124 -> sr.Read() |> fun _ -> command sr |> StackRead
        | 92 -> sr.Read() |> fun _ -> command sr |> StackPop
        | 60 -> sr.Read() |> fun _ -> command sr |> Left
        | 62 -> sr.Read() |> fun _ -> command sr |> Right
        | 118 -> sr.Read() |> fun _ -> command sr |> Down
        | 94 -> sr.Read() |> fun _ -> command sr |> Up
        | 63 -> sr.Read() |> fun _ -> command sr |> Cond
        | 64 -> sr.Read() |> fun _ -> command sr |> Output
        | 95 -> sr.Read() |> fun _ -> command sr |> Discard
        | 58 -> sr.Read() |> fun _ -> command sr |> Jump
        | _ -> failwith "unreachable unless the SYMBOLS set is out of sync"

    let parse (sr: StreamReader) : Command array = 
        seq {
            let mutable eof = false
            while not eof do
                let next = sr.Peek()
                match next with 
                | -1 -> eof <- true
                | 35 -> while let r = sr.Read() in r <> 10 && r <> -1 do ()
                | x when SYMBOLS.Contains x -> yield command sr
                | _ -> sr.Read() |> ignore
        } |> Array.ofSeq

let inline (%%) (a: 'T) (b: 'T) = ((a % b) + b) % b

[<EntryPoint>]
let main (argv: string array) =
    let mutable i = 0
    let mutable show_help = argv.Length = 0
    let mutable data_file = None
    let mutable mem_lower_bound = -32767
    let mutable mem_upper_bound = 32767
    let mutable code_file = None
    while i < argv.Length do
        match argv.[i] with
        | "--help" | "-h" -> show_help <- true
        | "--data" | "-d" when i + 1 < argv.Length -> 
            data_file <- Some argv.[i + 1]
            i <- i + 1
        | "--mem" | "-m" when i + 1 < argv.Length ->
            let mem_request = argv.[i + 1]
            if mem_request.Contains(":") then
                try
                    let s = mem_request.Split(":")
                    mem_upper_bound <- int s.[1]
                    mem_lower_bound <- int s.[0]
                with err -> printfn "Typo in memory size request, should for example 1024 or -1024:1024"
            else
                try
                    mem_lower_bound <- 0
                    mem_upper_bound <- int mem_request
                with err -> printfn "Typo in memory size request, should for example 1024 or -1024:1024"
            i <- i + 1
        | f when i + 1 = argv.Length -> code_file <- Some f
        | unknown -> printfn "Unknown argument '%s'" unknown
        i <- i + 1

    if show_help then
        printfn "Usage: dms <source file>\n\nFlags:"
        printfn " --help -h\t\t\tShows this help message"
        printfn " --mem -m <size>\t\tSets the size of the memory cell space"
        printfn " --data -d <text file>\t\tInitialises the memory cells to the contents of a text file"
        0
    else 
    
    match code_file with
    | None -> printfn "No source file specified"; 1
    | Some code_file ->
    

    let ctx = 
        {
            Stack = ResizeArray()
            Commands = 
                try
                    use fs = File.OpenRead(code_file)
                    use sr = new StreamReader(fs, Encoding.UTF8)
                    Parser.parse(sr)
                with err -> 
                    printfn "Failed to read file '%s'" code_file
                    [||]
            CommandPosition = 0
            CellPositionX = 0
            CellPositionY = 0
            Cells = Array2D.zeroCreateBased mem_lower_bound mem_lower_bound (mem_upper_bound - mem_lower_bound) (mem_upper_bound - mem_lower_bound)
        }
    match data_file with
    | None -> ()
    | Some data_file ->
        try
            let lines = File.ReadAllLines(data_file)
            let mutable y = 0
            for line in lines do
                let mutable x = 0
                for char in line do
                    ctx.Cells.[x, y] <- int char
                    x <- x + 1
                y <- y + 1
        with err -> printfn "Failed to read file '%s'" data_file

    let mutable exit = ctx.Commands.Length = 0

    let rec eval (command: Command) =
        match command with
        | Ex ex ->
            match ex with
            | K i -> i
            | Cell -> ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY]
            | StackPosition -> ctx.Stack.Count
            | CommandPosition -> ctx.CommandPosition
            | CellPositionX -> ctx.CellPositionX
            | CellPositionY -> ctx.CellPositionY
        | Neg cmd -> -(eval cmd)
        | Sign cmd -> 
            let x = eval cmd
            if x < 0 then -1 elif x = 0 then 0 else 1
        | StackPush cmd -> ctx.Stack.Insert(0, eval cmd); ctx.Stack.Count
        | StackRead cmd -> 
            if ctx.Stack.Count = 0 then 
                ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY]
            else
                let idx = eval cmd %% ctx.Stack.Count
                ctx.Stack.[idx]
        | StackPop cmd ->
            if ctx.Stack.Count = 0 then 
                ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY]
            else
                let idx = eval cmd %% ctx.Stack.Count
                let res = ctx.Stack.[idx]
                ctx.Stack.RemoveAt(idx)
                res
        | Left cmd ->
            let x = eval cmd
            ctx.CellPositionX <- (ctx.CellPositionX - x - mem_lower_bound) %% (mem_upper_bound - mem_lower_bound) + mem_lower_bound
            x
        | Right cmd ->
            let x = eval cmd
            ctx.CellPositionX <- (ctx.CellPositionX + x - mem_lower_bound) %% (mem_upper_bound - mem_lower_bound) + mem_lower_bound
            x
        | Up cmd ->
            let x = eval cmd
            ctx.CellPositionY <- (ctx.CellPositionY - x - mem_lower_bound) %% (mem_upper_bound - mem_lower_bound) + mem_lower_bound
            x
        | Down cmd ->
            let x = eval cmd
            ctx.CellPositionY <- (ctx.CellPositionY + x - mem_lower_bound) %% (mem_upper_bound - mem_lower_bound) + mem_lower_bound
            x
        | Cond cmd ->
            let x = eval cmd
            if ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY] > 0 then x else 0
        | Complement cmd -> 1 - eval cmd
        | Output cmd ->
            let x = eval cmd
            if x > 0 then printf "%c" (char x) else exit <- true
            x
        | Discard cmd -> eval cmd |> fun _ -> 0
        | Jump cmd -> 
            let x = eval cmd
            ctx.CommandPosition <- (ctx.CommandPosition + x) %% ctx.Commands.Length
            x

    while not exit do
        let result = eval ctx.Commands.[ctx.CommandPosition]
        ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY] <- ctx.Cells.[ctx.CellPositionX, ctx.CellPositionY] + result
        ctx.CommandPosition <- (ctx.CommandPosition + 1) %% ctx.Commands.Length
    0