using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using apbd_cw7_s33211.DTOs;
using apbd_cw7_s33211.Services;

namespace apbd_cw7_s33211.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentsService _appointmentsService;


        public AppointmentsController(IAppointmentsService appointmentsService)
        {
            _appointmentsService = appointmentsService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAppointments([FromQuery] string? status, [FromQuery] string? patientLastName)
        {
            var result = await _appointmentsService.GetAppointmentsAsync(status, patientLastName);
            return Ok(result);
        }

        [HttpGet]
        [Route("{idAppointment:int}")]
        public async Task<IActionResult> GetAppointmentDetails(int idAppointment)
        {
            try
            {
                var result = await _appointmentsService.GetAppointmentDetailsAsync(idAppointment);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponseDto { Message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentRequestDto request)
        {
            try
            {
                var newId = await _appointmentsService.CreateAppointmentAsync(request);
                return CreatedAtAction(nameof(GetAppointmentDetails), new { idAppointment = newId }, request);
            }
            catch (ArgumentException ex) 
            {
                return BadRequest(new ErrorResponseDto { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ErrorResponseDto { Message = ex.Message });
            }
        }

        [HttpPut]
        [Route("{idAppointment:int}")]
        public async Task<IActionResult> UpdateAppointment(int idAppointment, [FromBody] UpdateAppointmentRequestDto request)
        {
            try
            {
                await _appointmentsService.UpdateAppointmentAsync(idAppointment, request);
                return Ok();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponseDto { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ErrorResponseDto { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ErrorResponseDto { Message = ex.Message });
            }
        }

        [HttpDelete]
        [Route("{idAppointment:int}")]
        public async Task<IActionResult> DeleteAppointment(int idAppointment)
        {
            try
            {
                await _appointmentsService.DeleteAppointmentAsync(idAppointment);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponseDto { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ErrorResponseDto { Message = ex.Message });
            }
        }
    }
}
