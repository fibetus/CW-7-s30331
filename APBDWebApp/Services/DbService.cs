using APBDWebApp.Exceptions;
using APBDWebApp.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace APBDWebApp.Services;

public interface IDbService
{
    public Task<IEnumerable<TripsGetDto>> GetTripsAsync();
    public Task<IEnumerable<ClientsTripsGetDto>> GetClientTripsAsync(int clientId);
    public Task<ClientsGetDto> CreateClientAsync(ClientsCreateDto clientData);
    public Task RegisterClientForTripAsync(int clientId, int tripId);
    public Task DeleteClientTripRegistrationAsync(int clientId, int tripId);
}

public class DbService(IConfiguration config) : IDbService
{
    public async Task<IEnumerable<TripsGetDto>> GetTripsAsync()
    {
        var tripsDict = new Dictionary<int, TripsGetDto>();
        
        var connectionString = config.GetConnectionString("Default");
        await using var connection = new SqlConnection(connectionString);
        var sql = """
                  SELECT T.IdTrip, T.Name, T.Description, T.DateFrom, T.DateTo, T.MaxPeople,
                         C.IdCountry, C.Name AS CountryName
                  FROM Trip T
                  LEFT JOIN Country_Trip CT ON T.IdTrip = CT.IdTrip
                  LEFT JOIN Country C ON CT.IdCountry = C.IdCountry
                  ORDER BY T.IdTrip;
                  """;
        
        await using var command = new SqlCommand(sql, connection);
        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var tripId = reader.GetInt32(0);
            if (!tripsDict.TryGetValue(tripId, out var tripDetails))
            {
                tripDetails = new TripsGetDto
                {
                    IdTrip = tripId,
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    Countries = new List<CountryGetDto>()
                };
                
                tripsDict.Add(tripId, tripDetails);
            }

            if (!await reader.IsDBNullAsync(reader.GetOrdinal("IdCountry")))
            {
                tripDetails.Countries.Add(new CountryGetDto
                {
                    IdCountry = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
        }
        return tripsDict.Values;
    }

    public async Task<IEnumerable<ClientsTripsGetDto>> GetClientTripsAsync(int clientId)
    {
        var clientTrips = new List<ClientsTripsGetDto>();
        
        var connectionString = config.GetConnectionString("Default");
        await using var connection = new SqlConnection(connectionString);
        
        var checkIfClientExistsSql = "SELECT COUNT(1) FROM Client WHERE IdClient = @IdClient;";
        await using var checkIfClientExistsCommand = new SqlCommand(checkIfClientExistsSql, connection);
        await connection.OpenAsync();
        checkIfClientExistsCommand.Parameters.AddWithValue("@IdClient", clientId);
        if ((int)await checkIfClientExistsCommand.ExecuteScalarAsync() == 0)
        {
            throw new ClientNotFoundException($"Client with ID {clientId} not found");
        }

        var sql = """
                  SELECT T.IdTrip, T.Name, T.Description, T.DateFrom, T.DateTo, T.MaxPeople,
                         CT_Client.RegisteredAt, CT_Client.PaymentDate
                  FROM Trip T
                  JOIN Client_Trip CT_Client ON T.IdTrip = CT_Client.IdTrip
                  WHERE CT_Client.IdClient = @ClientId;
                  """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ClientId", clientId);
        await using var reader = await command.ExecuteReaderAsync();

        
        while (await reader.ReadAsync())
        {
            clientTrips.Add(new ClientsTripsGetDto
            {
                IdTrip = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                DateFrom = reader.GetDateTime(3),
                DateTo = reader.GetDateTime(4),
                MaxPeople = reader.GetInt32(5),
                RegisteredAt = reader.GetInt32(6),
                PaymentDate = reader.IsDBNull(reader.GetOrdinal("PaymentDate"))? (int?) null : reader.GetInt32(7)
            });
        }
        return clientTrips;
    }

    public async Task<ClientsGetDto> CreateClientAsync(ClientsCreateDto clientData)
    {
        var connectionString = config.GetConnectionString("Default");
        await using var connection = new SqlConnection(connectionString);
        
        var checkIfClientExistsSql = """
                                        SELECT COUNT(1) 
                                        FROM Client 
                                        WHERE 
                                            Email = @Email OR 
                                            Pesel = @Pesel OR 
                                            Telephone = @Telephone;
                                    """;
        await using var checkIfClientExistsCommand = new SqlCommand(checkIfClientExistsSql, connection);
        
        checkIfClientExistsCommand.Parameters.AddWithValue("@Email", clientData.Email);
        checkIfClientExistsCommand.Parameters.AddWithValue("@Pesel", clientData.Pesel);
        checkIfClientExistsCommand.Parameters.AddWithValue("@Telephone", clientData.Telephone);
        
        await connection.OpenAsync();
        
        if ((int)await checkIfClientExistsCommand.ExecuteScalarAsync() > 0)
        {
            throw new ClientAlreadyExistsException("Client with this email or PESEL or telephone already exists");
        }
        
        var sql = """
                  INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
                  VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel);
                  SELECT SCOPE_IDENTITY();
                  """;
        await using var command = new SqlCommand(sql, connection);
        
        command.Parameters.AddWithValue("@FirstName", clientData.FirstName);
        command.Parameters.AddWithValue("@LastName", clientData.LastName);
        command.Parameters.AddWithValue("@Email", clientData.Email);
        command.Parameters.AddWithValue("@Telephone", clientData.Telephone);
        command.Parameters.AddWithValue("@Pesel", clientData.Pesel);

        var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
        
        return new ClientsGetDto()
        {
            IdClient = newId,
            FirstName = clientData.FirstName,
            LastName = clientData.LastName,
            Email = clientData.Email,
            Telephone = clientData.Telephone,
            Pesel = clientData.Pesel
        };
            
        }

    public async Task RegisterClientForTripAsync(int clientId, int tripId)
    {
        var connectionString = config.GetConnectionString("Default");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var clientCheckSql = "SELECT COUNT(1) FROM Client WHERE IdClient = @IdClient;";
            await using var clientCheckCommand = new SqlCommand(clientCheckSql, connection, (SqlTransaction)transaction);
            clientCheckCommand.Parameters.AddWithValue("@IdClient", clientId);
            if ((int)await clientCheckCommand.ExecuteScalarAsync() == 0)
            {
                throw new ClientNotFoundException($"Client with ID {clientId} not found");
            }

            int maxPeople;
            var tripCheckSql = "SELECT MaxPeople FROM Trip WHERE IdTrip = @IdTrip;";
            await using var tripCheckCommand = new SqlCommand(tripCheckSql, connection, (SqlTransaction)transaction);
            tripCheckCommand.Parameters.AddWithValue("@IdTrip", tripId);
            var maxPeopleResult = await tripCheckCommand.ExecuteScalarAsync();
            if (maxPeopleResult == null || maxPeopleResult == DBNull.Value)
            {
                throw new TripNotFoundException($"Trip with ID {tripId} not found.");
            }

            maxPeople = (int)maxPeopleResult; // storing trip to check max people next

            var existingRegistrationSql =
                "SELECT COUNT(1) FROM Client_Trip WHERE IdClient = @IdClient AND IdTrip = @IdTrip;";
            await using var existingRegistrationCommand =
                new SqlCommand(existingRegistrationSql, connection, (SqlTransaction)transaction);
            existingRegistrationCommand.Parameters.AddWithValue("@IdClient", clientId);
            existingRegistrationCommand.Parameters.AddWithValue("@IdTrip", tripId);
            if ((int)await existingRegistrationCommand.ExecuteScalarAsync() > 0)
            {
                throw new ClientAlreadyRegisteredException(
                    $"Client with ID {clientId} is already registered for trip ID {tripId}.");
            }

            var isTripFullSql = "SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @IdTrip;";
            await using var isTripFullCommand = new SqlCommand(isTripFullSql, connection, (SqlTransaction)transaction);
            isTripFullCommand.Parameters.AddWithValue("@IdTrip", tripId);
            int currentPeopleCount = (int)await isTripFullCommand.ExecuteScalarAsync();
            if (currentPeopleCount > maxPeople)
            {
                throw new TripFullException(
                    $"Trip with ID {tripId} is already full. Max capacity: {maxPeople}, Current registrations: {currentPeopleCount}.");
            }

            int registeredAtDate = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            var insertSql = """
                            INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt, PaymentDate)
                            VALUES (@IdClient, @IdTrip, @RegisteredAt, NULL);
                            """;
            await using var insertCommand = new SqlCommand(insertSql, connection, (SqlTransaction)transaction);
            insertCommand.Parameters.AddWithValue("@IdClient", clientId);
            insertCommand.Parameters.AddWithValue("@IdTrip", tripId);
            insertCommand.Parameters.AddWithValue("@RegisteredAt", registeredAtDate);

            await insertCommand.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteClientTripRegistrationAsync(int clientId, int tripId)
    {
        var connectionString = config.GetConnectionString("Default");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        
        var checkIfRegistrationExistsQuery = "SELECT COUNT(1) FROM Client_Trip WHERE IdClient = @IdClient AND IdTrip = @IdTrip;";
        await using var checkIfRegistrationExistsCmd = new SqlCommand(checkIfRegistrationExistsQuery, connection);
        checkIfRegistrationExistsCmd.Parameters.AddWithValue("@IdClient", clientId);
        checkIfRegistrationExistsCmd.Parameters.AddWithValue("@IdTrip", tripId);
            
        if ((int)await checkIfRegistrationExistsCmd.ExecuteScalarAsync() == 0)
        {
            throw new RegistrationNotFoundException($"Registration for client ID {clientId} on trip ID {tripId} not found.");
        }
        
        var sql = "DELETE FROM Client_Trip WHERE IdClient = @IdClient AND IdTrip = @IdTrip;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@IdClient", clientId);
        command.Parameters.AddWithValue("@IdTrip", tripId);
        
        await command.ExecuteNonQueryAsync();
    }
}