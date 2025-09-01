using System.Text.Json;
using HtmlAgilityPack;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.SharedModels;

namespace Coflnet.Spt;

public class BundesligaCrawler
{
    private readonly HttpClient httpClient;
    private readonly IOpenAIService openAIService;
    private readonly ILogger<BundesligaCrawler> logger;

    public BundesligaCrawler(HttpClient httpClient, IOpenAIService openAIService, ILogger<BundesligaCrawler> logger)
    {
        this.httpClient = httpClient;
        this.openAIService = openAIService;
        this.logger = logger;
    }

    public async Task<BundesligaMatchdayResult> CrawlLatestMatchdayAsync()
    {
        try
        {
            var url = "https://www.bundesliga.com/de/bundesliga/spieltag/2025-2026/1/";
            logger.LogInformation($"Starting to crawl Bundesliga matchday from: {url}");
            
            var html = await httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            var games = ParseGamesFromPageAsync(doc);
            var completedGames = new List<MatchResult>();
            
            foreach (var game in games)
            {
                if (!game.IsFinished)
                {
                    logger.LogInformation($"Skipping game {game.HomeTeam} vs {game.AwayTeam} - not finished yet");
                    continue;
                }
                
                logger.LogInformation($"Processing completed game: {game.HomeTeam} vs {game.AwayTeam}");
                var events = await ExtractGameEventsAsync(game);
                game.Events = events;
                completedGames.Add(game);
            }
            
            var result = new BundesligaMatchdayResult
            {
                Matchday = 1,
                Season = "2025-2026",
                CrawledAt = DateTime.UtcNow,
                TotalGames = games.Count,
                CompletedGames = completedGames.Count,
                Matches = completedGames
            };
            
            logger.LogInformation($"Crawling completed. Processed {completedGames.Count} of {games.Count} games");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Bundesliga crawling");
            throw;
        }
    }

    private List<MatchResult> ParseGamesFromPageAsync(HtmlDocument doc)
    {
        var games = new List<MatchResult>();
        
        // Look for match containers using the correct selector
        var gameNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'matchRow')]");
        
        if (gameNodes == null)
        {
            logger.LogWarning("No game nodes found on the page");
            return games;
        }
        
        logger.LogInformation($"Found {gameNodes.Count} potential match nodes");
        
        foreach (var gameNode in gameNodes)
        {
            var game = ParseGameFromNode(gameNode);
            if (game != null)
            {
                games.Add(game);
            }
        }
        
        return games;
    }

    private MatchResult? ParseGameFromNode(HtmlNode gameNode)
    {
        try
        {
            // Extract team names
            var homeTeam = ExtractTeamName(gameNode, true);
            var awayTeam = ExtractTeamName(gameNode, false);
            
            if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(awayTeam))
            {
                logger.LogDebug("Could not extract team names from game node");
                return null;
            }
            
            // Extract score and match status
            var scoreInfo = ExtractScoreAndStatus(gameNode);
            
            // Extract date/time
            var matchDateTime = ExtractMatchDateTime(gameNode);
            
            // Extract live ticker URL
            var liveTickerUrl = ExtractLiveTickerUrl(gameNode);
            
            var match = new MatchResult
            {
                MatchId = GenerateMatchId(homeTeam, awayTeam, matchDateTime),
                HomeTeam = homeTeam,
                AwayTeam = awayTeam,
                HomeScore = scoreInfo.HomeScore,
                AwayScore = scoreInfo.AwayScore,
                Status = scoreInfo.Status,
                IsFinished = scoreInfo.IsFinished,
                MatchDateTime = matchDateTime,
                LiveTickerUrl = liveTickerUrl
            };
            
            logger.LogDebug($"Parsed match: {match.HomeTeam} vs {match.AwayTeam} - Status: {match.Status}");
            return match;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing game node");
            return null;
        }
    }

    private string ExtractTeamName(HtmlNode gameNode, bool isHome)
    {
        // Look for match-team elements with side attribute
        var teamSelector = isHome ? ".//match-team[@side='home']" : ".//match-team[@side='away']";
        var teamNode = gameNode.SelectSingleNode(teamSelector);
        
        if (teamNode != null)
        {
            // Try to get the full team name (visible on larger screens)
            var fullNameNode = teamNode.SelectSingleNode(".//div[contains(@class, 'd-lg-block')]");
            if (fullNameNode != null)
            {
                var fullName = fullNameNode.InnerText?.Trim();
                if (!string.IsNullOrEmpty(fullName))
                    return fullName;
            }
            
            // Fallback to short name (visible on medium screens)
            var shortNameNode = teamNode.SelectSingleNode(".//div[contains(@class, 'd-md-block')]");
            if (shortNameNode != null)
            {
                var shortName = shortNameNode.InnerText?.Trim();
                if (!string.IsNullOrEmpty(shortName))
                    return shortName;
            }
        }
        
        logger.LogDebug($"Could not extract team name for {(isHome ? "home" : "away")} team");
        return string.Empty;
    }

    private (int? HomeScore, int? AwayScore, string Status, bool IsFinished) ExtractScoreAndStatus(HtmlNode gameNode)
    {
        // Look for score-bug element
        var scoreBugNode = gameNode.SelectSingleNode(".//score-bug");
        if (scoreBugNode != null)
        {
            var homeScoreNode = scoreBugNode.SelectSingleNode(".//div[contains(@class, 'cell home')]//div[contains(@class, 'score')]");
            var awayScoreNode = scoreBugNode.SelectSingleNode(".//div[contains(@class, 'cell away')]//div[contains(@class, 'score')]");
            
            if (homeScoreNode != null && awayScoreNode != null)
            {
                var homeScoreText = homeScoreNode.InnerText?.Trim();
                var awayScoreText = awayScoreNode.InnerText?.Trim();
                
                if (int.TryParse(homeScoreText, out var homeScore) && 
                    int.TryParse(awayScoreText, out var awayScore))
                {
                    // Check if match is finished by looking for match-finished class
                    var isFinished = gameNode.SelectSingleNode(".//div[contains(@class, 'match-finished')]") != null;
                    var status = isFinished ? "Finished" : "Live";
                    
                    logger.LogDebug($"Extracted score: {homeScore}:{awayScore}, Status: {status}");
                    return (homeScore, awayScore, status, isFinished);
                }
            }
        }
        
        // Check if match is finished by looking for match-finished class
        var matchFinished = gameNode.SelectSingleNode(".//div[contains(@class, 'match-finished')]") != null;
        var matchStatus = matchFinished ? "Finished" : "Scheduled";
        
        logger.LogDebug($"No score found, Status: {matchStatus}");
        return (null, null, matchStatus, matchFinished);
    }

    private (int? HomeScore, int? AwayScore, string Status, bool IsFinished) ParseScoreText(string scoreText)
    {
        // Try to parse score like "2:1" or "2-1"
        var scoreParts = scoreText.Split(new[] { ':', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (scoreParts.Length == 2 &&
            int.TryParse(scoreParts[0], out var homeScore) &&
            int.TryParse(scoreParts[1], out var awayScore))
        {
            return (homeScore, awayScore, "Finished", true);
        }
        
        // Check for common status indicators
        var isFinished = scoreText.Contains("FT") || 
                        scoreText.Contains("Ended") || 
                        scoreText.Contains("Final") ||
                        scoreText.Contains(":") ||
                        scoreText.Contains("-");
        
        return (null, null, scoreText, isFinished);
    }

    private DateTime? ExtractMatchDateTime(HtmlNode gameNode)
    {
        var timeNode = gameNode.SelectSingleNode(".//time") ??
                      gameNode.SelectSingleNode(".//span[contains(@class, 'date')]") ??
                      gameNode.SelectSingleNode(".//div[contains(@class, 'date')]");
        
        if (timeNode != null)
        {
            var dateTimeStr = timeNode.GetAttributeValue("datetime", "") ?? timeNode.InnerText?.Trim();
            if (!string.IsNullOrEmpty(dateTimeStr) && DateTime.TryParse(dateTimeStr, out var result))
            {
                return result;
            }
        }
        
        return null;
    }

    private string? ExtractLiveTickerUrl(HtmlNode gameNode)
    {
        var linkNode = gameNode.SelectSingleNode(".//a[@href and contains(@class, 'matchFixture')]");
        if (linkNode != null)
        {
            var href = linkNode.GetAttributeValue("href", "");
            if (!string.IsNullOrEmpty(href))
            {
                return href.StartsWith("http") ? href : $"https://www.bundesliga.com{href}";
            }
        }
        
        return null;
    }

    private string GenerateMatchId(string homeTeam, string awayTeam, DateTime? matchDateTime)
    {
        var dateStr = matchDateTime?.ToString("yyyyMMdd") ?? DateTime.Now.ToString("yyyyMMdd");
        return $"{homeTeam.Replace(" ", "")}-{awayTeam.Replace(" ", "")}-{dateStr}";
    }

    private async Task<List<MatchEvent>> ExtractGameEventsAsync(MatchResult match)
    {
        if (string.IsNullOrEmpty(match.LiveTickerUrl))
        {
            logger.LogWarning($"No live ticker URL for match {match.HomeTeam} vs {match.AwayTeam}");
            return new List<MatchEvent>();
        }
        
        try
        {
            var liveTickerText = await GetLiveTickerTextAsync(match.LiveTickerUrl);
            if (string.IsNullOrEmpty(liveTickerText))
            {
                logger.LogWarning($"No live ticker text found for match {match.MatchId}");
                return new List<MatchEvent>();
            }
            
            var events = await ExtractEventsUsingOpenAI(liveTickerText, match);
            logger.LogInformation($"Extracted {events.Count} events for match {match.MatchId}");
            return events;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error extracting events for match {match.MatchId}");
            return new List<MatchEvent>();
        }
    }

    private async Task<string> GetLiveTickerTextAsync(string url)
    {
        try
        {
            logger.LogDebug($"Fetching live ticker from: {url}");
            var html = await httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            // Try different selectors for live ticker content
            var tickerSelectors = new[]
            {
                "//div[contains(@class, 'live-ticker')]",
                "//div[contains(@class, 'match-events')]",
                "//div[contains(@class, 'timeline')]",
                "//div[contains(@class, 'commentary')]",
                "//section[contains(@class, 'live-ticker')]"
            };
            
            foreach (var selector in tickerSelectors)
            {
                var tickerNodes = doc.DocumentNode.SelectNodes(selector);
                if (tickerNodes != null && tickerNodes.Any())
                {
                    var tickerText = string.Join("\n", tickerNodes.Select(n => n.InnerText?.Trim()))
                                          .Replace("\n\n", "\n")
                                          .Trim();
                    
                    if (!string.IsNullOrEmpty(tickerText) && tickerText.Length > 100)
                    {
                        logger.LogDebug($"Found live ticker content with {tickerText.Length} characters");
                        return tickerText;
                    }
                }
            }
            
            // Fallback: get all text content if no specific ticker found
            var allText = doc.DocumentNode.InnerText?.Trim();
            logger.LogDebug($"Using fallback text content with {allText?.Length ?? 0} characters");
            return allText ?? string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error retrieving live ticker from {url}");
            return string.Empty;
        }
    }

    private async Task<List<MatchEvent>> ExtractEventsUsingOpenAI(string liveTickerText, MatchResult match)
    {
        var prompt = $@"
Analyze the following football match live ticker text for the game between {match.HomeTeam} and {match.AwayTeam}.

Extract ALL relevant football events and return them as a JSON array. Include these event types:

1. **Goals**: Who scored, minute, how it was scored, goal details
2. **Corners**: Who took them, when (minute), and any notable details
3. **Free kicks**: Who took them, key action, and notes  
4. **Penalties**: Who took them and whether they scored or missed
5. **Yellow cards**: Who received them, minute, reason if mentioned
6. **Red cards**: Who received them, minute, reason (second yellow, direct red, etc.)
7. **Substitutions**: Who came on/off, minute, tactical reason if mentioned
8. **Saves**: Goalkeeper saves, especially notable ones
9. **Shots**: Notable shots on/off target, blocked shots
10. **Fouls**: Notable fouls, especially those leading to cards or free kicks
11. **Offside**: Offside calls, especially those that prevented goals

For each event, use this exact JSON structure:
{{
  ""eventType"": ""goal"" | ""corner"" | ""freekick"" | ""penalty"" | ""yellow_card"" | ""red_card"" | ""substitution"" | ""save"" | ""shot"" | ""foul"" | ""offside"",
  ""minute"": number,
  ""player"": ""primary player name"",
  ""secondaryPlayer"": ""secondary player name (for substitutions: player coming off, for assists, etc.)"",
  ""team"": ""team name"",
  ""action"": ""description of what happened"",
  ""outcome"": ""scored"" | ""missed"" | ""saved"" | ""blocked"" | ""deflected"" | ""on_target"" | ""off_target"" | ""other"",
  ""details"": {{
    ""position"": ""left_corner"" | ""right_corner"" | ""center"" | ""top_left"" | ""top_right"" | ""bottom_left"" | ""bottom_right"" | ""outside_post"" | ""crossbar"" | ""other"",
    ""distance"": ""distance from goal in meters (if mentioned)"",
    ""shotType"": ""header"" | ""left_foot"" | ""right_foot"" | ""volley"" | ""half_volley"" | ""chip"" | ""lob"" | ""tap_in"" | ""other"",
    ""assistPlayer"": ""player who assisted (for goals)"",
    ""cardReason"": ""reason for card (foul, dissent, time_wasting, etc.)"",
    ""substitutionReason"": ""tactical"" | ""injury"" | ""performance"" | ""time_wasting"" | ""other"",
    ""bodyPart"": ""head"" | ""left_foot"" | ""right_foot"" | ""chest"" | ""other""
  }},
  ""notes"": ""additional relevant details, context, or notable circumstances""
}}

Detailed extraction rules:
- **Goals**: Include shot placement (corners of goal), distance if mentioned, type of shot, assist details
- **Penalties**: Note if saved, direction of shot, keeper's action
- **Cards**: Always include reason if mentioned (foul type, dissent, etc.)
- **Substitutions**: Note if tactical, injury-related, or performance-based
- **Shots**: Include target accuracy, save details, shot type
- **Saves**: Note difficulty, shot type saved, body part used
- Be precise with minute timing including added time (e.g., 90+3)
- Extract player names accurately
- For goals, try to determine shot placement in goal (left/right corner, center, etc.)
- Note distance from goal for shots when mentioned
- Include assist information for goals when available

Live ticker text:
{liveTickerText}

Return ONLY a valid JSON array, no additional text or explanations.
";

        try
        {
            var response = await openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest()
            {
                Model = Models.Gpt_4o_mini,
                Messages = new List<ChatMessage>()
                {
                    ChatMessage.FromSystem("You are a comprehensive football event extractor. Extract ALL relevant match events including goals, cards, substitutions, shots, saves, fouls, corners, free kicks, penalties, and offsides. Return detailed information as structured JSON with nested details object for each event."),
                    ChatMessage.FromUser(prompt)
                }
            });

            if (!response.Successful || response.Choices?.FirstOrDefault()?.Message?.Content == null)
            {
                logger.LogError("Failed to get completion from OpenAI");
                return new List<MatchEvent>();
            }

            var content = response.Choices.First().Message.Content;
            
            // Clean the response in case it has markdown formatting
            var cleanResponse = content?.Trim() ?? string.Empty;
            if (cleanResponse.StartsWith("```json"))
            {
                cleanResponse = cleanResponse.Substring(7);
            }
            if (cleanResponse.EndsWith("```"))
            {
                cleanResponse = cleanResponse.Substring(0, cleanResponse.Length - 3);
            }
            cleanResponse = cleanResponse.Trim();
            
            var events = JsonSerializer.Deserialize<List<MatchEvent>>(cleanResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return events ?? new List<MatchEvent>();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, $"Failed to parse OpenAI response as JSON for match {match.MatchId}");
            return new List<MatchEvent>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling OpenAI service");
            return new List<MatchEvent>();
        }
    }
}

public class BundesligaMatchdayResult
{
    public int Matchday { get; set; }
    public string Season { get; set; } = string.Empty;
    public DateTime CrawledAt { get; set; }
    public int TotalGames { get; set; }
    public int CompletedGames { get; set; }
    public List<MatchResult> Matches { get; set; } = new();
    
    public MatchStatistics GetStatistics()
    {
        var allEvents = Matches.SelectMany(m => m.Events).ToList();
        
        return new MatchStatistics
        {
            TotalCorners = allEvents.Count(e => e.EventType == "corner"),
            TotalFreeKicks = allEvents.Count(e => e.EventType == "freekick"),
            TotalPenalties = allEvents.Count(e => e.EventType == "penalty"),
            PenaltiesScored = allEvents.Count(e => e.EventType == "penalty" && e.Outcome == "scored"),
            PenaltiesMissed = allEvents.Count(e => e.EventType == "penalty" && (e.Outcome == "missed" || e.Outcome == "saved")),
            TotalGoals = allEvents.Count(e => e.EventType == "goal"),
            TotalYellowCards = allEvents.Count(e => e.EventType == "yellow_card"),
            TotalRedCards = allEvents.Count(e => e.EventType == "red_card"),
            TotalSubstitutions = allEvents.Count(e => e.EventType == "substitution"),
            TotalShots = allEvents.Count(e => e.EventType == "shot"),
            ShotsOnTarget = allEvents.Count(e => e.EventType == "shot" && e.Outcome == "on_target"),
            ShotsOffTarget = allEvents.Count(e => e.EventType == "shot" && e.Outcome == "off_target"),
            TotalSaves = allEvents.Count(e => e.EventType == "save"),
            TotalFouls = allEvents.Count(e => e.EventType == "foul"),
            TotalOffsides = allEvents.Count(e => e.EventType == "offside"),
            
            // Goal analysis
            GoalsFromCorners = allEvents.Count(e => e.EventType == "goal" && e.Notes.ToLower().Contains("corner")),
            GoalsFromPenalties = allEvents.Count(e => e.EventType == "goal" && e.Notes.ToLower().Contains("penalty")),
            GoalsFromFreeKicks = allEvents.Count(e => e.EventType == "goal" && e.Notes.ToLower().Contains("free kick")),
            HeaderGoals = allEvents.Count(e => e.EventType == "goal" && (e.Details.ShotType == "header" || e.Details.BodyPart == "head")),
            FootGoals = allEvents.Count(e => e.EventType == "goal" && (e.Details.ShotType.Contains("foot") || e.Details.BodyPart.Contains("foot")))
        };
    }
}

public class MatchResult
{
    public string MatchId { get; set; } = string.Empty;
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsFinished { get; set; }
    public DateTime? MatchDateTime { get; set; }
    public string? LiveTickerUrl { get; set; }
    public List<MatchEvent> Events { get; set; } = new();
    
    public string GetScoreDisplay() => 
        HomeScore.HasValue && AwayScore.HasValue ? $"{HomeScore}:{AwayScore}" : Status;
}

public class MatchEvent
{
    public string EventType { get; set; } = string.Empty; // goal, corner, freekick, penalty, yellow_card, red_card, substitution, save, shot, foul, offside
    public int Minute { get; set; }
    public string Player { get; set; } = string.Empty;
    public string SecondaryPlayer { get; set; } = string.Empty; // For substitutions, assists, etc.
    public string Team { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty; // scored, missed, saved, blocked, deflected, on_target, off_target, other
    public EventDetails Details { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}

public class EventDetails
{
    public string Position { get; set; } = string.Empty; // left_corner, right_corner, center, top_left, top_right, bottom_left, bottom_right, outside_post, crossbar, other
    public string Distance { get; set; } = string.Empty; // Distance from goal in meters
    public string ShotType { get; set; } = string.Empty; // header, left_foot, right_foot, volley, half_volley, chip, lob, tap_in, other
    public string AssistPlayer { get; set; } = string.Empty; // Player who assisted (for goals)
    public string CardReason { get; set; } = string.Empty; // Reason for card (foul, dissent, time_wasting, etc.)
    public string SubstitutionReason { get; set; } = string.Empty; // tactical, injury, performance, time_wasting, other
    public string BodyPart { get; set; } = string.Empty; // head, left_foot, right_foot, chest, other
}

public class MatchStatistics
{
    public int TotalCorners { get; set; }
    public int TotalFreeKicks { get; set; }
    public int TotalPenalties { get; set; }
    public int PenaltiesScored { get; set; }
    public int PenaltiesMissed { get; set; }
    public int TotalGoals { get; set; }
    public int TotalYellowCards { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalSubstitutions { get; set; }
    public int TotalShots { get; set; }
    public int ShotsOnTarget { get; set; }
    public int ShotsOffTarget { get; set; }
    public int TotalSaves { get; set; }
    public int TotalFouls { get; set; }
    public int TotalOffsides { get; set; }
    
    // Goal analysis
    public int GoalsFromCorners { get; set; }
    public int GoalsFromPenalties { get; set; }
    public int GoalsFromFreeKicks { get; set; }
    public int HeaderGoals { get; set; }
    public int FootGoals { get; set; }
}