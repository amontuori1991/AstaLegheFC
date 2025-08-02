using Microsoft.AspNetCore.Mvc;
using AstaLegheFC.Data;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using AstaLegheFC.Models;

namespace AstaLegheFC.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        public async Task<IActionResult> VisualizzaListone(string? nome, string? squadra, string? ruolo)
        {
            var query = _context.ListoneCalciatori.AsQueryable();

            if (!string.IsNullOrEmpty(nome))
                query = query.Where(c => c.Nome.ToLower().Contains(nome.ToLower()));

            if (!string.IsNullOrEmpty(squadra))
                query = query.Where(c => c.Squadra.ToLower().Contains(squadra.ToLower()));

            if (!string.IsNullOrEmpty(ruolo))
                query = query.Where(c => c.Ruolo == ruolo);

            var risultati = await query
                .OrderBy(c => c.Ruolo)
                .ThenBy(c => c.Nome)
                .ToListAsync();

            var ruoliDisponibili = await _context.ListoneCalciatori
                .Select(c => c.Ruolo)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            ViewBag.Nome = nome;
            ViewBag.Squadra = squadra;
            ViewBag.Ruolo = ruolo;
            ViewBag.RuoliDisponibili = ruoliDisponibili;

            return View(risultati);
        }


        [HttpGet]
        public IActionResult ImportaListone()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ImportaListone(IFormFile file)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            if (file == null || file.Length == 0)
            {
                ViewBag.Errore = "File non valido.";
                return View();
            }

            var listone = new List<CalciatoreListone>();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            using var package = new ExcelPackage(stream);
            var ws = package.Workbook.Worksheets[0];
            if (ws == null)
            {
                ViewBag.Errore = "Foglio Excel non trovato.";
                return View();
            }

            int headerRow = 2;

            var cols = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 1; c <= ws.Dimension.End.Column; c++)
            {
                var name = ws.Cells[headerRow, c].Text?.Trim();
                if (!string.IsNullOrEmpty(name) && !cols.ContainsKey(name))
                    cols[name] = c;
            }

            string[] required = { "Id", "R", "RM", "Nome", "Squadra", "Qt.A", "Qt.I", "Diff.", "Qt.A M", "Qt.I M", "Diff.M", "FVM", "FVM M" };
            var mancanti = required.Where(r => !cols.ContainsKey(r)).ToList();
            if (mancanti.Any())
            {
                ViewBag.Errore = "Colonne mancanti: " + string.Join(", ", mancanti);
                return View();
            }

            _context.ListoneCalciatori.RemoveRange(_context.ListoneCalciatori);
            await _context.SaveChangesAsync();

            for (int r = headerRow + 1; r <= ws.Dimension.End.Row; r++)
            {
                string nome = ws.Cells[r, cols["Nome"]].Text?.Trim();
                if (string.IsNullOrWhiteSpace(nome)) continue;

                int? ToInt(string s) => int.TryParse(s?.Trim(), out var v) ? v : (int?)null;

                var item = new CalciatoreListone
                {
                    IdListone = ToInt(ws.Cells[r, cols["Id"]].Text) ?? 0,
                    Nome = nome,
                    Ruolo = ws.Cells[r, cols["R"]].Text?.Trim(),
                    RuoloMantra = ws.Cells[r, cols["RM"]].Text?.Trim(),
                    Squadra = ws.Cells[r, cols["Squadra"]].Text?.Trim(),
                    QtA = ToInt(ws.Cells[r, cols["Qt.A"]].Text),
                    QtI = ToInt(ws.Cells[r, cols["Qt.I"]].Text),
                    Diff = ToInt(ws.Cells[r, cols["Diff."]].Text),
                    QtAM = ToInt(ws.Cells[r, cols["Qt.A M"]].Text),
                    QtIM = ToInt(ws.Cells[r, cols["Qt.I M"]].Text),
                    DiffM = ToInt(ws.Cells[r, cols["Diff.M"]].Text),
                    FVM = ToInt(ws.Cells[r, cols["FVM"]].Text),
                    FVMM = ToInt(ws.Cells[r, cols["FVM M"]].Text)
                };

                listone.Add(item);
            }

            await _context.ListoneCalciatori.AddRangeAsync(listone);
            await _context.SaveChangesAsync();

            ViewBag.Messaggio = $"Importazione completata: {listone.Count} calciatori importati.";
            return View();
        }
    }
}
