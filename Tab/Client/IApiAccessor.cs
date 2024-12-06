/*
 * Whisper Asr Webservice
 *
 * Whisper ASR Webservice is a general-purpose speech recognition webservice.
 *
 * The version of the OpenAPI document: 1.5.0
 * Generated by: https://github.com/openapitools/openapi-generator.git
 */


using System;

namespace Coflnet.Whisper.Client
{
    /// <summary>
    /// Represents configuration aspects required to interact with the API endpoints.
    /// </summary>
    public interface IApiAccessor
    {
        /// <summary>
        /// Gets or sets the configuration object
        /// </summary>
        /// <value>An instance of the Configuration</value>
        IReadableConfiguration Configuration { get; set; }

        /// <summary>
        /// Gets the base path of the API client.
        /// </summary>
        /// <value>The base path</value>
        string GetBasePath();

        /// <summary>
        /// Provides a factory method hook for the creation of exceptions.
        /// </summary>
        ExceptionFactory ExceptionFactory { get; set; }
    }
}