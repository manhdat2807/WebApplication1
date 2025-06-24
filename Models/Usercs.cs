namespace testti.Models
{
    public class Usercs
    {
        public int Id { get; set; } 
        public string Name { get; set; }
        public string image { get; set; }
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>(); 
    }
}
