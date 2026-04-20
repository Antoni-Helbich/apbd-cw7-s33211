namespace apbd_cw7_s33211.Services;
using apbd_cw7_s33211.DTOs;

public interface IAppointmentsService
{
    Task<IEnumerable<AppointmentListDto>>  GetAppointmentsAsync(string? status, string? patientLastName);
    Task<AppointmentDetailsDto> GetAppointmentDetailsAsync(int idAppointment);
    Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto request);
    Task UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto request);
    Task DeleteAppointmentAsync(int idAppointment);
}