using System.ComponentModel.DataAnnotations;

namespace MONA.API.Models
{
    public class EmployeeDTO
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateOnly BirthDate { get; set; }
        public int Age { get; set; }
    }

    public class EmployeeVM
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateOnly BirthDate { get; set; }
        public int Age { get; set; }
    }
}
