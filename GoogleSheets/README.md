# Google Sheets Integration

This document describes how to set up and use the Google Sheets integration in the DemoApi project. The integration allows you to upload CSV data to Google Sheets using either service account authentication or OAuth2 user authentication.

## Table of Contents

1. [Authentication Methods](#authentication-methods)
2. [Service Account Setup](#service-account-setup)
3. [OAuth2 User Authentication Setup](#oauth2-user-authentication-setup)
4. [Configuration](#configuration)
5. [API Endpoints](#api-endpoints)
6. [Usage Examples](#usage-examples)
7. [Troubleshooting](#troubleshooting)

## Authentication Methods

The Google Sheets service supports two authentication methods:

### 1. Service Account Authentication (Recommended for Server Applications)
- **Use Case**: Server-to-server authentication without user interaction
- **Pros**: No user interaction required, works in automated environments
- **Cons**: Requires Google Cloud Console setup, spreadsheets must be shared with service account

### 2. OAuth2 User Authentication
- **Use Case**: User-based authentication with user consent
- **Pros**: Access to user's personal Google Sheets
- **Cons**: Requires user interaction, tokens need refresh management

## Service Account Setup

### Step 1: Create a Google Cloud Project

1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Note your project ID

### Step 2: Enable Google Sheets API

1. In the Google Cloud Console, go to "APIs & Services" > "Library"
2. Search for "Google Sheets API"
3. Click on it and click "Enable"

### Step 3: Create a Service Account

1. Go to "APIs & Services" > "Credentials"
2. Click "Create Credentials" > "Service Account"
3. Enter a name for your service account (e.g., "sheets-api-service")
4. Click "Create and Continue"
5. Skip granting roles for now (click "Continue")
6. Click "Done"

### Step 4: Create and Download Service Account Key

1. In the Credentials page, find your service account
2. Click on the service account email
3. Go to the "Keys" tab
4. Click "Add Key" > "Create New Key"
5. Select "JSON" format
6. Click "Create"
7. Save the downloaded JSON file securely

### Step 5: Share Spreadsheets with Service Account

For each Google Sheets document you want to access:

1. Open the Google Sheets document
2. Click "Share" in the top-right corner
3. Enter the service account email address (found in the JSON file as `client_email`)
4. Grant "Editor" permissions
5. Click "Send"

### Step 6: Configure the Application

Update your `appsettings.json`:

```json
{
  "GoogleSheets": {
    "Auth": {
      "Method": "ServiceAccount",
      "ServiceAccountKeyPath": "/path/to/your/service-account-key.json",
      "ApplicationName": "DemoApi"
    }
  }
}
```

Or use environment variables:

```bash
export GOOGLE_SHEETS_AUTH_METHOD="ServiceAccount"
export GOOGLE_SHEETS_SERVICE_ACCOUNT_KEY_PATH="/path/to/service-account-key.json"
```

Alternatively, you can embed the JSON content directly:

```json
{
  "GoogleSheets": {
    "Auth": {
      "Method": "ServiceAccount",
      "ServiceAccountJson": "{\"type\":\"service_account\",\"project_id\":\"your-project\",\"private_key_id\":\"...\",\"private_key\":\"...\",\"client_email\":\"...\",\"client_id\":\"...\",\"auth_uri\":\"...\",\"token_uri\":\"...\"}",
      "ApplicationName": "DemoApi"
    }
  }
}
```

## OAuth2 User Authentication Setup

### Step 1: Create OAuth2 Credentials

1. In the Google Cloud Console, go to "APIs & Services" > "Credentials"
2. Click "Create Credentials" > "OAuth client ID"
3. If prompted, configure the OAuth consent screen first:
   - Choose "External" user type (unless you have Google Workspace)
   - Fill in the required information
   - Add your email to test users during development
4. Select "Desktop application" as the application type
5. Enter a name for your OAuth client
6. Click "Create"
7. Download the client secrets JSON file

### Step 2: Configure the Application

Update your `appsettings.json`:

```json
{
  "GoogleSheets": {
    "Auth": {
      "Method": "UserOAuth2",
      "ClientSecretsPath": "/path/to/client-secrets.json",
      "UserId": "user",
      "DataStorePath": "./tokens",
      "ApplicationName": "DemoApi"
    }
  }
}
```

Or use individual credentials:

```json
{
  "GoogleSheets": {
    "Auth": {
      "Method": "UserOAuth2",
      "ClientId": "your-client-id.googleusercontent.com",
      "ClientSecret": "your-client-secret",
      "UserId": "user",
      "DataStorePath": "./tokens",
      "ApplicationName": "DemoApi"
    }
  }
}
```

## Configuration

### Configuration Options

| Setting | Description | Required | Example |
|---------|-------------|----------|---------|
| `Method` | Authentication method | Yes | `ServiceAccount` or `UserOAuth2` |
| `ServiceAccountKeyPath` | Path to service account JSON file | For service account | `/path/to/key.json` |
| `ServiceAccountJson` | Service account JSON content | For service account | `{"type":"service_account",...}` |
| `ClientId` | OAuth2 client ID | For user auth | `123...googleusercontent.com` |
| `ClientSecret` | OAuth2 client secret | For user auth | `GOCSPX-...` |
| `ClientSecretsPath` | Path to client secrets JSON file | For user auth | `/path/to/secrets.json` |
| `UserId` | User identifier for token storage | For user auth | `user` |
| `DataStorePath` | Path to store user tokens | For user auth | `./tokens` |
| `ApplicationName` | Application name for API requests | No | `DemoApi` |

### Environment Variables

You can also use environment variables for configuration:

```bash
# Service Account
export GOOGLE_SHEETS_AUTH_METHOD="ServiceAccount"
export GOOGLE_SHEETS_SERVICE_ACCOUNT_KEY_PATH="/path/to/key.json"

# Or OAuth2
export GOOGLE_SHEETS_AUTH_METHOD="UserOAuth2"
export GOOGLE_SHEETS_CLIENT_ID="your-client-id"
export GOOGLE_SHEETS_CLIENT_SECRET="your-client-secret"
```

## API Endpoints

### 1. Upload CSV to Existing Spreadsheet

**POST** `/api/GoogleSheets/upload-csv`

Uploads CSV data to an existing Google Sheets document.

**Request Body:**
```json
{
  "spreadsheetId": "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms",
  "sheetName": "Sheet1",
  "csvData": "Name,Age,City\nJohn,30,New York\nJane,25,Los Angeles",
  "hasHeaders": true,
  "clearExistingData": true,
  "authOptions": {
    "method": "ServiceAccount",
    "serviceAccountKeyPath": "/path/to/key.json"
  }
}
```

**Response:**
```json
{
  "success": true,
  "spreadsheetId": "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms",
  "sheetName": "Sheet1",
  "rowsUpdated": 3,
  "columnsUpdated": 3,
  "cellsUpdated": 9,
  "message": "Successfully uploaded 3 rows to sheet 'Sheet1'"
}
```

### 2. Create New Spreadsheet with CSV Data

**POST** `/api/GoogleSheets/create-spreadsheet`

Creates a new Google Sheets document and populates it with CSV data.

**Request Body:**
```json
{
  "title": "My New Spreadsheet",
  "sheetName": "Data",
  "csvData": "Name,Age,City\nJohn,30,New York\nJane,25,Los Angeles",
  "hasHeaders": true,
  "authOptions": {
    "method": "ServiceAccount",
    "serviceAccountKeyPath": "/path/to/key.json"
  }
}
```

**Response:**
```json
{
  "success": true,
  "spreadsheetId": "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms",
  "spreadsheetUrl": "https://docs.google.com/spreadsheets/d/1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms/edit",
  "title": "My New Spreadsheet",
  "sheetName": "Data",
  "rowsAdded": 3,
  "message": "Successfully created spreadsheet 'My New Spreadsheet' with 3 rows"
}
```

### 3. Get Spreadsheet Information

**GET** `/api/GoogleSheets/spreadsheet/{spreadsheetId}`

Retrieves information about a Google Sheets document.

**Response:**
```json
{
  "spreadsheetId": "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms",
  "title": "My Spreadsheet",
  "url": "https://docs.google.com/spreadsheets/d/1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms/edit",
  "sheets": [
    {
      "sheetId": 0,
      "title": "Sheet1",
      "rowCount": 1000,
      "columnCount": 26
    }
  ]
}
```

### 4. Upload Bundesliga Data

**POST** `/api/GoogleSheets/upload-bundesliga-data`

Uploads Bundesliga match data in CSV format to Google Sheets.

**Request Body:**
```json
{
  "spreadsheetId": "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms",
  "sheetName": "Bundesliga_Matches",
  "dataType": "Matches",
  "clearExistingData": true,
  "authOptions": {
    "method": "ServiceAccount",
    "serviceAccountKeyPath": "/path/to/key.json"
  }
}
```

**Data Types:**
- `Matches`: Match results and basic information
- `Events`: All match events (goals, cards, substitutions, etc.)
- `Goals`: Goal-specific data with details
- `Cards`: Card-specific data
- `Statistics`: Match statistics

## Usage Examples

### Example 1: Service Account Authentication

```csharp
// This is handled automatically by the API when configured properly
// Just make API calls with the spreadsheet ID
```

### Example 2: Upload CSV Data via API

```bash
curl -X POST "https://your-api.com/api/GoogleSheets/upload-csv" \
  -H "Content-Type: application/json" \
  -d '{
    "spreadsheetId": "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms",
    "sheetName": "MyData",
    "csvData": "Name,Age\nJohn,30\nJane,25",
    "hasHeaders": true,
    "clearExistingData": true
  }'
```

### Example 3: Create New Spreadsheet

```bash
curl -X POST "https://your-api.com/api/GoogleSheets/create-spreadsheet" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Sales Data 2024",
    "sheetName": "Q1",
    "csvData": "Month,Sales\nJan,1000\nFeb,1200\nMar,1100",
    "hasHeaders": true
  }'
```

### Example 4: Upload Bundesliga Match Data

```bash
curl -X POST "https://your-api.com/api/GoogleSheets/upload-bundesliga-data" \
  -H "Content-Type: application/json" \
  -d '{
    "spreadsheetId": "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms",
    "sheetName": "Matches",
    "dataType": "Matches",
    "clearExistingData": true
  }'
```

## Troubleshooting

### Common Issues

#### 1. "Service account credentials not configured"

**Cause**: Missing or invalid service account configuration.

**Solution**: 
- Ensure `ServiceAccountKeyPath` points to a valid JSON file
- Or ensure `ServiceAccountJson` contains valid JSON content
- Check file permissions

#### 2. "The caller does not have permission"

**Cause**: Service account doesn't have access to the spreadsheet.

**Solution**:
- Share the spreadsheet with the service account email
- Grant "Editor" permissions
- Check the service account email in the JSON file (`client_email` field)

#### 3. "Spreadsheet not found"

**Cause**: Invalid spreadsheet ID or no access.

**Solution**:
- Verify the spreadsheet ID is correct
- Ensure the spreadsheet is shared with the service account
- Check if the spreadsheet exists

#### 4. "Authentication failed"

**Cause**: Invalid credentials or expired tokens.

**Solution**:
- For service accounts: Check the JSON file is valid and not corrupted
- For OAuth2: Delete stored tokens and re-authenticate
- Verify API is enabled in Google Cloud Console

#### 5. "CSV data is empty or invalid"

**Cause**: Invalid or empty CSV data.

**Solution**:
- Ensure CSV data is properly formatted
- Check for proper escaping of quotes and commas
- Verify data is not empty

### Debug Steps

1. **Check Configuration**:
   ```bash
   # Verify configuration is loaded correctly
   GET /api/GoogleSheets/spreadsheet/test-id
   ```

2. **Test Service Account**:
   ```bash
   # Try to access a known spreadsheet
   curl -X GET "https://your-api.com/api/GoogleSheets/spreadsheet/1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms"
   ```

3. **Check Logs**:
   - Look for authentication errors in application logs
   - Check for Google API error messages
   - Verify service registration in Program.cs

4. **Validate Permissions**:
   - Ensure Google Sheets API is enabled
   - Check IAM permissions in Google Cloud Console
   - Verify spreadsheet sharing settings

### Security Best Practices

1. **Service Account Keys**:
   - Store JSON files securely
   - Use environment variables for sensitive data
   - Rotate keys regularly
   - Don't commit keys to version control

2. **OAuth2 Tokens**:
   - Store tokens securely
   - Implement proper token refresh logic
   - Use HTTPS for all communications
   - Validate token expiration

3. **Access Control**:
   - Use principle of least privilege
   - Share spreadsheets only with necessary accounts
   - Monitor access logs
   - Implement API rate limiting

### Performance Considerations

1. **Batch Operations**:
   - Upload data in batches when possible
   - Use bulk operations for multiple sheets
   - Implement retry logic for transient failures

2. **Caching**:
   - Cache spreadsheet metadata
   - Reuse service instances
   - Implement connection pooling

3. **Rate Limiting**:
   - Google Sheets API has rate limits
   - Implement exponential backoff
   - Monitor quota usage in Google Cloud Console
