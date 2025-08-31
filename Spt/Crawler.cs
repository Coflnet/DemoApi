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

Extract ONLY the following specific event types and return them as a JSON array:

1. **Corners**: Who took them, when (minute), and any notable details
2. **Free kicks**: Who took them, key action, and notes  
3. **Penalties**: Who took them and whether they scored or missed

For each event, use this exact JSON structure:
{{
  ""eventType"": ""corner"" | ""freekick"" | ""penalty"",
  ""minute"": number,
  ""player"": ""player name"",
  ""team"": ""team name"",
  ""action"": ""description of what happened"",
  ""outcome"": ""scored"" | ""missed"" | ""saved"" | ""deflected"" | ""other"",
  ""notes"": ""additional relevant details""
}}

Rules:
- Only extract corners, free kicks, and penalties
- Ignore goals, substitutions, cards, and other events
- Be precise with minute timing
- Include player names when mentioned
- For penalties: outcome should be ""scored"" or ""missed"" or ""saved""
- For corners: note if they led to anything significant
- For free kicks: note the outcome (shot, cross, etc.)

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
                    ChatMessage.FromSystem("You are a football event extractor. Extract only corners, free kicks, and penalties from live ticker text and return as JSON array."),
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
            PenaltiesMissed = allEvents.Count(e => e.EventType == "penalty" && e.Outcome == "missed"),
            TotalGoals = Matches.Sum(m => (m.HomeScore ?? 0) + (m.AwayScore ?? 0))
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
    public string EventType { get; set; } = string.Empty; // corner, freekick, penalty
    public int Minute { get; set; }
    public string Player { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty; // scored, missed, saved, deflected, other
    public string Notes { get; set; } = string.Empty;
}

public class MatchStatistics
{
    public int TotalCorners { get; set; }
    public int TotalFreeKicks { get; set; }
    public int TotalPenalties { get; set; }
    public int PenaltiesScored { get; set; }
    public int PenaltiesMissed { get; set; }
    public int TotalGoals { get; set; }
}