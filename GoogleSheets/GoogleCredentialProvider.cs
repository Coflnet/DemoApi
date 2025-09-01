using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;

namespace Coflnet.GoogleSheets;

/// <summary>
/// Service for managing Google OAuth2 credentials for Sheets API access
/// </summary>
public class GoogleCredentialProvider
{
    private readonly ILogger<GoogleCredentialProvider> logger;
    private readonly string applicationName;
    private readonly string[] scopes;

    /// <summary>
    /// Initializes a new instance of the GoogleCredentialProvider
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="applicationName">Application name for OAuth consent</param>
    public GoogleCredentialProvider(ILogger<GoogleCredentialProvider> logger, string applicationName = "DemoApi")
    {
        this.logger = logger;
        this.applicationName = applicationName;
        this.scopes = new[] { SheetsService.Scope.Spreadsheets };
    }

    /// <summary>
    /// Gets Google credentials using service account JSON key file
    /// </summary>
    /// <param name="serviceAccountKeyPath">Path to the service account JSON key file</param>
    /// <returns>Google credential for API access</returns>
    public async Task<GoogleCredential> GetServiceAccountCredentialAsync(string serviceAccountKeyPath)
    {
        try
        {
            if (!File.Exists(serviceAccountKeyPath))
            {
                throw new FileNotFoundException($"Service account key file not found: {serviceAccountKeyPath}");
            }

            logger.LogInformation($"Loading service account credentials from: {serviceAccountKeyPath}");

            using var stream = new FileStream(serviceAccountKeyPath, FileMode.Open, FileAccess.Read);
            var credential = GoogleCredential.FromStream(stream).CreateScoped(scopes);

            logger.LogInformation("Service account credentials loaded successfully");
            return credential;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error loading service account credentials from: {serviceAccountKeyPath}");
            throw;
        }
    }

    /// <summary>
    /// Gets Google credentials using service account JSON content
    /// </summary>
    /// <param name="serviceAccountJson">Service account JSON content as string</param>
    /// <returns>Google credential for API access</returns>
    public GoogleCredential GetServiceAccountCredentialFromJson(string serviceAccountJson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(serviceAccountJson))
            {
                throw new ArgumentException("Service account JSON content cannot be empty");
            }

            logger.LogInformation("Loading service account credentials from JSON content");

            var credential = GoogleCredential.FromJson(serviceAccountJson).CreateScoped(scopes);

            logger.LogInformation("Service account credentials loaded successfully from JSON");
            return credential;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading service account credentials from JSON content");
            throw;
        }
    }

    /// <summary>
    /// Gets Google credentials using OAuth2 user flow (requires user interaction)
    /// </summary>
    /// <param name="clientId">OAuth2 client ID</param>
    /// <param name="clientSecret">OAuth2 client secret</param>
    /// <param name="userId">User identifier for token storage</param>
    /// <param name="dataStorePath">Path to store user tokens (optional)</param>
    /// <returns>User credential for API access</returns>
    public async Task<UserCredential> GetUserCredentialAsync(
        string clientId, 
        string clientSecret, 
        string userId = "user",
        string? dataStorePath = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new ArgumentException("Client ID and Client Secret are required for OAuth2 flow");
            }

            logger.LogInformation($"Starting OAuth2 user flow for user: {userId}");

            var clientSecrets = new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            };

            // Use file-based data store if path is provided, otherwise use in-memory store
            IDataStore dataStore = dataStorePath != null 
                ? new FileDataStore(dataStorePath, true)
                : new MemoryDataStore();

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                clientSecrets,
                scopes,
                userId,
                CancellationToken.None,
                dataStore);

            logger.LogInformation($"OAuth2 user credentials obtained successfully for user: {userId}");
            return credential;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error obtaining OAuth2 user credentials for user: {userId}");
            throw;
        }
    }

    /// <summary>
    /// Gets Google credentials using OAuth2 user flow with client secrets file
    /// </summary>
    /// <param name="clientSecretsPath">Path to the client secrets JSON file</param>
    /// <param name="userId">User identifier for token storage</param>
    /// <param name="dataStorePath">Path to store user tokens (optional)</param>
    /// <returns>User credential for API access</returns>
    public async Task<UserCredential> GetUserCredentialFromFileAsync(
        string clientSecretsPath, 
        string userId = "user",
        string? dataStorePath = null)
    {
        try
        {
            if (!File.Exists(clientSecretsPath))
            {
                throw new FileNotFoundException($"Client secrets file not found: {clientSecretsPath}");
            }

            logger.LogInformation($"Loading OAuth2 client secrets from: {clientSecretsPath}");

            using var stream = new FileStream(clientSecretsPath, FileMode.Open, FileAccess.Read);

            // Use file-based data store if path is provided, otherwise use in-memory store
            IDataStore dataStore = dataStorePath != null 
                ? new FileDataStore(dataStorePath, true)
                : new MemoryDataStore();

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                scopes,
                userId,
                CancellationToken.None,
                dataStore);

            logger.LogInformation($"OAuth2 user credentials obtained successfully from file for user: {userId}");
            return credential;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error obtaining OAuth2 user credentials from file: {clientSecretsPath}");
            throw;
        }
    }

    /// <summary>
    /// Refreshes an existing user credential
    /// </summary>
    /// <param name="credential">The user credential to refresh</param>
    /// <returns>True if refresh was successful</returns>
    public async Task<bool> RefreshCredentialAsync(UserCredential credential)
    {
        try
        {
            logger.LogInformation("Refreshing user credential");

            var success = await credential.RefreshTokenAsync(CancellationToken.None);
            
            if (success)
            {
                logger.LogInformation("User credential refreshed successfully");
            }
            else
            {
                logger.LogWarning("Failed to refresh user credential");
            }

            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing user credential");
            return false;
        }
    }

    /// <summary>
    /// Revokes a user credential (logs out the user)
    /// </summary>
    /// <param name="credential">The user credential to revoke</param>
    /// <returns>True if revocation was successful</returns>
    public async Task<bool> RevokeCredentialAsync(UserCredential credential)
    {
        try
        {
            logger.LogInformation("Revoking user credential");

            var success = await credential.RevokeTokenAsync(CancellationToken.None);
            
            if (success)
            {
                logger.LogInformation("User credential revoked successfully");
            }
            else
            {
                logger.LogWarning("Failed to revoke user credential");
            }

            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error revoking user credential");
            return false;
        }
    }
}

/// <summary>
/// Configuration options for Google Sheets authentication
/// </summary>
public class GoogleSheetsAuthOptions
{
    /// <summary>
    /// Authentication method to use
    /// </summary>
    public AuthMethod Method { get; set; } = AuthMethod.ServiceAccount;

    /// <summary>
    /// Service account key file path (for service account auth)
    /// </summary>
    public string? ServiceAccountKeyPath { get; set; }

    /// <summary>
    /// Service account JSON content (for service account auth)
    /// </summary>
    public string? ServiceAccountJson { get; set; }

    /// <summary>
    /// OAuth2 client ID (for user auth)
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// OAuth2 client secret (for user auth)
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Client secrets file path (for user auth)
    /// </summary>
    public string? ClientSecretsPath { get; set; }

    /// <summary>
    /// User ID for token storage (for user auth)
    /// </summary>
    public string UserId { get; set; } = "user";

    /// <summary>
    /// Path to store user tokens (for user auth)
    /// </summary>
    public string? DataStorePath { get; set; }

    /// <summary>
    /// Application name for Google API requests
    /// </summary>
    public string ApplicationName { get; set; } = "DemoApi";
}

/// <summary>
/// Authentication methods for Google Sheets API
/// </summary>
public enum AuthMethod
{
    /// <summary>
    /// Service account authentication (server-to-server)
    /// </summary>
    ServiceAccount,

    /// <summary>
    /// OAuth2 user authentication (requires user interaction)
    /// </summary>
    UserOAuth2
}

/// <summary>
/// In-memory data store for OAuth2 tokens (tokens are lost when application restarts)
/// </summary>
public class MemoryDataStore : IDataStore
{
    private readonly Dictionary<string, byte[]> store = new();

    public Task StoreAsync<T>(string key, T value)
    {
        var serialized = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
        store[key] = serialized;
        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(string key)
    {
        store.Remove(key);
        return Task.CompletedTask;
    }

    public Task<T> GetAsync<T>(string key)
    {
        if (store.TryGetValue(key, out var data))
        {
            var value = System.Text.Json.JsonSerializer.Deserialize<T>(data);
            return Task.FromResult(value!);
        }
        return Task.FromResult(default(T)!);
    }

    public Task ClearAsync()
    {
        store.Clear();
        return Task.CompletedTask;
    }
}
