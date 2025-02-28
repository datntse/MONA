using CsvHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using MONA.API.Data;
using MONA.API.Models;
using System.Data;
using System.Diagnostics;
using System.Globalization;

namespace MONA.API.Controllers
{
    [Route("api/imports")]
    [ApiController]
    public class ImportController : ControllerBase
    {
        private IConfiguration _configuration;
        private ApplicationDbContext _context;
        private IMemoryCache _cache;

        public ImportController(IConfiguration configuration, ApplicationDbContext context,
             IMemoryCache cache)
        {
            _configuration = configuration;
            _context = context;
            _cache = cache;
        }
        [HttpPost("import-file")]
        public async Task<IActionResult> ImportFileAsync()
        {
            var stopwatch = Stopwatch.StartNew(); // Bắt đầu đo thời gian

            string filePath = "employee_data_100k.csv";
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            }))
            {
                var employees =  csv.GetRecords<Employee>();

                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                using var bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = "Employees",
                    
                };

                var table = new DataTable();
                table.Columns.Add("Id", typeof(string));
                table.Columns.Add("Name", typeof(string));
                table.Columns.Add("BirthDate", typeof(DateOnly));

                if (!employees.IsNullOrEmpty())
                {
                    foreach (var emp in employees)
                    {
                        table.Rows.Add(emp.Id, emp.Name, emp.BirthDate);
                    }
                    await bulkCopy.WriteToServerAsync(table);

                    stopwatch.Stop(); // Dừng đo thời gian

                    return Ok(new { ExecutionTime = stopwatch.ElapsedMilliseconds + " ms" });
                }
            }
            return Ok(new { ExecutionTime = "Failed try in " + stopwatch.ElapsedMilliseconds + " ms" });
        }

        [HttpGet("get-employee-storedProcedures")]
        public IActionResult GetEmployee(int perPage = 100, int pageIndex = 1)
        {
            var stopwatch = Stopwatch.StartNew(); // Bắt đầu đo thời gian
            string cacheKey = $"employees-storedProcedures-{pageIndex}-{perPage}";


            if (!_cache.TryGetValue(cacheKey, out List<EmployeeDTO> employees))
            {
                employees = _context.Database
                .SqlQueryRaw<EmployeeDTO>("EXEC GetEmployeesPaged @p0, @p1", pageIndex, perPage)
                .ToList();

                _cache.Set(cacheKey, employees, TimeSpan.FromMinutes(10));
            }
            stopwatch.Stop();

            return Ok(new
            {
                executionTime = stopwatch.ElapsedMilliseconds + " ms",
                data = employees,
            });
        }

        [HttpGet("get-employee-linq")]
        public IActionResult GetEmployeeEf(int perPage = 100, int pageIndex = 1)
        {
            var stopwatch = Stopwatch.StartNew(); // Bắt đầu đo thời gian
            string cacheKey = $"employees-linq-{pageIndex}-{perPage}";


            if (!_cache.TryGetValue(cacheKey, out List<Employee> employees))
            {
                employees = _context.Employees.OrderBy(e => Convert.ToInt64(e.Id.Substring(3))).Skip((pageIndex - 1) * perPage).Take(perPage).ToList(); 

                _cache.Set(cacheKey, employees, TimeSpan.FromMinutes(10));
            }
            List<EmployeeVM> result = new List<EmployeeVM>();
            foreach (var e in employees)
            {
            EmployeeVM employeeVM = new EmployeeVM();
                employeeVM.Id = e.Id;
                employeeVM.Name = e.Name;
                employeeVM.BirthDate = e.BirthDate;

                DateOnly today = DateOnly.FromDateTime(DateTime.Today);
                int age = today.Year - e.BirthDate.Year;
                if (e.BirthDate > today.AddYears(-age))
                {
                    age--;
                }

                employeeVM.Age = age;
                result.Add(employeeVM);
            }
            stopwatch.Stop();

            return Ok(new
            {
                executionTime = stopwatch.ElapsedMilliseconds + " ms",
                data = result,
            });
        }


    }
}
