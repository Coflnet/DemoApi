using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Spt;

/// <summary>
/// Controller for Bundesliga crawling operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SptController : ControllerBase
{
    private readonly BundesligaCrawler crawler;
    private readonly ILogger<SptController> logger;

    /// <summary>
    /// Initializes a new instance of the SptController
    /// </summary>
    /// <param name="crawler">The Bundesliga crawler service</param>
    /// <param name="logger">Logger instance</param>
    public SptController(BundesligaCrawler crawler, ILogger<SptController> logger)
    {
        this.crawler = crawler;
        this.logger = logger;
    }

    /// <summary>
    /// Crawls the latest Bundesliga matchday and extracts events from completed matches
    /// </summary>
    /// <returns>Aggregated match results with event details</returns>
    [HttpGet("latest-matchday")]
    public async Task<ActionResult<BundesligaMatchdayResult>> GetLatestMatchday()
    {
        try
        {
            logger.LogInformation("Starting Bundesliga matchday crawling");
            var result = await crawler.CrawlLatestMatchdayAsync();
            
            logger.LogInformation($"Crawling completed successfully. Found {result.CompletedGames} completed games out of {result.TotalGames} total games");
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while crawling Bundesliga matchday");
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    /// <summary>
    /// Gets statistics summary for the latest matchday
    /// </summary>
    /// <returns>Match statistics aggregated across all completed games</returns>
    [HttpGet("statistics")]
    public async Task<ActionResult<MatchStatistics>> GetMatchStatistics()
    {
        try
        {
            var result = await crawler.CrawlLatestMatchdayAsync();
            var statistics = result.GetStatistics();
            
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting match statistics");
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    /// <summary>
    /// Gets all goals from the latest matchday with detailed information
    /// </summary>
    /// <returns>List of goal events with comprehensive details</returns>
    [HttpGet("goals")]
    public async Task<ActionResult<List<MatchEvent>>> GetGoals()
    {
        try
        {
            var result = await crawler.CrawlLatestMatchdayAsync();
            var goals = result.Matches
                .SelectMany(m => m.Events)
                .Where(e => e.EventType == "goal")
                .OrderBy(e => e.Minute)
                .ToList();
            
            return Ok(goals);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting goals");
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    /// <summary>
    /// Gets all cards (yellow and red) from the latest matchday
    /// </summary>
    /// <returns>List of card events</returns>
    [HttpGet("cards")]
    public async Task<ActionResult<List<MatchEvent>>> GetCards()
    {
        try
        {
            var result = await crawler.CrawlLatestMatchdayAsync();
            var cards = result.Matches
                .SelectMany(m => m.Events)
                .Where(e => e.EventType == "yellow_card" || e.EventType == "red_card")
                .OrderBy(e => e.Minute)
                .ToList();
            
            return Ok(cards);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while getting cards");
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    /// <summary>
    /// Gets events by specific type from the latest matchday
    /// </summary>
    /// <param name="eventType">Type of event to filter by</param>
    /// <returns>List of events of the specified type</returns>
    [HttpGet("events/{eventType}")]
    public async Task<ActionResult<List<MatchEvent>>> GetEventsByType(string eventType)
    {
        try
        {
            var result = await crawler.CrawlLatestMatchdayAsync();
            var events = result.Matches
                .SelectMany(m => m.Events)
                .Where(e => e.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Minute)
                .ToList();
            
            return Ok(events);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error occurred while getting events of type {eventType}");
            return StatusCode(500, "An error occurred while processing the request");
        }
    }
}
