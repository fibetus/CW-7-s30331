using APBDWebApp.Exceptions;
using APBDWebApp.Models.DTOs;
using APBDWebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace APBDWebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController(IDbService service) : ControllerBase
{
    /// GET /api/clients/{id}/trips
    /// Retrieves all trips associated with a specific client, including registration details.
    [HttpGet("{id}/trips")]
    public async Task<IActionResult> GetClientTrips(int id)
    {
        try
        {
            return Ok(await service.GetClientTripsAsync(id));
        }
        catch (ClientNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// POST /api/clients
    /// Creates a new client record.
    [HttpPost]
    public async Task<IActionResult> CreateClient([FromBody] ClientsCreateDto clientData)
    {
        try
        {
            var createdClient = await service.CreateClientAsync(clientData);
            return StatusCode(StatusCodes.Status201Created, createdClient);
        }
        catch (ClientAlreadyExistsException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// PUT /api/clients/{clientId}/trips/{tripId}
    /// Registers a client for a specific trip.
    [HttpPut("{clientId}/trips/{tripId}")]
    public async Task<IActionResult> RegisterClientForTrip(int clientId, int tripId)
    {
        try
        {
            await service.RegisterClientForTripAsync(clientId, tripId);
            return Ok($"Client {clientId} successfully registered for trip {tripId}.");
        }
        catch (ClientNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ClientAlreadyExistsException ex)
        {
            return Conflict(ex.Message);
        }
        catch (TripNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ClientAlreadyRegisteredException ex)
        {
            return Conflict(ex.Message);
        }
        catch (TripFullException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// DELETE /api/clients/{clientId}/trips/{tripId}
    /// Deletes a client's registration from a trip.
    [HttpDelete("{clientId}/trips/{tripId}")]
    public async Task<IActionResult> DeleteClientTripRegistration(int clientId, int tripId)
    {
        try
        {
            await service.DeleteClientTripRegistrationAsync(clientId, tripId);
            return Ok($"Registration for client {clientId} from trip {tripId} successfully deleted.");
        }
        catch (RegistrationNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}