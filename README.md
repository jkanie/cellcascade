# cellcascade

A spreadsheet formula engine for .NET, with an HTTP API and a small grid screen. You set cells to values or Excel-style formulas. Each change re-calculates only the cells that depend on it, in an order where every cell comes after the cells it reads.

## What it does

- Reads formulas with Excel's operator order: `=-2^2` is 4 and `=2^3^2` is 64, as in Excel.
- Supports numbers, text, TRUE and FALSE, cell references (`A1`, `$B$2`), ranges (`A1:C10`). It has 18 functions: ABS, AND, AVERAGE, CONCAT, COUNT, COUNTA, IF, IFERROR, LEN, LOWER, MAX, MIN, NOT, OR, ROUND, SUM, UPPER, VLOOKUP.
- Keeps a dependency graph of the sheet. A change re-calculates the changed cells' dependents (directly or through other cells), each 1 time. Every other cell stays as it was.
- Finds circular references with Tarjan's algorithm and marks every cell in a cycle `#CYCLE!`. A cell that reads a cycle gets the error too, unless IFERROR catches it.
- Gives Excel's error values: `#DIV/0!`, `#VALUE!`, `#REF!`, `#NAME?`, `#NUM!`, `#N/A`.
- Answers every edit with the cells that changed and the cells that were re-calculated, in order. The grid screen flashes those cells, so the cascade is visible.
- Refuses a formula that cannot be read with a reason and a position (`D6: A cell address must follow ':' at position 8.`). The sheet then stays as it was.

## Who it is for

.NET developers who need spreadsheet formulas on a server: price sheets, budget calculators, business rules that the people who use them can write as formulas, without Excel on the server. The engine is a plain class library with no dependencies. The API shows 1 way to host it.

## Built with

- C# on .NET 10 (SDK 10.0.114, ASP.NET Core 10.0.14 runtime). The engine uses only the .NET base library.
- ASP.NET Core minimal APIs for the HTTP API, with the sheets kept in memory.
- A grid screen in plain HTML, CSS, JavaScript (no build step), served by the API.
- Tests: xUnit v3 4.0.3 on Microsoft.Testing.Platform, plus Microsoft.AspNetCore.Mvc.Testing 10.0.14 for the API tests.

## Set-up

You need the .NET 10 SDK.

```sh
dotnet run --project src/CellCascade.Api --urls http://localhost:5080
```

Then open http://localhost:5080 for the grid screen, or call the API at `http://localhost:5080/api`. The app starts with a sample sheet called `demo`: a made-up budget with 3 lines, a 10% discount, a total, a check against the budget.

## Tests

```sh
dotnet format --verify-no-changes
dotnet test
```

168 tests:

- The oracle test applies 12,000 random edits (300 seeded random sheets of 25 cells, 40 edits each) and checks the engine after every edit against a slow reference sheet in `tests/CellCascade.Tests/ReferenceSheet.cs`. The reference keeps no graph: it works out every value again from scratch, with its own walk for references and its own cycle check. The test compares every value, the exact set of cells re-calculated, their order, the cells in a cycle, the cells that changed. The random formulas cover arithmetic, comparison, `&`, ranges, lookups, IF branches, IFERROR, cycles (more than 500 of the edits make 1).
- Another test applies 100 random batches in 2 orders and requires the same values.
- Unit tests pin the operator order, the type rules, every function (with Excel's results for cases such as `ROUND(1.005,2)`, which is 1.01), the input rules, the parse faults, a chain of 100,000 cells, a cycle of 100,000 cells.
- API tests start the app in memory and cover the sample sheet, the cascade answer, batches, cycles, a cell's precedents and dependents, refusals (400 and 404).

On every push and pull request, GitHub Actions runs `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test` on Linux, Windows, macOS with .NET 10 (see [.github/workflows/ci.yml](.github/workflows/ci.yml)).

## Formulas

Input that starts with `=` is a formula. Other input is 1 of these: a number (`12`, `1e3`, `50%`), TRUE or FALSE, text. A leading `'` keeps the rest as text, so `'007` stays `007`. Empty input makes the cell blank. An input can have up to 8,192 characters, as in Excel. A sheet reaches column XFD and row 1,048,576, also as in Excel.

Operators, from loosest to tightest: comparison (`= <> < <= > >=`), then `&`, then `+ -`, then `* /`, then `^`, then `%` after a value, then a leading `-` or `+`, then `:` (a range). Every binary operator groups from the left.

Types follow Excel's rules:

- A blank cell is 0 in arithmetic and empty text in `&`. A formula that only points at a blank cell shows 0.
- Text that holds a number works in arithmetic (`="3"+1` is 4). Other text gives `#VALUE!`.
- Comparison puts every number before every text and every text before TRUE and FALSE. Text compares without regard to case.
- SUM, AVERAGE, MIN, MAX, COUNT skip text and booleans inside ranges and references, but turn a value typed into the call into a number (`=SUM("3",TRUE)` is 4).
- IF and IFERROR evaluate only the argument that they return.
- ROUND rounds half away from 0. VLOOKUP looks up approximately unless its 4th argument is FALSE.
- When both sides of an operator hold errors, the left 1 wins.

Where this engine differs from Excel:

- A circular reference gives `#CYCLE!`, which Excel does not have. Excel warns and shows 0 instead.
- A range where 1 value is needed (`=A1:A3+1`) gives `#VALUE!`. Excel would pick 1 cell (implicit intersection) or spill an array.
- Text compares by character code without regard to case, not by Excel's language-aware order.
- There are no number formats or dates yet, so 10% shows as 0.1.
- An approximate VLOOKUP reads the rows in order and stops at the 1st key that is too large. Excel asks for sorted keys in that case. On unsorted keys, its result can differ from this one.
- There are no whole-column references (`A:A`), references to other sheets, names, arrays.

## How re-calculation works

1. The edited cells are set, and their references are added to the graph. Each formula keeps the set of cells it reads, and each cell keeps the set of formulas that read it. A range adds each of its cells. That is why 1 range is capped at 100,000 cells.
2. A breadth-first walk from the edited cells collects every cell that the change reaches.
3. Tarjan's algorithm splits those cells into strongly connected components. A component of more than 1 cell, or a cell that reads itself, is a cycle. The algorithm runs with an explicit stack, so a chain or a cycle of 100,000 cells cannot overflow the call stack.
4. The components run in dependency order. Each cell in a cycle gets `#CYCLE!`. Every other formula is evaluated 1 time.
5. The answer lists the cells whose value changed and the cells that were re-calculated.

## API

All requests and answers are JSON.

| Method | Path | What it does |
|---|---|---|
| GET | `/api/sheets` | Lists the sheet names. |
| GET | `/api/sheets/{name}` | Lists every cell that is not blank, with its input and value. |
| GET | `/api/sheets/{name}/cells/{cell}` | Shows 1 cell, with the cells it reads (precedents) and the cells that read it (dependents). |
| PUT | `/api/sheets/{name}/cells/{cell}` | Sets 1 cell: `{"input": "=A1*2"}`. Creates the sheet when needed. |
| POST | `/api/sheets/{name}/cells` | Sets up to 10,000 cells, then re-calculates 1 time: `{"cells": {"A1": "1", "B1": "=A1*2"}}`. |
| DELETE | `/api/sheets/{name}` | Removes a sheet. |
| GET | `/api/functions` | Lists the function names. |

An edit answers with `changed` (each cell whose value changed), `recalculated` (the formulas evaluated, in order), `inCycle` (the cells in a cycle). A formula that cannot be read gives 400 with an `error` message. Nothing changes in that case.

## Sample

This answer is from a real run against the sample sheet, pretty-printed. The request sets the quantity of mugs (B4) from 4 to 2:

```text
PUT /api/sheets/demo/cells/B4
{"input": "2"}
```

```json
{
  "changed": [
    {
      "ref": "B4",
      "input": "2",
      "value": {
        "type": "number",
        "display": "2",
        "number": 2
      }
    },
    {
      "ref": "D4",
      "input": "=B4*C4",
      "value": {
        "type": "number",
        "display": "36",
        "number": 36
      }
    },
    {
      "ref": "D6",
      "input": "=SUM(D2:D4)",
      "value": {
        "type": "number",
        "display": "137.5",
        "number": 137.5
      }
    },
    {
      "ref": "D7",
      "input": "=-ROUND(D6*C7,2)",
      "value": {
        "type": "number",
        "display": "-13.75",
        "number": -13.75
      }
    },
    {
      "ref": "D8",
      "input": "=D6+D7",
      "value": {
        "type": "number",
        "display": "123.75",
        "number": 123.75
      }
    },
    {
      "ref": "D10",
      "input": "=IF(D8>D9,\"over by \"&ROUND(D8-D9,2),\"within budget\")",
      "value": {
        "type": "text",
        "display": "within budget",
        "number": null
      }
    }
  ],
  "recalculated": [
    "D4",
    "D6",
    "D7",
    "D8",
    "D10"
  ],
  "inCycle": []
}
```

The 5 formulas that depend on B4 ran in order. The other 2 formulas on the sheet (D2 and D3) did not run, because B4 does not reach them.

## Speed

1 run each, measured in a Release build on a 2-core cloud machine (Intel Xeon at 2.10 GHz), so the times vary from run to run:

| Work | Time (ms) |
|---|---|
| Load a chain of 100,000 formulas (A2 is `=A1+1`, and so on) | 839 |
| Change A1, which re-calculates all 99,999 formulas | 142 to 333 |
| Change A99999, which re-calculates 1 formula | 2.1 |

Repeatable benchmarks are item 2 in [ROADMAP.md](ROADMAP.md).

## Limits today

- The sheets live in memory, so a restart loses them. Saving them to a database is item 1 in [ROADMAP.md](ROADMAP.md).
- The `$` signs in a reference are read and ignored, because there is no copy or fill yet.
- There is no sign-in. Anyone who can reach the API can change any sheet.
- The grid screen shows 8 columns and 20 rows.
- It has been built and tested on Linux. CI also runs the tests on Windows and macOS.

## Licence

MIT Licence. See [LICENSE](LICENSE).
