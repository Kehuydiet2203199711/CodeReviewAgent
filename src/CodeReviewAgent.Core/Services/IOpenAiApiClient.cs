namespace CodeReviewAgent.Core.Services;

/// <summary>
/// Provides a low-level abstraction over the OpenAI Chat Completions API.
/// </summary>
public interface IOpenAiApiClient
{
    /// <summary>
    /// Sends a message to the OpenAI Chat Completions API and returns the raw text content
    /// of the first choice response.
    /// </summary>
    /// <param name="systemPrompt">The system-level instruction for the model.</param>
    /// <param name="userMessage">The user message content to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The raw text response from the model.</returns>
    Task<string> SendMessageAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default);
}
