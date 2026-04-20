namespace apbd_cw7_s33211.Services;
using DTOs;
using Microsoft.Data.SqlClient;
using System.Data;

public class AppointmentsService : IAppointmentsService
{
    private readonly string _connectionString;

    public AppointmentsService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string not found.");
    }
    
    
    public async Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName)
    {
        var appointments = new List<AppointmentListDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("""
                                                 SELECT 
                                                     a.IdAppointment, a.AppointmentDate, a.Status, a.Reason,
                                                     p.FirstName + N' ' + p.LastName AS PatientFullName, p.Email AS PatientEmail
                                                 FROM s33211.Appointments a
                                                 JOIN s33211.Patients p ON p.IdPatient = a.IdPatient
                                                 WHERE (@Status IS NULL OR a.Status = @Status)
                                                   AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                                                 ORDER BY a.AppointmentDate;
                                                 """, connection);

        command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 30) { Value = (object?)status ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@PatientLastName", SqlDbType.NVarChar, 80) { Value = (object?)patientLastName ?? DBNull.Value });

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            appointments.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail"))
            });
        }
        return appointments;
    }

    public async Task<AppointmentDetailsDto> GetAppointmentDetailsAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("""
                                                 SELECT 
                                                     a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, a.InternalNotes, a.CreatedAt,
                                                     p.Email AS PatientEmail, p.PhoneNumber AS PatientPhoneNumber,
                                                     d.LicenseNumber AS DoctorLicenseNumber
                                                 FROM s33211.Appointments a
                                                 JOIN s33211.Patients p ON a.IdPatient = p.IdPatient
                                                 JOIN s33211.Doctors d ON a.IdDoctor = d.IdDoctor
                                                 WHERE a.IdAppointment = @IdAppointment;
                                                 """, connection);

        command.Parameters.AddWithValue("@IdAppointment", idAppointment);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new KeyNotFoundException("Appointment not found.");
        }

        return new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
            AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            Reason = reader.GetString(reader.GetOrdinal("Reason")),
            InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes")) ? null : reader.GetString(reader.GetOrdinal("InternalNotes")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),
            PatientPhoneNumber = reader.GetString(reader.GetOrdinal("PatientPhoneNumber")),
            DoctorLicenseNumber = reader.GetString(reader.GetOrdinal("DoctorLicenseNumber"))
        };
    }

    public async Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto request)
    {
        if (request.AppointmentDate < DateTime.Now)
            throw new ArgumentException("Appointment date cannot be in the past.");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        if (!await IsActiveAsync(connection, "Patients", "IdPatient", request.IdPatient))
            throw new ArgumentException("Patient is inactive or does not exist.");

        if (!await IsActiveAsync(connection, "Doctors", "IdDoctor", request.IdDoctor))
            throw new ArgumentException("Doctor is inactive or does not exist.");

        if (await DoctorScheduleConflictAsync(connection, request.IdDoctor, request.AppointmentDate))
            throw new InvalidOperationException("Doctor already has an appointment at this exact time.");

        await using var command = new SqlCommand("""
                                                 INSERT INTO s33211.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                                                 OUTPUT INSERTED.IdAppointment
                                                 VALUES (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason);
                                                 """, connection);

        command.Parameters.AddWithValue("@IdPatient", request.IdPatient);
        command.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", request.AppointmentDate);
        command.Parameters.AddWithValue("@Reason", request.Reason);

        return (int)await command.ExecuteScalarAsync();
    }

    public async Task UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto request)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var selectCommand = new SqlCommand("SELECT Status, AppointmentDate FROM s33211.Appointments WHERE IdAppointment = @IdAppointment", connection);
        selectCommand.Parameters.AddWithValue("@IdAppointment", idAppointment);
        
        await using var reader = await selectCommand.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new KeyNotFoundException("Appointment not found.");

        var currentStatus = reader.GetString(reader.GetOrdinal("Status"));
        var currentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate"));
        await reader.CloseAsync(); 

        if (currentStatus == "Completed" && currentDate != request.AppointmentDate)
            throw new ArgumentException("Cannot change the date of a completed appointment.");

        if (!await IsActiveAsync(connection, "Patients", "IdPatient", request.IdPatient))
            throw new ArgumentException("Patient is inactive or does not exist.");

        if (!await IsActiveAsync(connection, "Doctors", "IdDoctor", request.IdDoctor))
            throw new ArgumentException("Doctor is inactive or does not exist.");

        if (currentDate != request.AppointmentDate && await DoctorScheduleConflictAsync(connection, request.IdDoctor, request.AppointmentDate, idAppointment))
            throw new InvalidOperationException("Doctor already has an appointment at this exact time.");

        await using var updateCommand = new SqlCommand("""
            UPDATE s33211.Appointments
            SET IdPatient = @IdPatient, IdDoctor = @IdDoctor, AppointmentDate = @AppointmentDate,
                Status = @Status, Reason = @Reason, InternalNotes = @InternalNotes
            WHERE IdAppointment = @IdAppointment;
            """, connection);

        updateCommand.Parameters.AddWithValue("@IdAppointment", idAppointment);
        updateCommand.Parameters.AddWithValue("@IdPatient", request.IdPatient);
        updateCommand.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
        updateCommand.Parameters.AddWithValue("@AppointmentDate", request.AppointmentDate);
        updateCommand.Parameters.AddWithValue("@Status", request.Status);
        updateCommand.Parameters.AddWithValue("@Reason", request.Reason);
        updateCommand.Parameters.AddWithValue("@InternalNotes", (object?)request.InternalNotes ?? DBNull.Value);

        await updateCommand.ExecuteNonQueryAsync();
    }

    public async Task DeleteAppointmentAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var selectCommand = new SqlCommand("SELECT Status FROM s33211.Appointments WHERE IdAppointment = @IdAppointment", connection);
        selectCommand.Parameters.AddWithValue("@IdAppointment", idAppointment);
            
        var statusResult = await selectCommand.ExecuteScalarAsync();
        if (statusResult == null)
            throw new KeyNotFoundException("Appointment not found.");

        if (statusResult.ToString() == "Completed")
            throw new InvalidOperationException("Cannot delete a completed appointment.");

        await using var deleteCommand = new SqlCommand("DELETE FROM s33211.Appointments WHERE IdAppointment = @IdAppointment", connection);
        deleteCommand.Parameters.AddWithValue("@IdAppointment", idAppointment);
        await deleteCommand.ExecuteNonQueryAsync();
    }
    
    private async Task<bool> IsActiveAsync(SqlConnection connection, string tableName, string idColumn, int id)
    {
        var query = $"SELECT IsActive FROM s33211.{tableName} WHERE {idColumn} = @Id";
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Id", id);
        var result = await command.ExecuteScalarAsync();
        return result != null && result != DBNull.Value && Convert.ToBoolean(result);
    }

    private async Task<bool> DoctorScheduleConflictAsync(SqlConnection connection, int idDoctor, DateTime appointmentDate, int? excludeId = null)
    {
        var query = "SELECT 1 FROM s33211.Appointments WHERE IdDoctor = @IdDoctor AND AppointmentDate = @AppointmentDate AND Status != 'Cancelled'";
        if (excludeId.HasValue) query += " AND IdAppointment != @ExcludeId";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@IdDoctor", idDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", appointmentDate);
        if (excludeId.HasValue) command.Parameters.AddWithValue("@ExcludeId", excludeId.Value);

        var result = await command.ExecuteScalarAsync();
        return result != null;
    }
}