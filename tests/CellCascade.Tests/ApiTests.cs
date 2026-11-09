using System.Net;
using System.Net.Http.Json;
using CellCascade.Api;

namespace CellCascade.Tests;

public sealed class ApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;

    public ApiTests(ApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        client = factory.CreateClient();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Serves_the_sample_sheet()
    {
        var sheet = await client.GetFromJsonAsync<SheetDto>("/api/sheets/demo", Token);
        Assert.NotNull(sheet);
        var cells = sheet.Cells.ToDictionary(c => c.Ref);
        Assert.Equal("156.15", cells["D8"].Value.Display);
        Assert.Equal("over by 6.15", cells["D10"].Value.Display);
        Assert.Equal("=SUM(D2:D4)", cells["D6"].Input);
        Assert.Equal(SheetStore.Sample.Count, sheet.Cells.Count);
    }

    [Fact]
    public async Task An_edit_answers_with_the_cascade_it_caused()
    {
        // A fresh app with its own database, such that this edit does not change the sample sheet for the other tests.
        await using var factory = new ApiFactory();
        using var own = factory.CreateClient();
        var answer = await own.PutAsJsonAsync("/api/sheets/demo/cells/B4", new CellEdit("2"), Token);
        answer.EnsureSuccessStatusCode();
        var result = await answer.Content.ReadFromJsonAsync<EditResultDto>(Token);
        Assert.NotNull(result);
        Assert.Equal(["D4", "D6", "D7", "D8", "D10"], result.Recalculated);
        var changed = result.Changed.ToDictionary(c => c.Ref);
        Assert.Equal(["B4", "D4", "D6", "D7", "D8", "D10"], result.Changed.Select(c => c.Ref));
        Assert.Equal(36, changed["D4"].Value.Number);
        Assert.Equal("123.75", changed["D8"].Value.Display);
        Assert.Equal("within budget", changed["D10"].Value.Display);
    }

    [Fact]
    public async Task A_batch_creates_a_sheet_and_reports_a_cycle()
    {
        var batch = new CellBatch(new Dictionary<string, string?> { ["A1"] = "=B1", ["B1"] = "=A1", ["C1"] = "=A1" });
        var answer = await client.PostAsJsonAsync("/api/sheets/loop/cells", batch, Token);
        answer.EnsureSuccessStatusCode();
        var result = await answer.Content.ReadFromJsonAsync<EditResultDto>(Token);
        Assert.NotNull(result);
        Assert.Equal(["A1", "B1"], result.InCycle);
        Assert.Equal(["C1"], result.Recalculated);
        Assert.Equal("#CYCLE!", result.Changed.Single(c => c.Ref == "C1").Value.Display);
    }

    [Fact]
    public async Task Explains_a_cell()
    {
        await client.PostAsJsonAsync("/api/sheets/explain/cells", new CellBatch(new() { ["A1"] = "1", ["B1"] = "=A1*2", ["C1"] = "=B1" }), Token);
        var cell = await client.GetFromJsonAsync<CellDetailDto>("/api/sheets/explain/cells/b1", Token);
        Assert.NotNull(cell);
        Assert.Equal("B1", cell.Ref);
        Assert.Equal("=A1*2", cell.Input);
        Assert.Equal(["A1"], cell.Precedents);
        Assert.Equal(["C1"], cell.Dependents);
        Assert.Equal("number", cell.Value.Type);
    }

    [Theory]
    [InlineData("/api/sheets/bad/cells/B1", "=1+", "B1: ")]
    [InlineData("/api/sheets/bad/cells/1B", "1", "'1B' is not a cell address")]
    [InlineData("/api/sheets/bad%20name/cells/A1", "1", "A sheet name has")]
    public async Task Refuses_bad_input_with_a_reason(string url, string input, string reason)
    {
        var answer = await client.PutAsJsonAsync(url, new CellEdit(input), Token);
        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        var error = await answer.Content.ReadFromJsonAsync<ErrorDto>(Token);
        Assert.NotNull(error);
        Assert.Contains(reason, error.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Answers_404_for_a_sheet_that_does_not_exist_and_deletes_one_that_does()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/sheets/nothing", Token)).StatusCode);
        await client.PutAsJsonAsync("/api/sheets/gone/cells/A1", new CellEdit("1"), Token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/sheets/gone", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/sheets/gone", Token)).StatusCode);
    }

    [Fact]
    public async Task Serves_the_grid_screen()
    {
        var page = await client.GetStringAsync("/", Token);
        Assert.Contains("<title>cellcascade</title>", page, StringComparison.Ordinal);
        Assert.Contains("SUM", await client.GetStringAsync("/api/functions", Token), StringComparison.Ordinal);
    }
}
