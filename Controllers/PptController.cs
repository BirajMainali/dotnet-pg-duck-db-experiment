using System.Diagnostics;
using System.Text.RegularExpressions;
using Dapper;
using DuckDB.NET.Data;
using Ganss.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PgDuckExperiment.Data;
using PgDuckExperiment.Dto;
using PgDuckExperiment.Models;

namespace PgDuckExperiment.Controllers;

[ApiController]
[Route("api/ppt")]
public class PptController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PptController> _logger;
    private readonly string _pptTemp;

    public PptController(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<PptController> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;

        _pptTemp = Path.Combine(environment.WebRootPath, "temp", "ppt");
        Directory.CreateDirectory(_pptTemp);
    }

    #region Regular Import

    [HttpPost("regular/import")]
    public async Task<IActionResult> RegularImport([FromForm] IFormFile file)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var items = LoadExcelFile(file);
            var errors = ValidatePptItems(items);
            if (errors.Any()) return BadRequest(errors);

            var ppts = MapToPpt(items);
            await _context.Set<Ppt>().AddRangeAsync(ppts);
            await _context.SaveChangesAsync();

            watch.Stop();
            _logger.LogInformation("Regular import completed in {Elapsed} ms", watch.ElapsedMilliseconds);
            return Ok(new { message = "Import successful", timeMs = watch.ElapsedMilliseconds });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Regular import failed");
            return BadRequest(e.Message);
        }
    }

    #endregion

    #region Batched Import

    [HttpPost("batched/import")]
    public async Task<IActionResult> BatchedImport([FromForm] IFormFile file)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var items = LoadExcelFile(file);
            var errors = ValidatePptItems(items);
            if (errors.Any()) return BadRequest(errors);

            var ppts = MapToPpt(items);
            const int batchSize = 1000;

            foreach (var batch in ppts.Chunk(batchSize))
            {
                await _context.Set<Ppt>().AddRangeAsync(batch);
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
                _logger.LogInformation("Saved batch of {BatchCount} records", batch.Length);
            }

            watch.Stop();
            _logger.LogInformation("Batched import completed in {Elapsed} ms", watch.ElapsedMilliseconds);
            return Ok(new { message = "Import successful", timeMs = watch.ElapsedMilliseconds });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Batched import failed");
            return BadRequest(e.Message);
        }
    }

    #endregion

    #region Optimized Import (DuckDB + PostgreSQL COPY)

    [HttpPost("optimized/import")]
    public async Task<IActionResult> OptimizedImport([FromForm] IFormFile file)
    {
        var watch = Stopwatch.StartNew();
        var filePath = Path.Combine(_pptTemp, file.FileName);
        var csvPath = filePath + ".csv";

        try
        {
            // Save uploaded file
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("File saved to {FilePath}", filePath);

            // DuckDB validation & CSV export
            var errors = await ValidateAndExportWithDuckDb(filePath, csvPath);
            if (errors.Any()) return BadRequest(errors);
            _logger.LogInformation("DuckDB validation passed. CSV exported to {CsvPath}", csvPath);

            // PostgreSQL import via COPY
            await ImportCsvToPostgres(csvPath);

            watch.Stop();
            _logger.LogInformation("Optimized import completed in {Elapsed} ms", watch.ElapsedMilliseconds);
            return Ok(new { message = "Import successful", timeMs = watch.ElapsedMilliseconds });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Optimized import failed");
            return BadRequest(e.Message);
        }
    }

    #endregion

    #region Helpers

    private List<PptDto> LoadExcelFile(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        return new ExcelMapper(stream).Fetch<PptDto>().ToList();
    }

    private List<Ppt> MapToPpt(List<PptDto> items)
    {
        return items.Select(x => new Ppt
        {
            Id = Guid.NewGuid(),
            EmployeeId = x.EmployeeId,
            FirstName = x.FirstName,
            MiddleName = x.MiddleName,
            LastName = x.LastName,
            Email = x.Email,
            Phone = x.Phone,
            DateOfBirth = x.DateOfBirth.HasValue ? DateTime.SpecifyKind(x.DateOfBirth.Value, DateTimeKind.Utc) : null,
            Gender = x.Gender,
            NationalId = x.NationalId,
            Country = x.Country,
            State = x.State,
            City = x.City,
            AddressLine = x.AddressLine,
            ZipCode = x.ZipCode,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();
    }

    private static List<string> ValidatePptItems(List<PptDto> items)
    {
        var errors = new List<string>();
        var specialCharacterRegex = new Regex("[^a-zA-Z0-9]");
        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        var validGenders = new[] { "Male", "Female", "Other" };

        foreach (var x in items)
        {
            if (string.IsNullOrWhiteSpace(x.EmployeeId) || x.EmployeeId.Length != 6 || !x.EmployeeId.StartsWith("EMP"))
                errors.Add($"{x.SN} : EmployeeId must start with 'EMP' and be exactly 6 characters.");

            if (string.IsNullOrWhiteSpace(x.FirstName) || x.FirstName.Length < 3 || x.FirstName.Contains(' ') ||
                specialCharacterRegex.IsMatch(x.FirstName) || !char.IsUpper(x.FirstName[0]) ||
                char.IsDigit(x.FirstName.Last()))
                errors.Add($"{x.SN} : Invalid FirstName.");

            if (!string.IsNullOrWhiteSpace(x.MiddleName) &&
                (x.MiddleName.Length > 20 || x.MiddleName.Any(char.IsDigit)))
                errors.Add($"{x.SN} : Invalid MiddleName.");

            if (string.IsNullOrWhiteSpace(x.LastName) || x.LastName.Length < 2 ||
                specialCharacterRegex.IsMatch(x.LastName))
                errors.Add($"{x.SN} : Invalid LastName.");

            if (!string.IsNullOrWhiteSpace(x.Email) && (x.Email.Length > 50 || !emailRegex.IsMatch(x.Email)))
                errors.Add($"{x.SN} : Invalid Email.");

            if (!string.IsNullOrWhiteSpace(x.Phone) &&
                (!x.Phone.All(char.IsDigit) || x.Phone.Length < 7 || x.Phone.Length > 15))
                errors.Add($"{x.SN} : Invalid Phone.");

            if (x.DateOfBirth.HasValue && (x.DateOfBirth.Value.Date > DateTime.Today ||
                                           DateTime.Today.Year - x.DateOfBirth.Value.Year < 18))
                errors.Add($"{x.SN} : Invalid DateOfBirth. Must be at least 18 years old.");

            if (!string.IsNullOrWhiteSpace(x.Gender) && !validGenders.Contains(x.Gender))
                errors.Add($"{x.SN} : Invalid Gender.");

            if (!string.IsNullOrWhiteSpace(x.NationalId) &&
                (x.NationalId.Length > 20 || !x.NationalId.All(char.IsLetterOrDigit)))
                errors.Add($"{x.SN} : Invalid NationalId.");

            if (!string.IsNullOrWhiteSpace(x.Country) && x.Country.Length > 50)
                errors.Add($"{x.SN} : Invalid Country.");

            if (!string.IsNullOrWhiteSpace(x.State) && x.State.Length > 50)
                errors.Add($"{x.SN} : Invalid State.");

            if (!string.IsNullOrWhiteSpace(x.City) && x.City.Length > 50)
                errors.Add($"{x.SN} : Invalid City.");

            if (!string.IsNullOrWhiteSpace(x.AddressLine) && x.AddressLine.Length > 100)
                errors.Add($"{x.SN} : Invalid AddressLine.");

            if (!string.IsNullOrWhiteSpace(x.ZipCode) && (!x.ZipCode.All(char.IsDigit) || x.ZipCode.Length > 10))
                errors.Add($"{x.SN} : Invalid ZipCode.");
        }

        return errors;
    }

    private static string GetDuckQbQuery(string sourcePath) => $"""
                                                                WITH data AS (
                                                                    SELECT *
                                                                    FROM '{sourcePath}'
                                                                ),
                                                                errors AS (
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN EmployeeId IS NULL OR EmployeeId = '' OR LENGTH(EmployeeId) != 6 OR SUBSTR(EmployeeId,1,3) != 'EMP'
                                                                            THEN 'EmployeeId must start with ''EMP'' and be exactly 6 characters.'
                                                                        END AS error_message
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN FirstName IS NULL OR LENGTH(FirstName) < 3 OR STRPOS(FirstName,' ') > 0
                                                                                 OR REGEXP_MATCHES(FirstName,'[^a-zA-Z0-9]')
                                                                                 OR SUBSTR(FirstName,1,1) != UPPER(SUBSTR(FirstName,1,1))
                                                                                 OR SUBSTR(FirstName,-1,1) SIMILAR TO '[0-9]'
                                                                            THEN 'Invalid FirstName.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN MiddleName IS NOT NULL AND (LENGTH(MiddleName) > 20 OR REGEXP_MATCHES(MiddleName,'[0-9]'))
                                                                            THEN 'Invalid MiddleName.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN LastName IS NULL OR LENGTH(LastName) < 2 OR REGEXP_MATCHES(LastName,'[^a-zA-Z0-9]')
                                                                            THEN 'Invalid LastName.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN Email IS NOT NULL AND (LENGTH(Email) > 50 OR NOT REGEXP_MATCHES(Email,'^[^@\s]+@[^@\s]+\.[^@\s]+$'))
                                                                            THEN 'Invalid Email.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN Phone IS NOT NULL AND (NOT REGEXP_MATCHES(Phone,'^[0-9]+$') OR LENGTH(Phone)<7 OR LENGTH(Phone)>15)
                                                                            THEN 'Invalid Phone.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN DateOfBirth IS NOT NULL AND (DateOfBirth>CURRENT_DATE OR DATE_DIFF('year',DateOfBirth,CURRENT_DATE)<18)
                                                                            THEN 'Invalid DateOfBirth. Must be at least 18 years old.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN Gender IS NOT NULL AND Gender NOT IN ('Male','Female','Other')
                                                                            THEN 'Invalid Gender.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN NationalId IS NOT NULL AND (LENGTH(NationalId)>20 OR NOT REGEXP_MATCHES(NationalId,'^[a-zA-Z0-9]+$'))
                                                                            THEN 'Invalid NationalId.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN Country IS NOT NULL AND LENGTH(Country)>50 THEN 'Invalid Country.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN State IS NOT NULL AND LENGTH(State)>50 THEN 'Invalid State.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN City IS NOT NULL AND LENGTH(City)>50 THEN 'Invalid City.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN AddressLine IS NOT NULL AND LENGTH(AddressLine)>100 THEN 'Invalid AddressLine.'
                                                                        END
                                                                    FROM data
                                                                    UNION ALL
                                                                    SELECT
                                                                        SN,
                                                                        CASE 
                                                                            WHEN ZipCode IS NOT NULL AND (LENGTH(ZipCode)>10 OR NOT REGEXP_MATCHES(ZipCode,'^[0-9]+$'))
                                                                            THEN 'Invalid ZipCode.'
                                                                        END
                                                                    FROM data
                                                                )
                                                                SELECT SN, error_message
                                                                FROM errors
                                                                WHERE error_message IS NOT NULL;

                                                                """;

    private static string GetCsvQbQuery(string sourcePath, string csvPath) => $@"
        COPY (
            SELECT
                uuid() AS Id,
                EmployeeId,
                FirstName,
                MiddleName,
                LastName,
                Email,
                Phone,
                DateOfBirth,
                Gender,
                NationalId,
                Country,
                State,
                City,
                AddressLine,
                ZipCode,
                TRUE AS IsActive,
                CURRENT_TIMESTAMP AS CreatedAt,
                CURRENT_TIMESTAMP AS UpdatedAt
            FROM '{sourcePath}'
        ) TO '{csvPath}' (HEADER, DELIMITER ',');";

    private async Task<List<string>> ValidateAndExportWithDuckDb(string filePath, string csvPath)
    {
        var errors = new List<string>();
        await using var conn = new DuckDBConnection("Data Source=file.db");
        await conn.OpenAsync();

        // Validate
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = GetDuckQbQuery(filePath);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) errors.Add(reader.GetString(0));
        }

        if (errors.Count > 0) return errors;

        // Export CSV
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = GetCsvQbQuery(filePath, csvPath);
            await cmd.ExecuteNonQueryAsync();
        }

        return errors;
    }

    private async Task ImportCsvToPostgres(string csvPath)
    {
        await using var connection = new NpgsqlConnection(_context.Database.GetDbConnection().ConnectionString);
        await connection.OpenAsync();
        await using var writer =
            await connection.BeginTextImportAsync("COPY ppts FROM STDIN WITH (FORMAT csv, HEADER true)");
        using var readerCsv = new StreamReader(csvPath);
        while (!readerCsv.EndOfStream)
        {
            var line = await readerCsv.ReadLineAsync();
            await writer.WriteLineAsync(line);
        }
    }
}

#endregion