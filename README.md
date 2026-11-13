# cellcascade

A spreadsheet formula engine for .NET, with an HTTP API and a small grid screen. You set cells to values or Excel-style formulas. Each change re-calculates only the cells that depend on it, in an order where every cell comes after the cells it reads.

## What it does

- Reads formulas with Excel's operator order: `=-2^2` is 4 and `=2^3^2` is 64, as in Excel.
- Supports numbers, text, TRUE and FALSE, cell references (`A1`, `$B$2`), ranges (`A1:C10`). It has 36 functions: ABS, AND, AVERAGE, CONCAT, COUNT, COUNTA, FIND, IF, IFERROR, INT, LEFT, LEN, LOWER, MAX, MID, MIN, MOD, NOT, OR, POWER, PRODUCT, PROPER, REPLACE, REPT, RIGHT, ROUND, ROUNDDOWN, ROUNDUP, SEARCH, SIGN, SQRT, SUBSTITUTE, SUM, TRIM, UPPER, VLOOKUP.
- Keeps a dependency graph of the sheet. A change re-calculates the changed cells' dependents (directly or through other cells), each 1 time. Every other cell stays as it was.
- Finds circular references with Tarjan's algorithm and marks every cell in a cycle `#CYCLE!`. A cell that reads a cycle gets the error too, unless IFERROR catches it.
- Gives Excel's error values: `#DIV/0!`, `#VALUE!`, `#REF!`, `#NAME?`, `#NUM!`, `#N/A`.
- Answers every edit with the cells that changed and the cells that were re-calculated, in order. The grid screen flashes those cells, so the cascade is visible.
- Refuses a formula that cannot be read with a reason and a position (`D6: A cell address must follow ':' at position 8.`). The sheet then stays as it was.
- Saves each sheet's inputs in SQLite (the default) or PostgreSQL through Entity Framework Core. The values are not saved -- they are re-calculated when the app loads the sheets, so a re-start gives the same values.

## Who it is for

.NET developers who need spreadsheet formulas on a server: price sheets, budget calculators, business rules that the people who use them can write as formulas, without Excel on the server. The engine is a plain class library with no dependencies. The API shows 1 way to host it.

## Built with

- C# on .NET 10 (SDK 10.0.114, ASP.NET Core 10.0.14 runtime). The engine uses only the .NET base library.
- ASP.NET Core minimal APIs for the HTTP API.
- Entity Framework Core 10.0.14 to save the sheets: Microsoft.EntityFrameworkCore.Sqlite 10.0.14 for SQLite, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 for PostgreSQL.
- A grid screen in plain HTML, CSS, JavaScript (no build step), served by the API.
- Tests: xUnit v3 4.0.3 on Microsoft.Testing.Platform, plus Microsoft.AspNetCore.Mvc.Testing 10.0.14 for the API tests.
- Benchmarks: BenchmarkDotNet 0.15.8 (see [Speed](#speed)).

## Set-up

You need the .NET 10 SDK.

```sh
dotnet run --project src/CellCascade.Api --urls http://localhost:5080
```

Then open http://localhost:5080 for the grid screen, or call the API at `http://localhost:5080/api`. With no set-up, the app saves the sheets in the SQLite file `src/CellCascade.Api/cellcascade.db`, which it creates on the 1st start. A new database starts with a sample sheet called `demo`: a made-up budget with 3 lines, a 10% discount, a total, a check against the budget.

## Database

The app saves 1 row per sheet (its name) and 1 row per cell that is not blank (the sheet name, the cell address, the input). It does not save values. When the app starts, it loads every sheet from its inputs, which re-calculates every formula. The sheets then stay in memory, and each edit is saved before the answer goes out. When a save fails, the sheet gets its old inputs back and the request fails.

The settings live in `src/CellCascade.Api/appsettings.json`:

| Setting | Default | What it does |
|---|---|---|
| `Database:Provider` | `Sqlite` | `Sqlite` or `PostgreSql`. Any other value stops the start with an error. |
| `Database:SqlitePath` | `cellcascade.db` | The SQLite file. A relative path starts at the content root, which is `src/CellCascade.Api` under `dotnet run --project src/CellCascade.Api`. |
| `ConnectionStrings:PostgreSql` | empty | The Npgsql connection string. It is needed when the provider is `PostgreSql`, and the start stops with an error without it. |

Environment variables override the file, with `__` in place of `:`. To run on PostgreSQL:

```sh
Database__Provider=PostgreSql \
ConnectionStrings__PostgreSql="Host=localhost;Database=cellcascade;Username=postgres;Password=postgres" \
dotnet run --project src/CellCascade.Api --urls http://localhost:5080
```

When the database in the connection string does not exist, the app creates it, so the login needs the right to create a database. This was checked by hand on PostgreSQL 17.11: the app created the database, saved an edit to `demo`, then showed the edited values after a re-start.

The app makes its tables with EF Core's `EnsureCreated`, not with migrations:

- EF Core makes migrations for 1 provider at a time. SQLite plus PostgreSQL would need 2 sets of migrations, with a new migration in each set for every change to the model ([EF Core docs](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers)).
- The schema is 2 small tables, `Sheets` and `Cells`, with nothing to migrate yet.
- The cost: `EnsureCreated` makes the tables only in a database that has no tables at all, and it never changes tables that exist. A later change to the schema needs migrations, and the simplest move from `EnsureCreated` to migrations starts from a new database ([EF Core docs](https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created)). For the same reason, give the app a database of its own.

The sample sheet is written only when the app makes the tables, so a deleted `demo` sheet stays deleted after a re-start.

## Tests

```sh
dotnet format --verify-no-changes
dotnet test
```

224 tests, of which 1 is skipped unless a PostgreSQL server is given (see below):

- The oracle test applies 12,000 random edits (300 seeded random sheets of 25 cells, 40 edits each) and checks the engine after every edit against a slow reference sheet in `tests/CellCascade.Tests/ReferenceSheet.cs`. The reference keeps no graph: it works out every value again from scratch, with its own walk for references and its own cycle check. The test compares every value, the exact set of cells re-calculated, their order, the cells in a cycle, the cells that changed. The random formulas cover arithmetic, comparison, `&`, ranges, lookups, IF branches, IFERROR, cycles (more than 500 of the edits make 1).
- Another test applies 100 random batches in 2 orders and requires the same values.
- Unit tests pin the operator order, the type rules, every function (with Excel's results for cases such as `ROUND(1.005,2)`, which is 1.01), the input rules, the parse faults, a chain of 100,000 cells, a cycle of 100,000 cells.
- API tests start the app in memory, each app on a SQLite file of its own in the temp folder, and cover the sample sheet, the cascade answer, batches, cycles, a cell's precedents and dependents, refusals (400 and 404).
- Database tests (`tests/CellCascade.Tests/PersistenceTests.cs`) run on an in-memory SQLite connection and on temp files. They check that a 2nd store on the same database loads every sheet with the same inputs and values, that only inputs are saved, that values are re-calculated from the inputs on load (an input changed in the database shows up in the values), that a cleared cell loses its row, that a refused formula saves nothing, that a failed save puts the old inputs back in the sheet, that sheet names ignore case, that the settings pick the right database, that 2 starts of the app on 1 file give the same answers, that a deleted sheet stays deleted.

The PostgreSQL test runs the same checks as the 1st database test on a PostgreSQL server. It needs a connection string in `CELLCASCADE_TEST_POSTGRESQL`, makes a database with a random name, then drops it. CI does not set the variable, so CI skips the test. It passed against PostgreSQL 17.11:

```sh
CELLCASCADE_TEST_POSTGRESQL="Host=localhost;Username=postgres;Password=postgres" dotnet test
```

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
- ROUND rounds half away from 0. ROUNDUP rounds away from 0 and ROUNDDOWN toward 0, as in Excel. INT rounds down to the next whole number, so INT(-8.9) is -9. MOD has the sign of the divisor, so MOD(-3,2) is 1. PRODUCT multiplies the numbers in its ranges, and skips the text, empty cells, logical values in them, as Microsoft documents. With no number at all it gives 0, as MIN and MAX do here. SIGN gives 1 for a number above 0, 0 for 0, -1 for a number below 0. SQRT of a number below 0 is #NUM!. POWER(number, power) gives the same as `number^power`, 0^0 being #NUM!. LEFT, RIGHT, MID count characters from 1 and stop at the end of the text. MID with a start past the end gives empty text. TRIM removes the space character 32 only, so a non-breaking space stays. FIND counts case and reads ? and * as plain characters. As Microsoft documents, it gives #VALUE! when the text is not found or when its start is below 1 or past the end. SUBSTITUTE counts case too. With an instance number it changes only that occurrence, and an instance below 1 gives #VALUE!. SEARCH is FIND without case, with ? for any 1 character, * for any run of characters, and ~ before ? or * to match that character itself. REPLACE swaps a span given by start and length. A start past the end adds the new text at the end, and a start below 1 or a negative length gives #VALUE!. PROPER makes each letter that follows a non-letter upper case and every other letter lower case, as Microsoft documents it. REPT gives #VALUE! for a result longer than 32,767 characters (Microsoft's limit) and for a negative count. VLOOKUP looks up approximately unless its 4th argument is FALSE.
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
| DELETE | `/api/sheets/{name}` | Removes a sheet and its saved cells. |
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

The numbers come from BenchmarkDotNet 0.15.8 (its default job: warm-up, then repeated runs until the result is stable) in `benchmarks/CellCascade.Benchmarks`. They were measured on 2026-11-09 on a 2-core cloud machine (Intel Xeon at 2.30 GHz, Ubuntu 24.04, .NET 10.0.14), so another machine gives other numbers. Compare runs made on the same machine.

| Benchmark | Mean | Standard deviation | Allocated |
|---|---|---|---|
| Load a chain of 100,000 formulas (A2 is `=A1+1`, and so on) | 272.0 ms | 10.8 ms | 198.1 MB |
| Change A1, which re-calculates all 99,999 formulas | 86.0 ms | 5.7 ms | 81.6 MB |
| Change A99999, which re-calculates 1 formula | 0.90 µs | 0.01 µs | 3.1 KB |
| Change A5000 under 100 formulas that each read `=SUM(A1:A10000)` | 25.9 ms | 0.4 ms | 25.1 MB |

Each change sets a new number, so every run of it does the same work. Before any timing starts, each benchmark checks that its change re-calculates the number of formulas its name gives, and stops with an error if not.

Run them in a Release build:

```sh
dotnet run -c Release --project benchmarks/CellCascade.Benchmarks -- --filter '*'
```

`--filter '*Chain*'` runs the chain only. BenchmarkDotNet writes Markdown, CSV, HTML reports to `BenchmarkDotNet.Artifacts/results`, which Git ignores. CI builds the benchmark project but does not run it, because the times of a shared CI machine vary too much to compare.

## Limits today

- The app reads the database only when it starts. 2 copies of the app on 1 database do not see each other's edits, so run 1 copy per database.
- Every sheet stays in memory, so the memory use and the start time grow with the number of cells.
- The `$` signs in a reference are read and ignored, because there is no copy or fill yet.
- There is no sign-in. Anyone who can reach the API can change any sheet.
- The grid screen shows 8 columns and 20 rows.
- It has been built and tested on Linux. CI also runs the tests on Windows and macOS.

## Licence

MIT Licence. See [LICENSE](LICENSE).
