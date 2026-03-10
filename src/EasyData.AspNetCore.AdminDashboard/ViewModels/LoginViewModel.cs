namespace EasyData.AspNetCore.AdminDashboard.ViewModels
{
    public class LoginViewModel
    {
        public string Title { get; set; }
        public string BasePath { get; set; }
        public string ErrorMessage { get; set; }
        public string NextUrl { get; set; }
        public bool EnableSaml { get; set; }
    }
}
