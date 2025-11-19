namespace Application.DTOs.Admin
{
    public class AdminDashboardStatsDto
    {
        public int TotalUsers { get; set; }
        public List<DailyCountDto> UserGrowthLast7Days { get; set; } = [];
        public int SignalsToday { get; set; }
        public List<DailyCountDto> SignalsPerDayLast7Days { get; set; } = [];
        // public int MessagesToday { get; set; } // Deferred for now - as per plan
    }
}

