using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using AstaLegheFC.Models;
using Microsoft.Extensions.Configuration;

namespace AstaLegheFC.Services
{
    public interface IReleaseNotesService
    {
        Task<IReadOnlyList<ReleaseNote>> GetLatestAsync(int take = 20);
    }

    public class GitHubReleaseNotesService : IReleaseNotesService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _branch;
        private readonly string? _token;

        public GitHubReleaseNotesService(IHttpClientFactory httpFactory, IConfiguration cfg)
        {
            _httpFactory = httpFactory;
            _owner = cfg["Releases:GitHub:Owner"] ?? "";
            _repo = cfg["Releases:GitHub:Repo"] ?? "";
            _branch = cfg["Releases:GitHub:Branch"] ?? "master"; // <- il tuo branch
            _token = cfg["Releases:GitHub:Token"];
        }

        public async Task<IReadOnlyList<ReleaseNote>> GetLatestAsync(int take = 20)
        {
            if (string.IsNullOrWhiteSpace(_owner) || string.IsNullOrWhiteSpace(_repo))
                return Array.Empty<ReleaseNote>();

            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AstaLegheFC", "1.0"));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrWhiteSpace(_token))
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            // Paginiamo: fino a 3 pagine x 100 = ultimi 300 commit del branch
            var all = new List<(string sha, string title, string body, string author, DateTimeOffset date, string htmlUrl)>();
            for (int page = 1; page <= 3; page++)
            {
                var url = $"https://api.github.com/repos/{_owner}/{_repo}/commits?sha={_branch}&per_page=100&page={page}";
                var res = await http.GetAsync(url);
                if (!res.IsSuccessStatusCode) break;

                using var stream = await res.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var arr = doc.RootElement;

                if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) break;

                foreach (var item in arr.EnumerateArray())
                {
                    var sha = item.GetProperty("sha").GetString() ?? "";
                    var commit = item.GetProperty("commit");
                    var message = commit.GetProperty("message").GetString() ?? "";
                    var author = commit.GetProperty("author").GetProperty("name").GetString() ?? "";
                    var dateIso = commit.GetProperty("author").GetProperty("date").GetDateTimeOffset();

                    var lines = message.Replace("\r\n", "\n").Split('\n');
                    var title = (lines.FirstOrDefault() ?? "(no message)").Trim();
                    var body = string.Join("\n", lines.Skip(1)).Trim();

                    var htmlUrl = item.TryGetProperty("html_url", out var html) ? (html.GetString() ?? "")
                                  : $"https://github.com/{_owner}/{_repo}/commit/{sha}";

                    all.Add((sha, title, body, author, dateIso, htmlUrl));
                }
            }

            if (all.Count == 0) return Array.Empty<ReleaseNote>();

            // Ordiniamo per data DESC (dal più recente), assegnando versione progressiva per giorno
            var perDayCounter = new Dictionary<string, int>();
            var ordered = all.OrderByDescending(x => x.date).Take(300).ToList();
            var result = new List<ReleaseNote>(take);

            foreach (var x in ordered)
            {
                var dateKey = x.date.ToUniversalTime().ToString("yyyy.MM.dd");
                if (!perDayCounter.ContainsKey(dateKey)) perDayCounter[dateKey] = 0;
                perDayCounter[dateKey] += 1;
                var nn = perDayCounter[dateKey];

                var version = $"v{dateKey}.{nn:D2}";
                result.Add(new ReleaseNote
                {
                    Version = version,
                    Title = x.title,
                    Body = x.body,
                    CommitSha = x.sha,
                    Date = x.date,
                    Author = x.author,
                    HtmlUrl = x.htmlUrl
                });

                if (result.Count >= take) break;
            }

            return result;
        }
    }
}
