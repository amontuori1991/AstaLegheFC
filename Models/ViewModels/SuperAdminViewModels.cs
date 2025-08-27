using System;
using System.Collections.Generic;
using AstaLegheFC.Models;

namespace AstaLegheFC.Models.ViewModels
{
    public class SuperAdminIndexViewModel
    {
        public List<ApplicationUser> Active { get; set; } = new();
        public List<ApplicationUser> Expiring { get; set; } = new();
        public List<ApplicationUser> Expired { get; set; } = new();
        public DateTime TodayUtcDate { get; set; }
    }

    public class SuperAdminLoginViewModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string ReturnUrl { get; set; }
    }
}
