using System.Text;
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
        // 1. Intentar desde variable de entorno (base64)
        var base64Env = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_BASE64");
        if (!string.IsNullOrWhiteSpace(base64Env))
        {
            try
            {
                var jsonBytes = Convert.FromBase64String(base64Env);
                var json = Encoding.UTF8.GetString(jsonBytes);
                var tempPath = Path.Combine(Path.GetTempPath(), "firebase-credentials.json");
                File.WriteAllText(tempPath, json);
                _logger.LogInformation("Usando credenciales Firebase desde variable base64.");
                return tempPath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error decodificando base64: {ex.Message}");
            }
        }

        // 2. Intentar desde variable de entorno directa
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

        // 3. Fallback: archivo local
        var localPath = Path.Combine(AppContext.BaseDirectory, "Config", "firebase-credentials.json");

        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException(
                "No se encontraron credenciales Firebase. " +
                "Define FIREBASE_CREDENTIALS_BASE64 o GOOGLE_APPLICATION_CREDENTIALS o coloca Config/firebase-credentials.json en el runtime.",
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