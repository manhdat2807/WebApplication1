using Microsoft.AspNetCore.SignalR;

namespace testti.Models
{
    public class Attendance
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime time { get; set; }
        public string status { get; set; }
        public Usercs user { get; set; }
    }
}