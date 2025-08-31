# Bundesliga Crawler - Comprehensive Event Extraction

This application crawls the latest Bundesliga matchday data from the official website and extracts detailed event information using OpenAI's API.

## Features

- **Match Data Retrieval**: Fetches the latest played games from the Bundesliga website
- **Comprehensive Event Extraction**: Uses OpenAI to extract ALL relevant football events:
  - **Goals**: Complete details including shot placement, distance, assist information
  - **Corners**: Who took them, timing, notable outcomes
  - **Free kicks**: Who took them, key actions, outcomes
  - **Penalties**: Who took them, scored/missed status, goalkeeper actions
  - **Yellow/Red cards**: Who received them, timing, reasons
  - **Substitutions**: Player changes, tactical reasons, timing
  - **Saves**: Goalkeeper saves, especially notable ones
  - **Shots**: All shots on/off target, blocked shots with details
  - **Fouls**: Notable fouls, especially those leading to cards
  - **Offside**: Offside calls, especially goal-preventing ones
- **Detailed Goal Analysis**: 
  - Shot placement (corners of goal, crossbar, outside post)
  - Distance from goal
  - Shot type (header, volley, tap-in, etc.)
  - Body part used
  - Assist information
- **Smart Filtering**: Only processes completed matches, skips ongoing games
- **Rich Statistics**: Comprehensive match statistics with detailed breakdowns

## API Endpoints

### GET `/api/spt/latest-matchday`
Crawls the latest Bundesliga matchday and extracts comprehensive events from completed matches.

**Response**: `BundesligaMatchdayResult`
- `Matchday`: Current matchday number
- `Season`: Season identifier (e.g., "2025-2026")
- `CrawledAt`: Timestamp of when the data was crawled
- `TotalGames`: Total number of games in the matchday
- `CompletedGames`: Number of completed games processed
- `Matches`: Array of match results with detailed extracted events

### GET `/api/spt/statistics`
Returns comprehensive aggregated statistics for the latest matchday.

**Response**: `MatchStatistics`
- `TotalCorners`: Total corner kicks across all matches
- `TotalFreeKicks`: Total free kicks across all matches
- `TotalPenalties`: Total penalties across all matches
- `PenaltiesScored`: Number of successful penalties
- `PenaltiesMissed`: Number of missed penalties
- `TotalGoals`: Total goals scored across all matches
- `TotalYellowCards`: Total yellow cards across all matches
- `TotalRedCards`: Total red cards across all matches
- `TotalSubstitutions`: Total substitutions across all matches
- `TotalShots`: Total shots taken
- `ShotsOnTarget`: Shots that were on target
- `ShotsOffTarget`: Shots that were off target
- `TotalSaves`: Total goalkeeper saves
- `TotalFouls`: Total fouls committed
- `TotalOffsides`: Total offside calls
- `GoalsFromCorners`: Goals scored from corner situations
- `GoalsFromPenalties`: Goals scored from penalties
- `GoalsFromFreeKicks`: Goals scored from free kicks
- `HeaderGoals`: Goals scored with headers
- `FootGoals`: Goals scored with feet

## Configuration

Make sure to set the following configuration:
- `OpenAiApiKey`: Your OpenAI API key for event extraction

## Usage Example

```bash
# Get comprehensive matchday data with all events
curl http://localhost:5159/api/spt/latest-matchday

# Get detailed statistics summary
curl http://localhost:5159/api/spt/statistics
```

## How It Works

1. **Web Scraping**: The crawler fetches HTML from the Bundesliga matchday page
2. **Match Parsing**: Extracts team names, scores, match status, and live ticker URLs
3. **Filtering**: Only processes matches that are marked as finished
4. **Comprehensive Event Extraction**: For each completed match:
   - Fetches the live ticker content
   - Sends the text to OpenAI for comprehensive event extraction
   - Parses the detailed JSON response to extract all event types
   - Includes rich details for each event (shot placement, distances, reasons, etc.)
5. **Statistical Analysis**: Combines all match data into comprehensive result object with detailed statistics

## Data Models

### MatchResult
- Match identification and team information
- Score and match status
- List of comprehensive extracted events
- Live ticker URL

### MatchEvent (Enhanced)
- **EventType**: goal, corner, freekick, penalty, yellow_card, red_card, substitution, save, shot, foul, offside
- **Minute**: When the event occurred (including added time)
- **Player**: Primary player involved
- **SecondaryPlayer**: Secondary player (for substitutions, assists, etc.)
- **Team**: Team that performed the action
- **Action**: Description of what happened
- **Outcome**: scored, missed, saved, blocked, deflected, on_target, off_target, other
- **Details**: Rich nested object with event-specific information
- **Notes**: Additional contextual information

### EventDetails (New)
- **Position**: Shot/save placement (left_corner, right_corner, center, crossbar, etc.)
- **Distance**: Distance from goal in meters
- **ShotType**: header, left_foot, right_foot, volley, half_volley, chip, lob, tap_in, etc.
- **AssistPlayer**: Player who provided the assist (for goals)
- **CardReason**: Reason for cards (foul, dissent, time_wasting, etc.)
- **SubstitutionReason**: tactical, injury, performance, time_wasting, other
- **BodyPart**: head, left_foot, right_foot, chest, other

### Enhanced Statistics
The statistics now include comprehensive breakdowns:
- Card analysis (yellow/red cards)
- Shot analysis (on/off target)
- Goal type analysis (headers vs foot goals)
- Situational goals (from corners, penalties, free kicks)
- Defensive actions (saves, blocks)
- Disciplinary actions (fouls, cards)

The crawler is designed to be resilient and will continue processing even if individual matches fail to extract events. The enhanced system provides deep insights into match dynamics, player performance, and tactical patterns.
