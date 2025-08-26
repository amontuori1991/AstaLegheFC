using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AstaLegheFC.Controllers
{
    [AllowAnonymous]
    public class PwaController : Controller
    {
        // Manifest "utente" dinamico:
        // sempre start_url = /pwa/utente (bootstrap che leggerà localStorage)
        [HttpGet("/pwa/manifest-utente")]
        public IActionResult ManifestUtente()
        {
            var manifest = new
            {
                name = "Fantabazzer Asta (Utente)",
                short_name = "Fantabazzer",
                start_url = "/pwa/utente",
                scope = "/",
                display = "standalone",
                background_color = "#2c3e50",
                theme_color = "#2c3e50",
                description = "App per l'asta del Fantabazzer (Utente).",
                icons = new[]
                {
                    new { src="/images/icons/icon-192x192.png", sizes="192x192", type="image/png" },
                    new { src="/images/icons/icon-512x512.png", sizes="512x512", type="image/png" }
                }
            };

            return Json(manifest);
        }

        // Pagina bootstrap che legge lega/nick da localStorage (o query) e ti porta su /Utente
        [HttpGet("/pwa/utente")]
        public IActionResult LaunchUtente()
        {
            return View();
        }
    }
}
