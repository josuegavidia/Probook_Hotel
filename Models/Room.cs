namespace Proyecto_Progra_Web.API.Models
{
    public class Room
    {

        public string Id { get; set; } = string.Empty;

        public string RoomNumber { get; set; } = string.Empty;

        public string RoomType {  get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PosterUrl { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

    }
}
