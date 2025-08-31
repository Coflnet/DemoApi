# Bundesliga Crawler

This application crawls the latest Bundesliga matchday data from the official website and extracts detailed event information using OpenAI's API.

## Features

- **Match Data Retrieval**: Fetches the latest played games from the Bundesliga website
- **Event Extraction**: Uses OpenAI to extract specific events from live tickers:
  - Corners (who took them, timing, notable details)
  - Free kicks (who took them, key actions, notes)
  - Penalties (who took them, scored/missed status)
- **Smart Filtering**: Only processes completed matches, skips ongoing games
- **Aggregated Results**: Returns comprehensive match statistics

## API Endpoints

### GET `/api/spt/latest-matchday`
Crawls the latest Bundesliga matchday and extracts events from completed matches.

**Response**: `BundesligaMatchdayResult`
- `Matchday`: Current matchday number
- `Season`: Season identifier (e.g., "2025-2026")
- `CrawledAt`: Timestamp of when the data was crawled
- `TotalGames`: Total number of games in the matchday
- `CompletedGames`: Number of completed games processed
- `Matches`: Array of match results with extracted events

### GET `/api/spt/statistics`
Returns aggregated statistics for the latest matchday.

**Response**: `MatchStatistics`
- `TotalCorners`: Total corner kicks across all matches
- `TotalFreeKicks`: Total free kicks across all matches
- `TotalPenalties`: Total penalties across all matches
- `PenaltiesScored`: Number of successful penalties
- `PenaltiesMissed`: Number of missed penalties
- `TotalGoals`: Total goals scored across all matches

## Configuration

Make sure to set the following configuration:
- `OpenAiApiKey`: Your OpenAI API key for event extraction

## Usage Example

```bash
# Get latest matchday data
curl http://localhost:5159/api/spt/latest-matchday

# Get statistics summary
curl http://localhost:5159/api/spt/statistics
```

## How It Works

1. **Web Scraping**: The crawler fetches HTML from the Bundesliga matchday page
2. **Match Parsing**: Extracts team names, scores, match status, and live ticker URLs
3. **Filtering**: Only processes matches that are marked as finished
4. **Event Extraction**: For each completed match:
   - Fetches the live ticker content
   - Sends the text to OpenAI for event extraction
   - Parses the JSON response to extract corners, free kicks, and penalties
5. **Aggregation**: Combines all match data into a comprehensive result object

## Data Models

### MatchResult
- Match identification and team information
- Score and match status
- List of extracted events
- Live ticker URL

### MatchEvent
- Event type (corner, freekick, penalty)
- Minute when the event occurred
- Player involved
- Team that performed the action
- Outcome (scored, missed, saved, etc.)
- Additional notes

The crawler is designed to be resilient and will continue processing even if individual matches fail to extract events.
