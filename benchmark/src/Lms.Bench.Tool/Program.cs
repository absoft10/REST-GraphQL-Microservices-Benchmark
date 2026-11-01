using System.Globalization;
using System.Text;
using System.Text.Json;
using Lms.Data;
using Lms.Data.Dataset;
using Lms.RequiredBytes;

// lms-bench — database lifecycle and equivalence verification (local only; never targets AWS unless a
// connection template is supplied explicitly by the researcher).
return await Cli.RunAsync(args);

internal static class Cli
{
    private const string Usage = """
        lms-bench db init       [--server S] [--prefix lms_]
        lms-bench db seed       [--server S] [--prefix lms_] [--label L] [--seed N] [--books-fixture N] [--members N]
                                [--target-members M] [--loans-per-target L] [--max-loans-other N] --out DIR
        lms-bench db verify     [--server S] [--prefix lms_] --manifest DIR/manifest.json [--out FILE]
        lms-bench db reset      [--server S] [--prefix lms_]      (drop + init; DESTRUCTIVE for the three named DBs)
        lms-bench db drop       [--server S] [--prefix lms_]
        lms-bench equivalence   --rest URL --graphql URL [--server S] [--prefix lms_]
                                [--flat-limit 100] [--nested-limit 10] [--offset 0] --out DIR
        Defaults: --server "(localdb)\MSSQLLocalDB", --prefix lms_. Env LMS_SQL_CONNECTION_TEMPLATE (with {database}) overrides auth.
        Books: only the TEMPORARY fixture is available until the Kaggle dataset is verified (DECISION-004, O-7).
        """;

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var (verb, opts) = Parse(args);
            var dbs = new DatabaseSet(Opt(opts, "server", DatabaseSet.LocalDbServer), Opt(opts, "prefix", "lms_"),
                Environment.GetEnvironmentVariable("LMS_SQL_CONNECTION_TEMPLATE"));
            switch (verb)
            {
                case "db init": await DdlRunner.InitializeAsync(dbs); Console.WriteLine($"Initialised {dbs.CatalogDb}, {dbs.MembersDb}, {dbs.LoansDb}."); return 0;
                case "db drop": await DdlRunner.DropAsync(dbs); Console.WriteLine("Dropped."); return 0;
                case "db reset": await DdlRunner.DropAsync(dbs); await DdlRunner.InitializeAsync(dbs); Console.WriteLine("Reset complete."); return 0;
                case "db seed": return await SeedAsync(dbs, opts);
                case "db verify": return await VerifyAsync(dbs, opts);
                case "equivalence": return await EquivalenceAsync(dbs, opts);
                default: Console.Error.WriteLine(Usage); return 2;
            }
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(Usage);
            return 2;
        }
    }

    private static async Task<int> SeedAsync(DatabaseSet dbs, Dictionary<string, string> o)
    {
        if (o.TryGetValue("books-source", out var src) && src != "fixture")
            throw new ArgumentException("Only --books-source fixture is available: the Kaggle dataset has not been verified (O-7).");
        var options = new GeneratorOptions
        {
            Seed = ulong.Parse(Opt(o, "seed", "20261101"), CultureInfo.InvariantCulture),
            FixtureBookCount = Int(o, "books-fixture", 2000),
            MemberCount = Int(o, "members", 10000),
            TargetMembers = Int(o, "target-members", 10),
            LoansPerTarget = Int(o, "loans-per-target", 5),
            MaxLoansOtherMembers = Int(o, "max-loans-other", 20),
        };
        var label = Opt(o, "label", "local-fixture");
        var outDir = Required(o, "out");
        var data = DatasetGenerator.Generate(options);
        var manifest = DatasetArtifacts.Write(data, label, outDir);
        await DdlRunner.InitializeAsync(dbs);
        await DatasetStore.LoadAsync(dbs, data, manifest);
        Console.WriteLine($"Seeded label={label} books_source={data.BooksSource} seed={options.Seed}");
        foreach (var t in manifest.Tables) Console.WriteLine($"  {t.Table,-16} rows={t.RowCount,7} canonical_sha256={t.CanonicalSha256}");
        Console.WriteLine($"Manifest: {Path.GetFullPath(Path.Combine(outDir, "manifest.json"))}");
        return 0;
    }

    private static async Task<int> VerifyAsync(DatabaseSet dbs, Dictionary<string, string> o)
    {
        var manifest = DatasetArtifacts.ReadManifest(Required(o, "manifest"));
        var report = await DataFreezeVerifier.VerifyAsync(dbs, manifest);
        foreach (var c in report.Checks) Console.WriteLine($"{(c.Passed ? "PASS" : "FAIL")}  {c.Name}  {c.Detail}");
        if (o.TryGetValue("out", out var outFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
            await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(report, DatasetManifest.Json), new UTF8Encoding(false));
            Console.WriteLine($"Report: {Path.GetFullPath(outFile)}");
        }
        Console.WriteLine(report.Passed ? "DATA FREEZE VERIFICATION: PASS" : "DATA FREEZE VERIFICATION: FAIL");
        return report.Passed ? 0 : 1;
    }

    private static async Task<int> EquivalenceAsync(DatabaseSet dbs, Dictionary<string, string> o)
    {
        using var rest = new HttpClient { BaseAddress = new Uri(Required(o, "rest")) };
        using var gql = new HttpClient { BaseAddress = new Uri(Required(o, "graphql")) };
        var books = await DatasetStore.ReadBooksAsync(dbs);
        var members = await DatasetStore.ReadMembersAsync(dbs);
        var loans = await DatasetStore.ReadLoansAsync(dbs);
        var info = await DatasetStore.ReadDatasetInfoAsync(dbs.CatalogConnectionString);
        var result = await EquivalenceRunner.RunAsync(rest, gql, books, members, loans,
            Int(o, "flat-limit", 100), Int(o, "nested-limit", 10), Int(o, "offset", 0));
        var outDir = Required(o, "out");
        EquivalenceReport.Write(result, outDir, info.GetValueOrDefault("books_source", "unknown"));
        foreach (var c in result.Configs)
            Console.WriteLine($"{(c.Failure is null && c.EquivalentToOracle ? "PASS" : "FAIL")}  {ConfigNames.Cell(c.Config),-5} " +
                              $"required={c.Bytes.RequiredBytes} actual={c.Bytes.ActualBytes} overfetch={c.Bytes.OverFetch:P2} {c.Failure}");
        Console.WriteLine(result.Passed ? "EQUIVALENCE: PASS" : "EQUIVALENCE: FAIL");
        return result.Passed ? 0 : 1;
    }

    private static (string, Dictionary<string, string>) Parse(string[] args)
    {
        var words = args.TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var opts = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = words.Length; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length) throw new ArgumentException($"Bad option near '{args[i]}'.");
            opts[args[i][2..]] = args[++i];
        }
        return (string.Join(' ', words), opts);
    }

    private static string Opt(Dictionary<string, string> o, string k, string d) => o.TryGetValue(k, out var v) ? v : d;
    private static string Required(Dictionary<string, string> o, string k) => o.TryGetValue(k, out var v) ? v : throw new ArgumentException($"--{k} is required.");
    private static int Int(Dictionary<string, string> o, string k, int d) => o.TryGetValue(k, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : d;
}

/// <summary>Writes C-6 key lists, bytes_summary.csv, report.json and raw sample exchanges.</summary>
internal static class EquivalenceReport
{
    public static void Write(EquivalenceResult r, string dir, string booksSource)
    {
        Directory.CreateDirectory(Path.Combine(dir, "samples"));
        var utf8 = new UTF8Encoding(false);
        var banner = $"# books_source={booksSource} (TEMPORARY fixture data is NOT research data)\n";

        var csv = new StringBuilder("workload,config,required_bytes,actual_bytes,overfetch,envelope,nonrequired,formatting,multicall,calls,equivalent,compact\n");
        foreach (var c in r.Configs)
        {
            var b = c.Bytes;
            csv.Append(CultureInfo.InvariantCulture, $"{b.Workload},{b.Config},{b.RequiredBytes},{b.ActualBytes},{b.OverFetch:F6},{b.Envelope},{b.NonRequired},{b.Formatting},{b.MultiCall},{b.Calls},{c.EquivalentToOracle},{c.AllCompact}\n");
            for (var i = 0; i < c.Exchanges.Count; i++)
            {
                var ex = c.Exchanges[i];
                var stem = Path.Combine(dir, "samples", ConfigNames.Cell(c.Config) + (c.Exchanges.Count > 1 ? $"_call{i + 1}" : ""));
                File.WriteAllText(stem + ".response.json", ex.Body, utf8);
                File.WriteAllText(stem + ".exchange.txt",
                    $"{ex.Method} {ex.Url}\n{(ex.RequestBody is null ? "" : ex.RequestBody + "\n")}\nHTTP {ex.Status}\n" +
                    string.Join('\n', ex.Headers.OrderBy(h => h.Key, StringComparer.Ordinal).Select(h => $"{h.Key}: {h.Value}")) + "\n", utf8);
            }
        }
        File.WriteAllText(Path.Combine(dir, "bytes_summary.csv"), csv.ToString(), utf8);

        foreach (var (wl, configs) in new[] { ("flat", new[] { ConfigId.FR, ConfigId.FG }), ("nested", new[] { ConfigId.NRC, ConfigId.NRM, ConfigId.NG }) })
        {
            var keys = new StringBuilder(banner);
            foreach (var id in configs)
            {
                var c = r.Configs.Single(x => x.Config == id);
                keys.Append(ConfigNames.Cell(id)).Append('\t').Append(Keys(c.NormalizedJcs, wl)).Append('\n');
            }
            File.WriteAllText(Path.Combine(dir, $"{wl}_keys.txt"), keys.ToString(), utf8);
        }
        File.WriteAllText(Path.Combine(dir, "crir_flat.jcs.json"), r.FlatOracleJcs, utf8);
        File.WriteAllText(Path.Combine(dir, "crir_nested.jcs.json"), r.NestedOracleJcs, utf8);
        File.WriteAllText(Path.Combine(dir, "report.json"), JsonSerializer.Serialize(new
        {
            booksSource, passed = r.Passed, r.FlatRequiredBytes, r.NestedRequiredBytes,
            configs = r.Configs.Select(c => new { config = ConfigNames.Cell(c.Config), c.EquivalentToOracle, c.AllCompact, c.Bytes, c.Failure }),
        }, DatasetManifest.Json), utf8);
    }

    /// <summary>C-6 key list: flat = bookIds; nested = memberId:loanId>bookId triples, in CRIR order.</summary>
    private static string Keys(string jcs, string workload)
    {
        if (string.IsNullOrEmpty(jcs)) return "<FAILED>";
        var arr = System.Text.Json.Nodes.JsonNode.Parse(jcs)!.AsArray();
        return workload == "flat"
            ? string.Join(',', arr.Select(b => b!["bookId"]!.ToJsonString()))
            : string.Join(',', arr.SelectMany(m => m!["loans"]!.AsArray().Select(l =>
                $"{m["memberId"]!.ToJsonString()}:{l!["loanId"]!.ToJsonString()}>{l["book"]!["bookId"]!.ToJsonString()}")));
    }
}
