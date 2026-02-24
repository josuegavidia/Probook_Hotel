namespace Proyecto_Progra_Web.API.Models
{

    /// <summary>
    /// Movie representa una pelicula o serie en la base de datos
    /// Se almacena como un documento en la coleccion "movies en Firestore
    /// </summary>
    public class Movie
    {
        /* Id: Identificador unico de la pelicula
         * En firestores, es el documento ID (autogenera)
         * Ejemplo: movie_001, etc..
         */

        public string Id { get; set; } = string.Empty;

        // Titulo
        // Titulo de la pelicula o serie
        // 007: golden eye

        public string Title { get; set; } = string.Empty ;

        // Description
        // DE QUE TRATA

        public string Description { get; set; } = string.Empty;

        //Genero
        //Terror, accion..

        public string Genre { get; set; } = string.Empty;

        //Año
        //2020, 2021

        public string ReleaseYear { get; set; }

        //Poster
        // Almacena una URL de la imagen de la pelicula
        // Se almacena en Firestore

        public string PosterUrl { get; set; } = string.Empty;

        //Promedio
        //Calificacion promedio de todos los usuarios
        //Se Calcula automaticamente a partir de los ratings
        //0.0 - 10.0

        public double AverageRating {  get; set; }

        //Rating Total
        //Cantidad de calificaciones recibidas
        //Se incrementa cada vez que un usuario califica la pelicula

        public int TotalRating { get; set; }

        /*
         * Cuando se creó
         * Fecha y hora que se agrego la pelicula
         * Se asigna automaticamente al crear el documneto 
         */

        public DateTime CreatedAt { get; set; }

        /*
         * Creado por ID del usuario que creo la pelicula (Admin)
         * Solo los admins pueden ingresar peliculas
         * 
         */

        public string UpdatedBy { get; set; }


    }
}
