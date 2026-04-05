using Proyecto_Progra_Web.API.DTOs;
using Proyecto_Progra_Web.API.Models;
using Google.Cloud.Firestore;

namespace Proyecto_Progra_Web.API.Services;

/// <summary>
/// RoomService implementa el CRUD de habitaciones y la gestión de reviews contra Firestore.
/// </summary>
public class RoomService : IRoomService
{
    private readonly FirebaseService _firebaseService;
    private readonly ILogger<RoomService> _logger;

    public RoomService(FirebaseService firebaseService, ILogger<RoomService> logger)
    {
        _firebaseService = firebaseService;
        _logger = logger;
    }

    // --------------------------------------------------------
    // ROOMS
    // --------------------------------------------------------

    public async Task<List<RoomDto>> GetAllRooms(string? roomType = null)
    {
        try
        {
            var roomsCollection = _firebaseService.GetCollection("rooms");
            var reservationsCollection = _firebaseService.GetCollection("reservations");

            Query query = roomsCollection;
            if (!string.IsNullOrWhiteSpace(roomType))
                query = query.WhereEqualTo("RoomType", roomType);

            var snapshot = await query.GetSnapshotAsync();

            var hondurasNow = DateTime.UtcNow.AddHours(-6);
            var allResSnapshot = await reservationsCollection.GetSnapshotAsync();

            var activeByRoomId = new HashSet<string>();
            foreach (var resDoc in allResSnapshot.Documents)
            {
                var d = resDoc.ToDictionary();
                var status = d.ContainsKey("Status") ? d["Status"]?.ToString() : "confirmed";
                if (status == "cancelled") continue;

                if (!d.ContainsKey("CheckOutDate")) continue;
                var checkOut = ((Timestamp)d["CheckOutDate"]).ToDateTime();
                if (checkOut < hondurasNow) continue;

                var roomId = d.ContainsKey("RoomId") ? d["RoomId"]?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(roomId))
                    activeByRoomId.Add(roomId);
            }

            var rooms = new List<RoomDto>();
            foreach (var doc in snapshot.Documents)
            {
                var room = MapDocumentToRoom(doc);
                var dto = ConvertToDto(room);
                dto.ReservationCount = activeByRoomId.Contains(room.Id) ? 1 : 0;
                rooms.Add(dto);
            }

            return rooms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener habitaciones");
            throw;
        }
    }

    public async Task<RoomDto?> GetRoomById(string roomId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return null;

            var roomsCollection = _firebaseService.GetCollection("rooms");
            var doc = await roomsCollection.Document(roomId).GetSnapshotAsync();

            if (!doc.Exists) return null;

            var room = MapDocumentToRoom(doc);
            return ConvertToDto(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener habitacion {RoomId}", roomId);
            throw;
        }
    }

    public async Task<Room> CreateRoom(CreateRoomDto createRoomDto, string createdByName, string createdById)
    {
        try
        {
            if (createRoomDto == null)
                throw new ArgumentException("Los datos de la habitacion son requeridos");

            if (string.IsNullOrWhiteSpace(createRoomDto.RoomNumber))
                throw new ArgumentException("El numero de habitacion es requerido");

            if (string.IsNullOrWhiteSpace(createRoomDto.RoomType))
                throw new ArgumentException("El tipo de habitacion es requerido");

            if (createRoomDto.Capacity <= 0)
                throw new ArgumentException("La capacidad debe ser mayor a cero");

            if (createRoomDto.BaseRate <= 0)
                throw new ArgumentException("La tarifa base debe ser mayor a cero");

            var roomsCollection = _firebaseService.GetCollection("rooms");

            var existingQuery = await roomsCollection
                .WhereEqualTo("RoomNumber", createRoomDto.RoomNumber)
                .GetSnapshotAsync();

            if (existingQuery.Count > 0)
                throw new InvalidOperationException($"Ya existe una habitacion con el numero {createRoomDto.RoomNumber}");

            var newRoom = new Room
            {
                Id = Guid.NewGuid().ToString(),
                RoomNumber = createRoomDto.RoomNumber,
                RoomType = createRoomDto.RoomType,
                Capacity = createRoomDto.Capacity,
                Amenities = createRoomDto.Amenities ?? new List<string>(),
                BaseRate = createRoomDto.BaseRate,
                Description = createRoomDto.Description ?? string.Empty,
                PhotoUrl = createRoomDto.PhotoUrl ?? string.Empty,
                ReservationCount = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdByName,
                CreatedById = createdById
            };

            await roomsCollection.Document(newRoom.Id).SetAsync(newRoom);

            _logger.LogInformation("Habitacion creada: {RoomNumber} por {CreatedBy}", newRoom.RoomNumber, createdByName);
            return newRoom;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validacion en CreateRoom");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear habitacion");
            throw;
        }
    }

    public async Task<Room> UpdateRoom(string roomId, UpdateRoomDto updateRoomDto, string updatedByName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomId))
                throw new ArgumentException("El ID de la habitacion es requerido");

            var roomsCollection = _firebaseService.GetCollection("rooms");
            var doc = await roomsCollection.Document(roomId).GetSnapshotAsync();

            if (!doc.Exists)
                throw new InvalidOperationException($"Habitacion con ID {roomId} no existe");

            var existingRoom = MapDocumentToRoom(doc);

            if (!string.IsNullOrWhiteSpace(updateRoomDto.RoomType))
                existingRoom.RoomType = updateRoomDto.RoomType;

            if (updateRoomDto.Capacity.HasValue && updateRoomDto.Capacity.Value > 0)
                existingRoom.Capacity = updateRoomDto.Capacity.Value;

            if (updateRoomDto.Amenities != null)
                existingRoom.Amenities = updateRoomDto.Amenities;

            if (updateRoomDto.BaseRate.HasValue && updateRoomDto.BaseRate.Value > 0)
                existingRoom.BaseRate = updateRoomDto.BaseRate.Value;

            if (!string.IsNullOrWhiteSpace(updateRoomDto.Description))
                existingRoom.Description = updateRoomDto.Description;

            if (!string.IsNullOrWhiteSpace(updateRoomDto.PhotoUrl))
                existingRoom.PhotoUrl = updateRoomDto.PhotoUrl;

            await roomsCollection.Document(roomId).SetAsync(existingRoom, SetOptions.MergeAll);

            _logger.LogInformation("Habitacion {RoomId} actualizada por {UpdatedByName}", roomId, updatedByName);
            return existingRoom;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar habitacion {RoomId}", roomId);
            throw;
        }
    }

    public async Task DeleteRoom(string roomId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomId))
                throw new ArgumentException("El ID de la habitacion es requerido");

            var roomsCollection = _firebaseService.GetCollection("rooms");
            var reservationsCollection = _firebaseService.GetCollection("reservations");

            var roomDoc = await roomsCollection.Document(roomId).GetSnapshotAsync();
            if (!roomDoc.Exists)
                throw new InvalidOperationException($"Habitacion con ID {roomId} no existe");

            var reservationsQuery = await reservationsCollection
                .WhereEqualTo("RoomId", roomId)
                .GetSnapshotAsync();

            if (reservationsQuery.Count > 0)
                throw new InvalidOperationException(
                    $"No se puede eliminar la habitacion. Tiene {reservationsQuery.Count} reserva(s) registrada(s).");

            await roomsCollection.Document(roomId).DeleteAsync();

            _logger.LogInformation("Habitacion {RoomId} eliminada", roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar habitacion {RoomId}", roomId);
            throw;
        }
    }

    public async Task<List<RoomDto>> SearchRooms(string searchTerm)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<RoomDto>();

            var allRooms = await GetAllRooms();
            var term = searchTerm.ToLowerInvariant();

            return allRooms
                .Where(r =>
                    (r.RoomNumber ?? "").ToLowerInvariant().Contains(term) ||
                    (r.RoomType ?? "").ToLowerInvariant().Contains(term) ||
                    (r.Description ?? "").ToLowerInvariant().Contains(term))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar habitaciones");
            throw;
        }
    }

    public async Task<List<RoomDto>> GetAvailableRooms(DateTime checkIn, DateTime checkOut)
    {
        try
        {
            var roomsCollection = _firebaseService.GetCollection("rooms");
            var reservationsCollection = _firebaseService.GetCollection("reservations");

            var allRoomsSnapshot = await roomsCollection.GetSnapshotAsync();
            var allReservationsSnapshot = await reservationsCollection.GetSnapshotAsync();

            var unavailableRoomIds = new HashSet<string>();

            foreach (var doc in allReservationsSnapshot.Documents)
            {
                var dict = doc.ToDictionary();

                if (!dict.ContainsKey("CheckInDate") || !dict.ContainsKey("CheckOutDate"))
                    continue;

                var status = dict.ContainsKey("Status") ? dict["Status"]?.ToString() : "confirmed";
                if (status == "cancelled") continue;

                var existingCheckIn = ((Timestamp)dict["CheckInDate"]).ToDateTime();
                var existingCheckOut = ((Timestamp)dict["CheckOutDate"]).ToDateTime();

                bool overlaps = checkIn < existingCheckOut && checkOut >= existingCheckIn;

                if (overlaps && dict.ContainsKey("RoomId"))
                    unavailableRoomIds.Add(dict["RoomId"]?.ToString() ?? "");
            }

            var availableRooms = new List<RoomDto>();

            foreach (var doc in allRoomsSnapshot.Documents)
            {
                if (!unavailableRoomIds.Contains(doc.Id))
                {
                    var room = MapDocumentToRoom(doc);
                    availableRooms.Add(ConvertToDto(room));
                }
            }

            return availableRooms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener habitaciones disponibles");
            throw;
        }
    }

    public async Task<string?> GetRoomIdByNumber(string roomNumber)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomNumber))
                return null;

            var roomsCollection = _firebaseService.GetCollection("rooms");

            var query = await roomsCollection
                .WhereEqualTo("RoomNumber", roomNumber)
                .GetSnapshotAsync();

            if (query.Count == 0)
                return null;

            return query.Documents[0].Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar habitacion por numero {RoomNumber}", roomNumber);
            return null;
        }
    }

    // --------------------------------------------------------
    // DISPONIBILIDAD POR ROOM NUMBER
    // --------------------------------------------------------

    public async Task<bool> IsRoomAvailableByNumber(string roomNumber, DateTime checkIn, DateTime checkOut, string? excludeReservationId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomNumber))
                throw new ArgumentException("El numero de habitacion es requerido");

            var reservationsCol = _firebaseService.GetCollection("reservations");
            var roomQuery = await reservationsCol
                .WhereEqualTo("RoomNumber", roomNumber)
                .GetSnapshotAsync();

            foreach (var doc in roomQuery.Documents)
            {
                if (!string.IsNullOrWhiteSpace(excludeReservationId) && doc.Id == excludeReservationId)
                    continue;

                var d = doc.ToDictionary();
                var status = d.ContainsKey("Status") ? d["Status"]?.ToString() : "confirmed";
                if (status == "cancelled") continue;

                if (!d.ContainsKey("CheckInDate") || !d.ContainsKey("CheckOutDate"))
                    continue;

                var existingIn = ((Timestamp)d["CheckInDate"]).ToDateTime();
                var existingOut = ((Timestamp)d["CheckOutDate"]).ToDateTime();

                if (checkIn < existingOut && checkOut >= existingIn)
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando disponibilidad para roomNumber {RoomNumber}", roomNumber);
            throw;
        }
    }

    // --------------------------------------------------------
    // REVIEWS
    // --------------------------------------------------------

    public async Task<(double averageStars, List<object> reviews)> GetRoomReviews(string roomNumber)
    {
        try
        {
            var reviewsCol = _firebaseService.GetCollection("reviews");
            var snapshot = await reviewsCol
                .WhereEqualTo("RoomNumber", roomNumber)
                .GetSnapshotAsync();

            var reviews = snapshot.Documents.Select(doc =>
            {
                var d = doc.ToDictionary();
                var createdAtRaw = d.ContainsKey("CreatedAt")
                    ? ((Timestamp)d["CreatedAt"]).ToDateTime()
                    : DateTime.MinValue;

                return new
                {
                    id = doc.Id,
                    reservationId = d.ContainsKey("ReservationId") ? d["ReservationId"]?.ToString() : "",
                    userName = d.ContainsKey("UserName") ? d["UserName"]?.ToString() : "",
                    stars = d.ContainsKey("Stars") ? Convert.ToInt32(d["Stars"]) : 0,
                    comment = d.ContainsKey("Comment") ? d["Comment"]?.ToString() : "",
                    createdAt = createdAtRaw != DateTime.MinValue ? createdAtRaw.ToString("dd-MM-yyyy") : "",
                    createdAtSort = createdAtRaw
                };
            })
            .OrderByDescending(r => r.createdAtSort)
            .Select(r => (object)new { r.id, r.reservationId, r.userName, r.stars, r.comment, r.createdAt })
            .ToList();

            var avg = reviews.Count > 0
                ? Math.Round(reviews.Cast<dynamic>().Average(r => (int)r.stars), 1)
                : 0.0;

            return (avg, reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo reviews de roomNumber {RoomNumber}", roomNumber);
            throw;
        }
    }

    public async Task<List<object>> GetAllReviews(int limit = 12)
    {
        try
        {
            var reviewsCol = _firebaseService.GetCollection("reviews");
            var snapshot = await reviewsCol.GetSnapshotAsync();

            var reviews = snapshot.Documents
                .Select(doc =>
                {
                    var d = doc.ToDictionary();
                    var createdAtRaw = d.ContainsKey("CreatedAt")
                        ? ((Timestamp)d["CreatedAt"]).ToDateTime()
                        : DateTime.MinValue;

                    return new
                    {
                        id = doc.Id,
                        roomNumber = d.ContainsKey("RoomNumber") ? d["RoomNumber"]?.ToString() : "",
                        userId = d.ContainsKey("UserId") ? d["UserId"]?.ToString() : "",
                        userName = d.ContainsKey("UserName") ? d["UserName"]?.ToString() : "Huesped",
                        stars = d.ContainsKey("Stars") ? Convert.ToInt32(d["Stars"]) : 0,
                        comment = d.ContainsKey("Comment") ? d["Comment"]?.ToString() : "",
                        createdAt = createdAtRaw != DateTime.MinValue ? createdAtRaw.ToString("dd-MM-yyyy") : "",
                        createdAtSort = createdAtRaw
                    };
                })
                .Where(r => r.stars > 0 && !string.IsNullOrWhiteSpace(r.comment))
                .OrderByDescending(r => r.createdAtSort)
                .Take(Math.Min(limit, 50))
                .Select(r => (object)new { r.id, r.userId, r.roomNumber, r.userName, r.stars, r.comment, r.createdAt })
                .ToList();

            return reviews;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo all reviews");
            throw;
        }
    }

    public async Task DeleteReview(string roomNumber, string reviewId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reviewId))
                throw new ArgumentException("El ID de la reseña es requerido");

            var reviewsCol = _firebaseService.GetCollection("reviews");
            var doc = await reviewsCol.Document(reviewId).GetSnapshotAsync();

            if (!doc.Exists)
                throw new InvalidOperationException("Reseña no encontrada");

            var d = doc.ToDictionary();
            var docRoomNumber = d.ContainsKey("RoomNumber") ? d["RoomNumber"]?.ToString() : "";

            if (!string.IsNullOrWhiteSpace(docRoomNumber) && docRoomNumber != roomNumber)
                throw new InvalidOperationException("La reseña no pertenece a esta habitación");

            await reviewsCol.Document(reviewId).DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar reseña {ReviewId}", reviewId);
            throw;
        }
    }

    public async Task<string> CreateReview(string roomNumber, CreateReviewDto reviewDto, string userId, string userName)
    {
        try
        {
            if (reviewDto == null)
                throw new ArgumentException("El cuerpo de la peticion es requerido");

            if (reviewDto.Stars < 1 || reviewDto.Stars > 5)
                throw new ArgumentException("La calificacion debe ser entre 1 y 5 estrellas");

            if (string.IsNullOrWhiteSpace(reviewDto.ReservationId))
                throw new ArgumentException("El ID de la reserva es requerido");

            var reservationsCol = _firebaseService.GetCollection("reservations");
            var reviewsCol = _firebaseService.GetCollection("reviews");

            var reservationDoc = await reservationsCol.Document(reviewDto.ReservationId).GetSnapshotAsync();
            if (!reservationDoc.Exists)
                throw new InvalidOperationException("Reserva no encontrada");

            var reservationDict = reservationDoc.ToDictionary();

            if (!reservationDict.ContainsKey("UserId") || reservationDict["UserId"]?.ToString() != userId)
                throw new InvalidOperationException("No puedes resenyar una reserva que no es tuya");

            if (!reservationDict.ContainsKey("CheckOutDate"))
                throw new InvalidOperationException("La reserva no tiene fecha de salida válida");

            var checkOutDate = ((Timestamp)reservationDict["CheckOutDate"]).ToDateTime();
            var hondurasNow = DateTime.UtcNow.AddHours(-6);
            if (checkOutDate > hondurasNow)
                throw new InvalidOperationException("Solo puedes dejar una resena despues de que termine tu estadía");

            var existingReview = await reviewsCol
                .WhereEqualTo("ReservationId", reviewDto.ReservationId)
                .GetSnapshotAsync();

            if (existingReview.Count > 0)
                throw new InvalidOperationException("Ya dejaste una resena para esta estadía");

            var newReview = new Review
            {
                Id = Guid.NewGuid().ToString(),
                RoomNumber = roomNumber,
                UserId = userId,
                UserName = userName,
                ReservationId = reviewDto.ReservationId,
                Stars = reviewDto.Stars,
                Comment = reviewDto.Comment?.Trim() ?? "",
                CreatedAt = DateTime.UtcNow
            };

            var reviewData = new Dictionary<string, object>
            {
                { "RoomNumber",    newReview.RoomNumber },
                { "UserId",        newReview.UserId },
                { "UserName",      newReview.UserName },
                { "ReservationId", newReview.ReservationId },
                { "Stars",         newReview.Stars },
                { "Comment",       newReview.Comment },
                { "CreatedAt",     newReview.CreatedAt }
            };

            await reviewsCol.Document(newReview.Id).SetAsync(reviewData);
            return newReview.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear reseña para roomNumber {RoomNumber}", roomNumber);
            throw;
        }
    }

    // --------------------------------------------------------
    // PRIVADOS
    // --------------------------------------------------------

    private Room MapDocumentToRoom(DocumentSnapshot doc)
    {
        var dict = doc.ToDictionary();

        var room = new Room
        {
            Id = doc.Id,
            RoomNumber = dict.ContainsKey("RoomNumber") ? dict["RoomNumber"]?.ToString() ?? "" : "",
            RoomType = dict.ContainsKey("RoomType") ? dict["RoomType"]?.ToString() ?? "" : "",
            Capacity = dict.ContainsKey("Capacity") ? Convert.ToInt32(dict["Capacity"]) : 0,
            BaseRate = dict.ContainsKey("BaseRate") ? Convert.ToDouble(dict["BaseRate"]) : 0,
            Description = dict.ContainsKey("Description") ? dict["Description"]?.ToString() ?? "" : "",
            PhotoUrl = dict.ContainsKey("PhotoUrl") ? dict["PhotoUrl"]?.ToString() ?? "" : "",
            ReservationCount = dict.ContainsKey("ReservationCount") ? Convert.ToInt32(dict["ReservationCount"]) : 0,
            CreatedBy = dict.ContainsKey("CreatedBy") ? dict["CreatedBy"]?.ToString() ?? "" : "",
            CreatedById = dict.ContainsKey("CreatedById") ? dict["CreatedById"]?.ToString() ?? "" : "",
            CreatedAt = dict.ContainsKey("CreatedAt")
                ? ((Timestamp)dict["CreatedAt"]).ToDateTime()
                : DateTime.UtcNow
        };

        if (dict.ContainsKey("Amenities") && dict["Amenities"] is List<object> rawList)
            room.Amenities = rawList.Select(a => a.ToString() ?? "").Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        return room;
    }

    private RoomDto ConvertToDto(Room room)
    {
        return new RoomDto
        {
            Id = room.Id,
            RoomNumber = room.RoomNumber,
            RoomType = room.RoomType,
            Capacity = room.Capacity,
            Amenities = room.Amenities,
            BaseRate = room.BaseRate,
            Description = room.Description,
            PhotoUrl = room.PhotoUrl,
            ReservationCount = room.ReservationCount,
            CreatedBy = room.CreatedBy,
            CreatedAt = room.CreatedAt
        };
    }
}