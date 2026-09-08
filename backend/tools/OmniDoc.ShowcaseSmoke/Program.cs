using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Services;
using OmniDoc.Infrastructure.Services.Ai;
using OmniDoc.Infrastructure.Services.Security;
using OmniDoc.Persistence.Contexts;

// Deliberately hard-coded to the disposable local smoke DB, never the application's database.
if (args.Length != 1) throw new ArgumentException("Usage: OmniDoc.ShowcaseSmoke <manifest-path>; requires local smoke DB on port 55439 and GEMINI_API_KEY for retrieval checks.");
const string connection = "Host=127.0.0.1;Port=55439;Database=omnidoc_showcase_smoke;Username=omnidoc;Password=smoke-only-local";
var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection, n => n.UseVector()).Options;
await using var db = new ApplicationDbContext(dbOptions);
await db.Database.MigrateAsync(); // Existing migrations only, on the isolated test database.
var settings = new ShowcaseSettings
{
    Enabled = true, SeedOnStartup = true, Password = "OmniDoc-Showcase2026!",
    UserId = Guid.Parse("b4987f7e-48cc-4ba5-a117-10ac4cbced01"), WorkspaceId = Guid.Parse("b4987f7e-48cc-4ba5-a117-10ac4cbced02"),
    CorpusPath = Path.GetFullPath(args[0])
};
var ai = new AiSettings { Provider = "Gemini", Gemini = new()
{
    ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "",
    EmbeddingModel = Environment.GetEnvironmentVariable("GEMINI_EMBEDDING_MODEL") ?? "gemini-embedding-2"
} };
var storagePath = Path.Combine(AppContext.BaseDirectory, "smoke-artifacts");
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:RootPath"] = storagePath }).Build();
var files = new LocalFileStorageService(configuration, NullLogger<LocalFileStorageService>.Instance);
ShowcaseSeeder Seeder(ApplicationDbContext context) => new(context, Options.Create(settings), Options.Create(ai), new PasswordHasher(),
    new DocumentArtifactStorage(files), files, new DocumentFormatDetector(), new PdfPigParserService(), new RecursiveTextChunkerService(), NullLogger<ShowcaseSeeder>.Instance);
var ordinaryUsers = await db.Users.CountAsync(u => u.Id != settings.UserId);
await Seeder(db).SeedAsync();
var originalHash = await db.Users.Where(u => u.Id == settings.UserId).Select(u => u.PasswordHash).SingleAsync();
await using var second = new ApplicationDbContext(dbOptions);
await Seeder(second).SeedAsync();
if (await second.Users.CountAsync(u => u.Id == settings.UserId) != 1 || await second.Documents.CountAsync() != 3 || await second.DocumentChunks.CountAsync() != 7 ||
    await second.DocumentArtifacts.CountAsync() != 6 || (await second.Users.SingleAsync(u => u.Id == settings.UserId)).PasswordHash != originalHash ||
    await second.Users.CountAsync(u => u.Id != settings.UserId) != ordinaryUsers)
    throw new InvalidOperationException($"PostgreSQL idempotency verification failed: users={await second.Users.CountAsync()}, documents={await second.Documents.CountAsync()}, chunks={await second.DocumentChunks.CountAsync()}, artifacts={await second.DocumentArtifacts.CountAsync()}.");
Console.WriteLine("PASS PostgreSQL: two independent imports, 1 showcase user, 3 documents, 6 artifacts, 7 vectors; existing users and password unchanged.");

using var client = new HttpClient { BaseAddress = new Uri("https://generativelanguage.googleapis.com/"), Timeout = TimeSpan.FromMinutes(2) };
var embedding = new GeminiEmbeddingService(new ClientFactory(client), Options.Create(ai));
var retrieval = new VectorRetrievalService(second, embedding, NullLogger<VectorRetrievalService>.Instance);
var questions = new[]
{
    (Text: "Doanh thu quý III đạt bao nhiêu tỷ đồng?", Document: "b4987f7e-48cc-4ba5-a117-10ac4cbced11", Page: 1),
    (Text: "Ai phê duyệt đề nghị mua sắm trước khi chuyển đến phòng tài chính?", Document: "b4987f7e-48cc-4ba5-a117-10ac4cbced12", Page: 1),
    (Text: "Lộ trình triển khai tháng 10, 11, 12 gồm những bước nào?", Document: "b4987f7e-48cc-4ba5-a117-10ac4cbced13", Page: 2)
};
foreach (var question in questions)
{
    var matches = await retrieval.SearchSimilarChunksAsync(settings.WorkspaceId, question.Text, topK: 4);
    if (!matches.Any(m => m.DocumentId == Guid.Parse(question.Document) && m.PageNumber == question.Page))
        throw new InvalidOperationException($"Expected citation missing: {question.Document}, page {question.Page}.");
    Console.WriteLine($"PASS retrieval: expected document/page in top 4 for {question.Document}, page {question.Page}.");
}
Console.WriteLine($"Artifacts: {storagePath}");

sealed class ClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
