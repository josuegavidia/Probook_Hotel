using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Grpc.Auth;
using Newtonsoft.Json.Linq;

namespace Proyecto_Progra_Web.API.Services;

public class FirebaseService
{
    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<FirebaseService> _logger;

    public FirebaseService(ILogger<FirebaseService> logger)
    {
        _logger = logger;

        try
        {
            // 1) Resolver ruta de credenciales:
            //    - Primero: variable de entorno GOOGLE_APPLICATION_CREDENTIALS
            //    - Fallback: archivo local Config/firebase-credentials.json
            var credentialsPath = ResolveCredentialsPath();

            // 2) Extraer project_id del JSON de credenciales
            var projectId = GetProjectIdFromCredentials(credentialsPath);

            // 3) Asegurar que Google SDK tenga la variable de entorno correcta
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);

            // 4) Inicializar FirebaseApp una sola vez
            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(credentialsPath)
                });

                _logger.LogInformation("FirebaseApp inicializada correctamente.");
            }
            else
            {
                _logger.LogInformation("FirebaseApp ya estaba inicializada. Reutilizando instancia.");
            }

            // 5) Crear cliente Firestore
            var firestoreClientBuilder = new FirestoreClientBuilder
            {
                ChannelCredentials = GoogleCredential
                    .FromFile(credentialsPath)
                    .CreateScoped("https://www.googleapis.com/auth/cloud-platform")
                    .ToChannelCredentials()
            };

            var firestoreClient = firestoreClientBuilder.Build();

            // 6) Crear instancia FirestoreDb
            _firestoreDb = FirestoreDb.Create(projectId, firestoreClient);

            _logger.LogInformation("Conexión a Firestore iniciada correctamente para project_id: {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al iniciar Firebase/Firestore.");
            throw;
        }
    }

    private string ResolveCredentialsPath()
    {
        var envPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

        if (!string.IsNullOrWhiteSpace(envPath))
        {
            if (!File.Exists(envPath))
            {
                throw new FileNotFoundException(
                    $"La variable GOOGLE_APPLICATION_CREDENTIALS apunta a un archivo inexistente: {envPath}");
            }

            _logger.LogInformation("Usando credenciales Firebase desde variable de entorno.");
            return envPath;
        }

        var localPath = Path.Combine(AppContext.BaseDirectory, "Config", "firebase-credentials.json");

        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException(
                "No se encontraron credenciales Firebase. " +
                "Define GOOGLE_APPLICATION_CREDENTIALS o coloca Config/firebase-credentials.json en el runtime.",
                localPath);
        }

        _logger.LogInformation("Usando credenciales Firebase desde archivo local: {Path}", localPath);
        return localPath;
    }

    private string GetProjectIdFromCredentials(string credentialsPath)
    {
        var json = File.ReadAllText(credentialsPath);

        var obj = JObject.Parse(json);
        var projectId = obj["project_id"]?.ToString();

        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException(
                $"El archivo de credenciales no contiene 'project_id': {credentialsPath}");
        }

        return projectId;
    }

    public CollectionReference GetCollection(string collectionName)
    {
        return _firestoreDb.Collection(collectionName);
    }

    public async Task RunTransactionAsync(Func<Transaction, Task> updateFunc)
    {
        await _firestoreDb.RunTransactionAsync(updateFunc);
    }
}