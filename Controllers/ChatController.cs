
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IntentTrainer intentTrainer;
    private readonly AppDbContext _db;

    public ChatController(AppDbContext db)
    {
        _db = db;
        intentTrainer = new IntentTrainer();
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest req)
    {
        try
        {
            if (req == null || string.IsNullOrEmpty(req.Message))
                return BadRequest("Pesan tidak boleh kosong");

            var prediction = intentTrainer.PredictWithScore(req.Message.ToLower());
            string intent = prediction.intent;
            float score = prediction.score;

            if (string.IsNullOrEmpty(intent) || score < 0.8f || intent == "unknown")
            {
                return Ok(new { reply = await GetResponseFromGemini(req.Message) });
            }

            return Ok(new { reply = await HandleIntent(intent, req.Message) });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error: {ex.Message}");
        }

    }

    private async Task<string> HandleIntent(string intent, string userMessage)
    {
        try
        {
            if (intent == "pengeluaran" || intent == "pemasukan")
            {
                int nominal = ExtractAmount(userMessage);
                if (nominal <= 0)
                    return "Nominalnya berapa?";

                _db.Transactions.Add(new Transaction
                {
                    Type = intent,
                    Amount = nominal
                });
                await _db.SaveChangesAsync();

                return $"Oke, saya catat {intent} sebesar Rp{nominal}";
            }
            if (intent == "summary_pengeluaran")
            {
                var (start, end) = ParsePeriode(userMessage);

                var query = _db.Transactions.Where(t => t.Type == "pengeluaran");

                if (start.HasValue && end.HasValue)
                {
                    query = query.Where(t => t.CreatedAt >= start && t.CreatedAt <= end);
                }

                int total = await query.SumAsync(t => t.Amount);
                return $"Total pengeluaran kamu: Rp{total:N0}";
            }

            if (intent == "summary_pemasukan")
            {
                var (start, end) = ParsePeriode(userMessage);

                var query = _db.Transactions.Where(t => t.Type == "pemasukan");

                if (start.HasValue && end.HasValue)
                {
                    query = query.Where(t => t.CreatedAt >= start && t.CreatedAt <= end);
                }

                int total = await query.SumAsync(t => t.Amount);
                return $"Total pemasukan kamu: Rp{total:N0}";
            }

            return "Maaf, saya belum bisa memproses itu.";
        }
        catch (Exception ex)
        {
            return $"Terjadi kesalahan: {ex.Message}";
        }

    }



    private int ExtractAmount(string msg)
    {
        var digits = Regex.Matches(msg, @"\d+")
                          .Select(m => int.Parse(m.Value))
                          .ToList();

        return digits.Any() ? digits.Max() : 0;
    }

    private async Task<string> GetResponseFromGemini(string message)
    {
        var apiKey = "AIzaSyD0zXzS9ooYEqP3MB-zXt6o7yaaARqJesg";
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";

        var payload = new
        {
            contents = new[] {
                new {
                    parts = new[] {
                        new {
                            text = message
                        }
                    }
                }
            }
        };

        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync(url, payload);
        var result = await response.Content.ReadFromJsonAsync<GeminiResponseWrapper>();

        var text = result?.candidates?.FirstOrDefault()?.content?.parts?.FirstOrDefault()?.text ?? "";
        return text.Trim();
    }
    
    private readonly Dictionary<string, int> _bulanMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "januari", 1 }, { "februari", 2 }, { "maret", 3 }, { "april", 4 },
        { "mei", 5 }, { "juni", 6 }, { "juli", 7 }, { "agustus", 8 },
        { "september", 9 }, { "oktober", 10 }, { "november", 11 }, { "desember", 12 },
    };

    public  (DateTime? start, DateTime? end) ParsePeriode(string input)
    {
        input = input.ToLower();

        // Hari ini
        if (Regex.IsMatch(input, @"\bhari ini\b"))
        {
            var today = DateTime.UtcNow.Date;
            return (today, today.AddDays(1).AddTicks(-1));
        }

        // Kemarin
        if (Regex.IsMatch(input, @"\bkemarin\b"))
        {
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            return (yesterday, yesterday.AddDays(1).AddTicks(-1));
        }

        // Minggu ini
        if (Regex.IsMatch(input, @"\bminggu ini\b"))
        {
            var today = DateTime.UtcNow.Date;
            var diff = (int)today.DayOfWeek;
            var start = today.AddDays(-diff);
            var end = start.AddDays(7).AddTicks(-1);
            return (start, end);
        }

        // Format tanggal 25 juni [tahun optional]
        var matchTanggal = Regex.Match(input, @"tanggal (?<tanggal>\d{1,2}) (?<bulan>\w+)( (?<tahun>\d{4}))?");
        if (matchTanggal.Success)
        {
            int tanggal = int.Parse(matchTanggal.Groups["tanggal"].Value);
            string bulanStr = matchTanggal.Groups["bulan"].Value;
            int bulan = _bulanMap.GetValueOrDefault(bulanStr, 0);
            int tahun = matchTanggal.Groups["tahun"].Success ? int.Parse(matchTanggal.Groups["tahun"].Value) : DateTime.UtcNow.Year;

            if (bulan > 0)
            {
                DateTime start = new(tahun, bulan, tanggal);
                DateTime end = start.AddDays(1).AddTicks(-1);
                return (start, end);
            }
        }

        // Format range 1-5 juli [tahun optional]
        var matchRange = Regex.Match(input, @"(?<tanggal1>\d{1,2})-(?<tanggal2>\d{1,2}) (?<bulan>\w+)( (?<tahun>\d{4}))?");
        if (matchRange.Success)
        {
            int t1 = int.Parse(matchRange.Groups["tanggal1"].Value);
            int t2 = int.Parse(matchRange.Groups["tanggal2"].Value);
            string bulanStr = matchRange.Groups["bulan"].Value;
            int bulan = _bulanMap.GetValueOrDefault(bulanStr, 0);
            int tahun = matchRange.Groups["tahun"].Success ? int.Parse(matchRange.Groups["tahun"].Value) : DateTime.UtcNow.Year;

            if (bulan > 0)
            {
                DateTime start = new DateTime(tahun, bulan, t1);
                DateTime end = new DateTime(tahun, bulan, t2).AddDays(1).AddTicks(-1);
                return (start, end);
            }
        }

        // Format: bulan april [tahun optional]
        var matchBulan = Regex.Match(input, @"bulan (?<bulan>\w+)( (?<tahun>\d{4}))?");
        if (matchBulan.Success)
        {
            string bulanStr = matchBulan.Groups["bulan"].Value;
            int bulan = _bulanMap.GetValueOrDefault(bulanStr, 0);
            int tahun = matchBulan.Groups["tahun"].Success ? int.Parse(matchBulan.Groups["tahun"].Value) : DateTime.UtcNow.Year;

            if (bulan > 0)
            {
                DateTime start = new(tahun, bulan, 1);
                DateTime end = start.AddMonths(1).AddTicks(-1);
                return (start, end);
            }
        }

        // Tidak ketemu
        return (null, null);
    }
}

public class GeminiResponseWrapper
{
    public List<Candidate>? candidates { get; set; }
}

public class Candidate
{
    public Content content { get; set; }
}

public class Content
{
    public List<Part> parts { get; set; }
}

public class Part
{
    public string text { get; set; }
}
