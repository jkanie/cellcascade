# Roadmap

Planned work after the seed, with an hour estimate per item. The total is 24.5 hours.

## 1. Ranges as 1 dependency, 3 hours

- What: store a range such as A1:A100000 as 1 entry in an index of rectangles, instead of 1 dependency per cell. A look-up in that index then finds the formulas that read a cell.
- Why: today, each cell of a range is its own entry in the graph. That caps a range at 100,000 cells. A few wide SUM ranges also make a sheet slow to load.

## 2. Conditional sums and newer lookups, 3 hours

- What: SUMIF, COUNTIF, AVERAGEIF, INDEX, MATCH, XLOOKUP, with tests against results worked out by hand.
- Why: budgets and price sheets need sums with a condition and lookups to the left, which VLOOKUP cannot do.

## 3. Dates and number formats, 3 hours

- What: dates as serial numbers (as in Excel), DATE, TODAY, YEAR, MONTH, DAY, plus a display format per cell (percent, currency, date).
- Why: today, 10% shows as 0.1. There is also no way to work with dates.

## 4. Copy and fill with relative references, 2 hours

- What: an API call that copies a cell or a range to another place and shifts the references that have no `$`, such that `=A1+$B$1` copied 1 row down becomes `=A2+$B$1`.
- Why: today, the `$` signs are read and ignored. Every formula of a column therefore has to be typed 1 at a time.

## 5. Live updates for several viewers, 2.5 hours

- What: a SignalR hub that sends the changed cells of each edit to every grid screen that shows the same sheet.
- Why: today, 2 people on 1 sheet see each other's changes only after a re-load.

## 6. xlsx import and export, 3 hours

- What: read the values and formulas of an .xlsx file with the Open XML SDK. Write a sheet back to .xlsx. A test sends a file both ways.
- Why: the sheets that people already have live in Excel files. Typing them in again takes too long.

## 7. End-to-end tests of the grid screen, 2 hours

- What: Playwright tests that load the sample sheet, set a cell, check the cascade on screen, check a refused formula, run in CI.
- Why: the API tests cover the answers, but only a test in a browser proves that the screen shows them.

## 8. Release v0.1.0, 2 hours

- What: a change log, a tag, the engine as a NuGet package, a Docker image of the API.
- Why: another .NET app can then use the engine with 1 package reference. The API can also run with 1 command.

## 9. Find and replace inside text, 2 hours

- What: FIND (case-sensitive), SEARCH (case-insensitive, with ? and * as wild-cards), SUBSTITUTE (with its optional instance number), REPLACE, each with Excel's results for a start before 1 or past the end, checked against Microsoft's documented examples.
- Why: LEFT, RIGHT, MID take text apart only at fixed places today. A formula cannot yet find where a word starts or swap 1 word for another.

## 10. Undo and redo in the grid, 2 hours

- What: an undo list of cell changes in the page (Ctrl+Z, Ctrl+Y), each step sent back to the API as an ordinary cell change, such that the cascade follows.
- Why: a cell written by mistake can only be fixed by typing its old content again today.

## Total

24.5 hours.
